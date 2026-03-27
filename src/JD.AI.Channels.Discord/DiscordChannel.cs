using Discord;
using Discord.WebSocket;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;

namespace JD.AI.Channels.Discord;

/// <summary>
/// Discord channel adapter using Discord.Net.
/// Supports DMs, guild channels, thread-based conversations,
/// and native slash command registration via <see cref="ICommandAwareChannel"/>.
/// </summary>
public sealed class DiscordChannel : Core.Channels.IChannel, ICommandAwareChannel
{
    private readonly string _botToken;
    private readonly bool _allowBots;
    private readonly HashSet<ulong> _allowedBotIds;
    private readonly bool _enableReactions;
    private readonly bool _requireMention;
    private readonly HashSet<ulong> _allowedMentionRoleIds;
    private readonly Dictionary<ulong, string> _activeReactionByMessageId = new();
    private readonly Dictionary<string, ulong> _pendingInboundByChannelId = new();
    private DiscordSocketClient? _client;
    private TaskCompletionSource? _readyTcs;
    private ICommandRegistry? _commandRegistry;

    /// <summary>Prefix used for slash commands (e.g., "jdai-help").</summary>
    public const string CommandPrefix = "jdai-";

    public DiscordChannel(
        string botToken,
        bool allowBots = false,
        IEnumerable<ulong>? allowedBotIds = null,
        bool enableReactions = false,
        bool requireMention = true,
        IEnumerable<ulong>? allowedMentionRoleIds = null)
    {
        _botToken = botToken;
        _allowBots = allowBots;
        _allowedBotIds = allowedBotIds != null ? new HashSet<ulong>(allowedBotIds) : new HashSet<ulong>();
        _enableReactions = enableReactions;
        _requireMention = requireMention;
        _allowedMentionRoleIds = allowedMentionRoleIds != null ? new HashSet<ulong>(allowedMentionRoleIds) : new HashSet<ulong>();
    }

    public string ChannelType => "discord";
    public string DisplayName => "Discord";
    public bool IsConnected => _client?.ConnectionState == ConnectionState.Connected;

