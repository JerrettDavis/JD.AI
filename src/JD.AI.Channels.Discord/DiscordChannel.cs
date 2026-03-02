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
    private DiscordSocketClient? _client;
    private TaskCompletionSource? _readyTcs;
    private ICommandRegistry? _commandRegistry;

    /// <summary>Prefix used for slash commands (e.g., "jdai-help").</summary>
    public const string CommandPrefix = "jdai-";

    public DiscordChannel(string botToken)
    {
        _botToken = botToken;
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
            await channel.SendMessageAsync(content);
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
        if (msg.Author.IsBot) return;

        var channelMessage = new ChannelMessage
        {
            Id = msg.Id.ToString(),
            ChannelId = msg.Channel.Id.ToString(),
            SenderId = msg.Author.Id.ToString(),
            SenderDisplayName = msg.Author.GlobalName ?? msg.Author.Username,
            Content = msg.Content,
            Timestamp = msg.Timestamp,
            ThreadId = msg.Thread?.Id.ToString(),
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

        if (MessageReceived is not null)
            await MessageReceived.Invoke(channelMessage);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.StopAsync();
            _client.Dispose();
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
