using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using SlackNet;
using SlackNet.Events;
using SlackNet.WebApi;

namespace JD.AI.Channels.Slack;

/// <summary>
/// Slack channel adapter using SlackNet Socket Mode.
/// Supports native slash command handling via <see cref="ICommandAwareChannel"/>.
/// </summary>
public sealed class SlackChannel : IChannel, ICommandAwareChannel
{
    private readonly string _botToken;
    private readonly string _appToken;
    private ISlackApiClient? _api;
    private SlackServiceBuilder? _serviceBuilder;
    private bool _connected;
    private ICommandRegistry? _commandRegistry;

    /// <summary>Prefix for Slack slash commands (e.g., "/jdai-help").</summary>
    public const string CommandPrefix = "/jdai-";

    public SlackChannel(string botToken, string appToken)
    {
        _botToken = botToken;
        _appToken = appToken;
    }

    public string ChannelType => "slack";
    public string DisplayName => "Slack";
    public bool IsConnected => _connected;

    public event Func<ChannelMessage, Task>? MessageReceived;

    /// <inheritdoc />
    public Task RegisterCommandsAsync(ICommandRegistry registry, CancellationToken ct = default)
    {
        _commandRegistry = registry;
        return Task.CompletedTask;
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        _serviceBuilder = new SlackServiceBuilder()
            .UseApiToken(_botToken)
            .UseAppLevelToken(_appToken);

        _api = _serviceBuilder.GetApiClient();
        _connected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _connected = false;
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
    {
        if (_api is null) throw new InvalidOperationException("Not connected.");

        // SlackAPI does not expose CancellationToken on PostMessage
#pragma warning disable CA2016
        await _api.Chat.PostMessage(new Message
        {
            Channel = conversationId,
            Text = content
        });
#pragma warning restore CA2016
    }

    /// <summary>
    /// Process an incoming Slack event (called by event handler registration).
    /// </summary>
    public async Task HandleMessageAsync(MessageEvent slackEvent)
    {
        if (slackEvent.User is null) return; // Skip bot messages

        var msg = new ChannelMessage
        {
            Id = slackEvent.Ts ?? Guid.NewGuid().ToString("N"),
            ChannelId = slackEvent.Channel ?? "unknown",
            SenderId = slackEvent.User,
            Content = slackEvent.Text ?? "",
            Timestamp = DateTimeOffset.UtcNow,
            ThreadId = slackEvent.ThreadTs
        };

        if (MessageReceived is not null)
            await MessageReceived.Invoke(msg);
    }

    /// <summary>
    /// Handles a Slack slash command payload.
    /// Called by the gateway when a /jdai-* command is received from Slack's API.
    /// </summary>
    /// <param name="commandName">The command name without slash (e.g., "jdai-help").</param>
    /// <param name="text">The text after the command.</param>
    /// <param name="userId">Slack user ID.</param>
    /// <param name="channelId">Slack channel ID.</param>
    /// <returns>Response text to send back to Slack.</returns>
    public async Task<string> HandleSlashCommandAsync(
        string commandName, string text, string userId, string channelId)
    {
        if (_commandRegistry is null)
            return "Commands not available.";

        // Strip prefix to get command name
        var name = commandName.StartsWith("jdai-", StringComparison.OrdinalIgnoreCase)
            ? commandName["jdai-".Length..]
            : commandName;

        var command = _commandRegistry.GetCommand(name);
        if (command is null)
            return $"Unknown command: {commandName}. Use /jdai-help to see available commands.";

        // Parse space-separated text into positional args
        var args = new Dictionary<string, string>();
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length && i < command.Parameters.Count; i++)
        {
            args[command.Parameters[i].Name] = parts[i];
        }

        var context = new CommandContext
        {
            CommandName = name,
            InvokerId = userId,
            ChannelId = channelId,
            ChannelType = ChannelType,
            Arguments = args
        };

        try
        {
            var result = await command.ExecuteAsync(context);
            return result.Content;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            return $"Command error: {ex.Message}";
        }
#pragma warning restore CA1031
    }

    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }
}