    public event Func<ChannelMessage, Task>? MessageReceived;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _readyTcs = new TaskCompletionSource();
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                | GatewayIntents.GuildMessages
                | GatewayIntents.DirectMessages
                | GatewayIntents.MessageContent,
            MessageCacheSize = 50
        });

        _client.Ready += OnReadyAsync;
        _client.MessageReceived += OnMessageReceivedAsync;
        _client.SlashCommandExecuted += OnSlashCommandExecutedAsync;

        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();

        // Wait for ready or cancellation
        using var reg = ct.Register(() => _readyTcs.TrySetCanceled());
        await _readyTcs.Task;
    }

    /// <inheritdoc />
    public async Task RegisterCommandsAsync(ICommandRegistry registry, CancellationToken ct = default)
    {
        _commandRegistry = registry;

        if (_client is null || _client.ConnectionState != ConnectionState.Connected)
            return;

        await RegisterSlashCommandsAsync();
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_client is not null)
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
        }
    }

    public async Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
    {
        if (_client is null) throw new InvalidOperationException("Not connected.");

        if (ulong.TryParse(conversationId, out var channelId)
            && await _client.GetChannelAsync(channelId) is IMessageChannel channel)
        {
            try
            {
                if (_enableReactions && _pendingInboundByChannelId.TryGetValue(conversationId, out var inboundMsgId))
                {
                    if (await channel.GetMessageAsync(inboundMsgId) is IUserMessage inbound)
                        await SetStatusReactionAsync(inbound, "✍️");
                }

                var outbound = string.IsNullOrWhiteSpace(content)
                    ? "I processed your message but produced no text output. Please retry your request."
                    : content;

                await channel.SendMessageAsync(outbound);

                if (_enableReactions && _pendingInboundByChannelId.TryGetValue(conversationId, out var sentInboundMsgId))
                {
                    if (await channel.GetMessageAsync(sentInboundMsgId) is IUserMessage inbound)
                        await SetStatusReactionAsync(inbound, "✅");
                }
            }
            finally
            {
                _pendingInboundByChannelId.Remove(conversationId);
            }
        }
    }

    private async Task OnReadyAsync()
    {
        // Register slash commands if registry was provided before ready
        if (_commandRegistry is not null)
            await RegisterSlashCommandsAsync();

        _readyTcs?.TrySetResult();
    }

    private async Task RegisterSlashCommandsAsync()
    {
        if (_client is null || _commandRegistry is null) return;

        var builders = new List<ApplicationCommandProperties>();

        foreach (var cmd in _commandRegistry.Commands)
        {
            var builder = new SlashCommandBuilder()
                .WithName($"{CommandPrefix}{cmd.Name}")
                .WithDescription(Truncate(cmd.Description, 100));

            foreach (var param in cmd.Parameters)
            {
                builder.AddOption(
                    param.Name,
                    MapParameterType(param.Type),
                    Truncate(param.Description, 100),
                    isRequired: param.IsRequired,
                    choices: param.Choices.Count > 0
                        ? param.Choices.Select(c =>
                            new ApplicationCommandOptionChoiceProperties { Name = c, Value = c }).ToArray()
                        : null);
            }

            builders.Add(builder.Build());
        }

        await _client.BulkOverwriteGlobalApplicationCommandsAsync(builders.ToArray());
    }

    private async Task OnSlashCommandExecutedAsync(SocketSlashCommand interaction)
    {
        if (_commandRegistry is null)
        {
            await interaction.RespondAsync("Commands not available.", ephemeral: true);
            return;
        }

        // Strip the prefix to find the command name
        var fullName = interaction.Data.Name;
        var commandName = fullName.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase)
            ? fullName[CommandPrefix.Length..]
            : fullName;

        var command = _commandRegistry.GetCommand(commandName);
        if (command is null)
        {
            await interaction.RespondAsync($"Unknown command: `{fullName}`", ephemeral: true);
            return;
        }

        // Parse arguments from interaction options
        var args = new Dictionary<string, string>();
        if (interaction.Data.Options is not null)
        {
            foreach (var opt in interaction.Data.Options)
            {
                args[opt.Name] = opt.Value?.ToString() ?? "";
            }
        }

        var context = new CommandContext
        {
            CommandName = commandName,
            InvokerId = interaction.User.Id.ToString(),
            InvokerDisplayName = interaction.User.GlobalName ?? interaction.User.Username,
            ChannelId = interaction.Channel.Id.ToString(),
            ChannelType = ChannelType,
            Arguments = args
        };

        try
        {
            var result = await command.ExecuteAsync(context);
            await interaction.RespondAsync(result.Content, ephemeral: result.Ephemeral);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            await interaction.RespondAsync($"❌ Command error: {ex.Message}", ephemeral: true);
        }
