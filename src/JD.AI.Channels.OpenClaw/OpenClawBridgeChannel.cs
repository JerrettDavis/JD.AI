using System.Net.Http.Json;
using JD.AI.Core.Channels;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// Bridges JD.AI gateway to an OpenClaw instance.
/// Implements <see cref="IChannel"/> to appear as a regular channel in the JD.AI gateway.
/// Messages sent to this channel are forwarded to OpenClaw; messages from OpenClaw
/// are surfaced as inbound channel messages.
/// </summary>
public sealed class OpenClawBridgeChannel : IChannel
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenClawBridgeChannel> _logger;
    private readonly OpenClawConfig _config;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    public string ChannelType => "openclaw";
    public string DisplayName => $"OpenClaw ({_config.InstanceName})";
    public bool IsConnected { get; private set; }

    public event Func<ChannelMessage, Task>? MessageReceived;

    public OpenClawBridgeChannel(HttpClient http, ILogger<OpenClawBridgeChannel> logger, OpenClawConfig config)
    {
        _http = http;
        _logger = logger;
        _config = config;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Verify OpenClaw is reachable
        var response = await _http.GetAsync(new Uri($"{_config.BaseUrl}/api/health"), ct);
        response.EnsureSuccessStatusCode();

        IsConnected = true;
        _logger.LogInformation("Connected to OpenClaw at {Url}", _config.BaseUrl);

        // Start polling for inbound messages
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollTask = PollMessagesAsync(_pollCts.Token);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_pollCts is not null)
        {
            await _pollCts.CancelAsync();
            if (_pollTask is not null)
            {
                try { await _pollTask; }
                catch (OperationCanceledException) { /* expected */ }
            }

            _pollCts.Dispose();
            _pollCts = null;
        }

        IsConnected = false;
        _logger.LogInformation("Disconnected from OpenClaw");
    }

    public async Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
    {
        var payload = new OpenClawOutboundMessage
        {
            Channel = _config.TargetChannel,
            Content = content,
            Sender = "jdai",
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "jdai-gateway",
                ["original_channel"] = conversationId,
            },
        };

        await _http.PostAsJsonAsync(new Uri($"{_config.BaseUrl}/api/messages"), payload, ct);
    }

    private async Task PollMessagesAsync(CancellationToken ct)
    {
        var lastTimestamp = DateTimeOffset.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.PollIntervalMs, ct);

                var url = $"{_config.BaseUrl}/api/messages?since={lastTimestamp:O}&channel={_config.SourceChannel}";
                var messages = await _http.GetFromJsonAsync<OpenClawInboundMessage[]>(new Uri(url), ct);

                if (messages is { Length: > 0 })
                {
                    foreach (var msg in messages)
                    {
                        lastTimestamp = msg.Timestamp;

                        var channelMsg = new ChannelMessage
                        {
                            Id = msg.Id,
                            ChannelId = $"openclaw-{_config.InstanceName}",
                            SenderId = msg.Sender,
                            SenderDisplayName = msg.Sender,
                            Content = msg.Content,
                            Timestamp = msg.Timestamp,
                        };

                        if (MessageReceived is not null)
                            await MessageReceived.Invoke(channelMsg);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error polling OpenClaw messages");
                await Task.Delay(5000, ct); // Back off on error
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
