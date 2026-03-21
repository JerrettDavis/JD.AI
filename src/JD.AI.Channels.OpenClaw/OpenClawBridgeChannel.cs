using System.Text.Json;
using JD.AI.Core.Channels;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// Bridges JD.AI gateway to an OpenClaw instance via WebSocket JSON-RPC.
/// Implements <see cref="IChannel"/> to appear as a regular channel in the JD.AI gateway.
/// Messages sent to this channel are forwarded to OpenClaw via <c>chat.send</c>;
/// messages from OpenClaw are surfaced as inbound channel messages via <c>chat</c> events.
/// </summary>
public sealed class OpenClawBridgeChannel : IChannel
{
    private readonly OpenClawRpcClient _rpc;
    private readonly ILogger<OpenClawBridgeChannel> _logger;
    private readonly OpenClawConfig _config;

    public string ChannelType => "openclaw";
    public string DisplayName => $"OpenClaw ({_config.InstanceName})";
    public bool IsConnected => _rpc.IsConnected;

    /// <summary>The underlying RPC client for direct event subscription.</summary>
    public OpenClawRpcClient RpcClient => _rpc;

    public event Func<ChannelMessage, Task>? MessageReceived;

    public OpenClawBridgeChannel(
        OpenClawRpcClient rpc,
        ILogger<OpenClawBridgeChannel> logger,
        OpenClawConfig config)
    {
        _rpc = rpc;
        _logger = logger;
        _config = config;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Idempotent — skip if already connected
        if (_rpc.IsConnected)
        {
            _logger.LogDebug("OpenClaw bridge already connected, skipping duplicate ConnectAsync");
            return;
        }

        _rpc.EventReceived += OnEvent;
        await _rpc.ConnectAsync(ct);

        // Subscribe to chat events for the default session
        _ = await _rpc.RequestAsync("chat.history", new { sessionKey = _config.SessionKey, limit = 1 }, ct);
        _logger.LogInformation(
            "Connected to OpenClaw at {Url}, session={Session}",
            _config.WebSocketUrl, _config.SessionKey);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _rpc.EventReceived -= OnEvent;
        try
        {
            await _rpc.DisconnectAsync();
        }
        catch (ObjectDisposedException)
        {
            // RPC client already disposed during shutdown
        }

        _logger.LogInformation("Disconnected from OpenClaw");
    }

    public async Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
    {
        var sessionKey = string.IsNullOrEmpty(conversationId) ? _config.SessionKey : conversationId;

        var response = await _rpc.RequestAsync("chat.send", new
        {
            sessionKey,
            idempotencyKey = Guid.NewGuid().ToString(),
            message = content,
        }, ct);

        if (!response.Ok)
        {
            var error = response.Error?.GetProperty("message").GetString() ?? "unknown error";
            _logger.LogWarning("chat.send failed: {Error}", error);
        }
    }

    /// <summary>
    /// Injects a message into an OpenClaw session without triggering agent processing.
    /// Use this for injecting JD.AI-generated responses back into the conversation.
    /// </summary>
    public async Task InjectMessageAsync(string sessionKey, string content, CancellationToken ct = default)
    {
        var response = await _rpc.RequestAsync("chat.inject", new
        {
            sessionKey,
            message = content,
        }, ct);

        if (!response.Ok)
        {
            var error = response.Error?.GetProperty("message").GetString() ?? "unknown error";
            _logger.LogWarning("chat.inject failed for session '{Session}': {Error}", sessionKey, error);
        }
    }

    /// <summary>
    /// Aborts the currently running agent for an OpenClaw session.
    /// Used by the intercept handler to stop OpenClaw's own processing.
    /// </summary>
    public async Task AbortSessionAsync(string sessionKey, CancellationToken ct = default)
    {
        var response = await _rpc.RequestAsync("chat.abort", new
        {
            sessionKey,
        }, ct);

        if (!response.Ok)
        {
            var error = response.Error?.GetProperty("message").GetString() ?? "unknown error";
            _logger.LogWarning("chat.abort failed for session '{Session}': {Error}", sessionKey, error);
        }
    }

    /// <summary>Lists all active sessions on the OpenClaw gateway.</summary>
    public async Task<RpcResponse> ListSessionsAsync(CancellationToken ct = default) =>
        await _rpc.RequestAsync("sessions.list", new { }, ct);

    /// <summary>Gets channel status from the OpenClaw gateway.</summary>
    public async Task<RpcResponse> GetChannelStatusAsync(CancellationToken ct = default) =>
        await _rpc.RequestAsync("channels.status", new { }, ct);

    /// <summary>Gets skill status from the OpenClaw gateway.</summary>
    public async Task<RpcResponse> GetSkillStatusAsync(CancellationToken ct = default) =>
        await _rpc.RequestAsync("skills.status", new { }, ct);

