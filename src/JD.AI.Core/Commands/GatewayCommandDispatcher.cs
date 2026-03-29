using System.Text.RegularExpressions;

namespace JD.AI.Core.Commands;

/// <summary>
/// Shared command fast-path parser/executor for gateway channels.
/// Keeps command dispatch logic centralized so channel adapters and bridge integrations
/// can reuse one source of truth.
/// </summary>
public static class GatewayCommandDispatcher
{
    private const string JdaiCommandPrefix = "/jdai-";
    private const string DiscordBangModelPrefix = "!model";
    private const string DiscordSlashModelPrefix = "/model";

    private static readonly Regex DiscordLeadingMentionRegex =
        new(@"^(\s*(?:<@!?\d+>|@\S+)\s*)+", RegexOptions.Compiled);

    public static async Task<CommandDispatchResult> TryDispatchAsync(
        ICommandRegistry? commandRegistry,
        string channelType,
        string message,
        string invokerId,
        string channelId,
        string? invokerDisplayName = null,
        CancellationToken ct = default)
    {
        if (commandRegistry is null || string.IsNullOrWhiteSpace(message))
            return CommandDispatchResult.NotHandled;

        if (!TryMap(channelType, message, out var mapped))
            return CommandDispatchResult.NotHandled;

        var command = commandRegistry.GetCommand(mapped.CommandName);
        if (command is null)
        {
            var hint = mapped.HelpHint ?? "Use /jdai-help to see available commands.";
            return CommandDispatchResult.HandledFailure($"Command not found: {mapped.CommandName}. {hint}", mapped.CommandName);
        }

        var context = new CommandContext
        {
            CommandName = mapped.CommandName,
            InvokerId = invokerId,
            InvokerDisplayName = invokerDisplayName,
            ChannelId = channelId,
            ChannelType = channelType,
            Arguments = BuildArgumentMap(command, mapped.Args),
        };

        try
        {
            var result = await command.ExecuteAsync(context, ct).ConfigureAwait(false);
            return new CommandDispatchResult(
                Handled: true,
                Success: result.Success,
                Response: result.Content,
                CommandName: mapped.CommandName,
                SourceLabel: mapped.SourceLabel);
        }
        catch (Exception ex)
        {
            return CommandDispatchResult.HandledFailure($"Command error: {ex.Message}", mapped.CommandName, mapped.SourceLabel);
        }
    }

    internal static bool TryMap(string channelType, string message, out MappedCommand mapped)
    {
        mapped = default;

        if (TryMapDiscordModelCommand(channelType, message, out mapped))
            return true;

        if (message.StartsWith(JdaiCommandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var withoutPrefix = message[JdaiCommandPrefix.Length..];
            var parts = withoutPrefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            mapped = new MappedCommand(
                parts[0],
                [.. parts.Skip(1)],
                "Use /jdai-help to see available commands.",
                $"/jdai-{parts[0]}");
            return true;
        }

        return false;
    }

    internal static bool TryMapDiscordModelCommand(string channelType, string message, out MappedCommand mapped)
    {
        mapped = default;

        if (!string.Equals(channelType, "discord", StringComparison.OrdinalIgnoreCase))
            return false;

        var cleaned = DiscordLeadingMentionRegex.Replace(message, "").TrimStart();
        if (string.IsNullOrWhiteSpace(cleaned))
            return false;

        var isBang = cleaned.StartsWith(DiscordBangModelPrefix, StringComparison.OrdinalIgnoreCase);
        var isSlash = cleaned.StartsWith(DiscordSlashModelPrefix, StringComparison.OrdinalIgnoreCase);
        if (!isBang && !isSlash)
            return false;

        var prefixLength = isBang ? DiscordBangModelPrefix.Length : DiscordSlashModelPrefix.Length;
        var tail = cleaned[prefixLength..].Trim();

        if (tail.Length == 0 || tail.Equals("current", StringComparison.OrdinalIgnoreCase))
        {
            mapped = new MappedCommand("status", [], "Use !model list, !model current, or !model set <model>.", "!model -> status");
            return true;
        }

        if (tail.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            mapped = new MappedCommand("models", [], "Use !model list, !model current, or !model set <model>.", "!model -> models");
            return true;
        }

        if (tail.StartsWith("set", StringComparison.OrdinalIgnoreCase))
        {
            var model = tail[3..].Trim();
            if (model.Length == 0)
                return false;

            mapped = new MappedCommand("switch", [model], "Use !model list, !model current, or !model set <model>.", "!model -> switch");
            return true;
        }

        return false;
    }

    private static Dictionary<string, string> BuildArgumentMap(IChannelCommand command, IReadOnlyList<string> args)
    {
        var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Count && i < command.Parameters.Count; i++)
        {
            mapped[command.Parameters[i].Name] = args[i];
        }

        return mapped;
    }

    internal readonly record struct MappedCommand(
        string CommandName,
        string[] Args,
        string? HelpHint,
        string SourceLabel);
}

public readonly record struct CommandDispatchResult(
    bool Handled,
    bool Success,
    string Response,
    string? CommandName,
    string? SourceLabel)
{
    public static CommandDispatchResult NotHandled => new(false, false, string.Empty, null, null);

    public static CommandDispatchResult HandledFailure(string response, string? commandName = null, string? sourceLabel = null) =>
        new(true, false, response, commandName, sourceLabel);
}
