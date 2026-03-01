using Discord;
using Discord.WebSocket;
using JD.AI.Core.Channels;

namespace JD.AI.Channels.Discord;

/// <summary>
/// Discord channel adapter using Discord.Net.
/// Supports DMs, guild channels, and thread-based conversations.
/// </summary>
public sealed class DiscordChannel : Core.Channels.IChannel
{
    private readonly string _botToken;
    private DiscordSocketClient? _client;
    private TaskCompletionSource? _readyTcs;

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

        _client.Ready += () => { _readyTcs.TrySetResult(); return Task.CompletedTask; };
        _client.MessageReceived += OnMessageReceivedAsync;

        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();

        // Wait for ready or cancellation
        using var reg = ct.Register(() => _readyTcs.TrySetCanceled());
        await _readyTcs.Task;
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
}
