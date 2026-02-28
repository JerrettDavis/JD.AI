using JD.AI.Core.Channels;
using SlackNet;
using SlackNet.Events;
using SlackNet.WebApi;

namespace JD.AI.Channels.Slack;

/// <summary>
/// Slack channel adapter using SlackNet Socket Mode.
/// </summary>
public sealed class SlackChannel : IChannel
{
    private readonly string _botToken;
    private readonly string _appToken;
    private ISlackApiClient? _api;
    private SlackServiceBuilder? _serviceBuilder;
    private bool _connected;

    public SlackChannel(string botToken, string appToken)
    {
        _botToken = botToken;
        _appToken = appToken;
    }

    public string ChannelType => "slack";
    public string DisplayName => "Slack";
    public bool IsConnected => _connected;

    public event Func<ChannelMessage, Task>? MessageReceived;

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

        await _api.Chat.PostMessage(new Message
        {
            Channel = conversationId,
            Text = content
        });
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

    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }
}