    /// <summary>Sends an arbitrary RPC request to the OpenClaw gateway.</summary>
    public Task<RpcResponse> RpcAsync(string method, object? parameters = null, CancellationToken ct = default) =>
        _rpc.RequestAsync(method, parameters, ct);

    /// <summary>
    /// Deletes OpenClaw sessions whose keys begin with one of the provided prefixes.
    /// Intended for bridge-disable cleanup of JD.AI-managed session namespaces.
    /// </summary>
    public async Task<int> DeleteSessionsByPrefixAsync(
        IEnumerable<string> sessionKeyPrefixes,
        IEnumerable<string>? sessionKeyContains = null,
        bool deleteTranscript = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessionKeyPrefixes);

        var prefixes = sessionKeyPrefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var containsParts = (sessionKeyContains ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (prefixes.Length == 0 && containsParts.Length == 0)
            return 0;

        var listResponse = await ListSessionsAsync(ct).ConfigureAwait(false);
        if (!listResponse.Ok || !listResponse.Payload.HasValue)
        {
            var error = listResponse.Error?.GetProperty("message").GetString() ?? "unknown error";
            _logger.LogWarning("sessions.list failed during session cleanup: {Error}", error);
            return 0;
        }

        var keys = ExtractSessionKeys(listResponse.Payload.Value)
            .Where(key =>
                prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                || containsParts.Any(part => key.Contains(part, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var deleted = 0;
        foreach (var key in keys)
        {
            if (key.EndsWith(":main", StringComparison.OrdinalIgnoreCase))
            {
                if (await TryResetSessionAsync(key, ct).ConfigureAwait(false))
                {
                    deleted++;
                    continue;
                }

                _logger.LogWarning("sessions.reset failed for protected main session '{SessionKey}'", key);
                continue;
            }

            var response = await _rpc.RequestAsync("sessions.delete", new
            {
                key,
                deleteTranscript,
            }, ct).ConfigureAwait(false);

            if (response.Ok)
            {
                deleted++;
                continue;
            }

            var error = response.Error?.GetProperty("message").GetString() ?? "unknown error";
            _logger.LogWarning("sessions.delete failed for '{SessionKey}': {Error}", key, error);
        }

        return deleted;
    }

    private static IEnumerable<string> ExtractSessionKeys(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var key in ReadKeysFromArray(payload))
                yield return key;
            yield break;
        }

        if (payload.ValueKind != JsonValueKind.Object)
            yield break;

        if (payload.TryGetProperty("sessions", out var sessions) && sessions.ValueKind == JsonValueKind.Array)
        {
            foreach (var key in ReadKeysFromArray(sessions))
                yield return key;
        }

        if (payload.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var key in ReadKeysFromArray(items))
                yield return key;
        }
    }

    private static IEnumerable<string> ReadKeysFromArray(JsonElement sessions)
    {
        foreach (var session in sessions.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object)
                continue;

            if (session.TryGetProperty("key", out var keyEl))
            {
                var key = keyEl.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                    yield return key;
            }
        }
    }

    private async Task<bool> TryResetSessionAsync(string key, CancellationToken ct)
    {
        // OpenClaw rejects deleting the active/main session; reset it instead.
        var candidates = new object[]
        {
            new { key },
            new { sessionKey = key },
            new { id = key },
        };

        foreach (var parameters in candidates)
        {
            var response = await _rpc.RequestAsync("sessions.reset", parameters, ct).ConfigureAwait(false);
            if (response.Ok)
            {
                _logger.LogInformation("sessions.reset succeeded for protected session '{SessionKey}'", key);
                return true;
            }
        }

        return false;
    }

    private void OnEvent(OpenClawEvent evt)
    {
        if (!string.Equals(evt.EventName, "chat", StringComparison.Ordinal) || !evt.Payload.HasValue)
            return;

        try
        {
            var payload = evt.Payload.Value;
            var stream = payload.TryGetProperty("stream", out var s) ? s.GetString() : null;

            if (!string.Equals(stream, "assistant", StringComparison.Ordinal))
                return;

            var text = payload.TryGetProperty("data", out var data)
                && data.TryGetProperty("text", out var t)
                    ? t.GetString()
                    : null;

            if (string.IsNullOrEmpty(text))
                return;

            var sessionKey = payload.TryGetProperty("sessionKey", out var sk) ? sk.GetString() ?? "" : "";
            var runId = payload.TryGetProperty("runId", out var rid) ? rid.GetString() ?? "" : Guid.NewGuid().ToString();

            var msg = new ChannelMessage
            {
                Id = runId,
                ChannelId = $"openclaw-{_config.InstanceName}",
                SenderId = "openclaw-assistant",
                SenderDisplayName = "OpenClaw",
                Content = text,
                Timestamp = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["session_key"] = sessionKey,
                    ["stream"] = stream ?? "",
                },
            };

            _ = Task.Run(async () =>
            {
                if (MessageReceived is not null)
                    await MessageReceived.Invoke(msg);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing OpenClaw chat event");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        await _rpc.DisposeAsync();
    }
}