#pragma warning restore CA1031
    }

    private async Task OnMessageReceivedAsync(SocketMessage msg)
    {
        // Skip bot messages unless explicitly allowed
        if (msg.Author.IsBot)
        {
            if (!_allowBots && !_allowedBotIds.Contains(msg.Author.Id))
                return;
        }

        // In guild channels, only process messages with an explicit mention token when requireMention is enabled.
        // This intentionally ignores implicit/reply mentions so random replies don't trigger the bot.
        if (_requireMention && msg.Channel is SocketGuildChannel && _client?.CurrentUser is not null)
        {
            var content = msg.Content ?? string.Empty;
            var userMentionToken = $"<@{_client.CurrentUser.Id}>";
            var userNicknameMentionToken = $"<@!{_client.CurrentUser.Id}>";
            var explicitlyMentioned = content.Contains(userMentionToken, StringComparison.Ordinal)
                || content.Contains(userNicknameMentionToken, StringComparison.Ordinal);

            var roleMentioned = _allowedMentionRoleIds.Any(roleId =>
                content.Contains($"<@&{roleId}>", StringComparison.Ordinal));

            if (!explicitlyMentioned && !roleMentioned)
                return;
        }

        if (_enableReactions && msg is IUserMessage inboundUserMessage)
            await SetStatusReactionAsync(inboundUserMessage, "👀");

        var metadata = new Dictionary<string, string>();
        for (var i = 0; i < msg.Attachments.Count; i++)
        {
            var a = msg.Attachments.ElementAt(i);
            metadata[$"attachment.{i}.url"] = a.Url;
            metadata[$"attachment.{i}.filename"] = a.Filename;
            metadata[$"attachment.{i}.contentType"] = a.ContentType ?? "application/octet-stream";
            metadata[$"attachment.{i}.sizeBytes"] = a.Size.ToString();
        }

        var channelMessage = new ChannelMessage
        {
            Id = msg.Id.ToString(),
            ChannelId = msg.Channel.Id.ToString(),
            SenderId = msg.Author.Id.ToString(),
            SenderDisplayName = msg.Author.GlobalName ?? msg.Author.Username,
            Content = msg.Content ?? string.Empty,
            Timestamp = msg.Timestamp,
            ThreadId = msg.Thread?.Id.ToString(),
            Metadata = metadata,
            Attachments = msg.Attachments.Select(a => new ChannelAttachment(
                a.Filename,
                a.ContentType ?? "application/octet-stream",
                (long)a.Size,
                async ct =>
                {
                    using var http = new HttpClient();
                    return await http.GetStreamAsync(new Uri(a.Url), ct);
                })).ToList()
        };

        if (MessageReceived is null)
            return;

        // Start typing indicator loop — shows "<bot> is typing..." in Discord
        // while the agent processes the message. Refreshes every 8s (indicator lasts ~10s).
        using var typingCts = new CancellationTokenSource();
        var typingTask = RunTypingIndicatorAsync(msg.Channel, typingCts.Token);

        try
        {
            if (_enableReactions && msg is IUserMessage inboundUserMessage2)
            {
                _pendingInboundByChannelId[channelMessage.ChannelId] = inboundUserMessage2.Id;
                await SetStatusReactionAsync(inboundUserMessage2, "🧠");
            }

            await MessageReceived.Invoke(channelMessage);
        }
#pragma warning disable CA1031
        catch
        {
            if (_enableReactions && msg is IUserMessage inboundUserMessage3)
                await SetStatusReactionAsync(inboundUserMessage3, "❌");
            throw;
        }
#pragma warning restore CA1031
        finally
        {
            // Stop typing indicator when response is ready
            await typingCts.CancelAsync();
            try { await typingTask; } catch (OperationCanceledException) { /* expected */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.StopAsync();
            _client.Dispose();
        }
    }

    /// <summary>
    /// Triggers the Discord "is typing..." indicator repeatedly until cancelled.
    /// Each trigger lasts ~10 seconds; we refresh every 8 seconds for continuity.
    /// </summary>
    private static async Task RunTypingIndicatorAsync(ISocketMessageChannel channel, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await channel.TriggerTypingAsync();
                await Task.Delay(TimeSpan.FromSeconds(8), ct);
            }
        }
        catch (OperationCanceledException) { /* expected — typing stopped */ }
#pragma warning disable CA1031
        catch { /* best-effort — don't crash on typing failures */ }
#pragma warning restore CA1031
    }

    private async Task SetStatusReactionAsync(IUserMessage msg, string emoji)
    {
        if (_client?.CurrentUser is null)
            return;

        if (_activeReactionByMessageId.TryGetValue(msg.Id, out var previous) && !string.Equals(previous, emoji, StringComparison.Ordinal))
        {
            await TryRemoveReactionAsync(msg, previous);
        }

        await TryAddReactionAsync(msg, emoji);
        _activeReactionByMessageId[msg.Id] = emoji;
    }

    private async Task TryAddReactionAsync(IUserMessage msg, string emoji)
    {
        try
        {
            await msg.AddReactionAsync(new Emoji(emoji));
        }
        catch
        {
            // best-effort only
        }
    }

    private async Task TryRemoveReactionAsync(IUserMessage msg, string emoji)
    {
        try
        {
            await msg.RemoveReactionAsync(new Emoji(emoji), _client!.CurrentUser);
        }
        catch
        {
            // best-effort only
        }
    }

    private static ApplicationCommandOptionType MapParameterType(CommandParameterType type) => type switch
    {
        CommandParameterType.Number => ApplicationCommandOptionType.Integer,
        CommandParameterType.Boolean => ApplicationCommandOptionType.Boolean,
        _ => ApplicationCommandOptionType.String
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
