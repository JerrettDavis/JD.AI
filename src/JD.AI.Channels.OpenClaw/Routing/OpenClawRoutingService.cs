using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Hosted service that connects the OpenClaw bridge and routes incoming messages
/// to JD.AI agents based on per-channel routing configuration.
///
/// OpenClaw broadcasts two event types to WebSocket clients:
/// <list type="bullet">
///   <item><c>"agent"</c> — lifecycle, assistant, tool events (has <c>stream</c> and <c>sessionKey</c>)</item>
///   <item><c>"chat"</c> — chat deltas and finals (has <c>state</c>, <c>sessionKey</c>)</item>
/// </list>
/// User input is NOT broadcast as a WebSocket event. To detect user messages, we watch
/// for agent lifecycle <c>"start"</c> events and query <c>chat.history</c> for the session.
/// </summary>
public sealed class OpenClawRoutingService : BackgroundService
{
    private readonly OpenClawBridgeChannel _bridge;
    private readonly OpenClawRoutingConfig _routingConfig;
    private readonly Dictionary<OpenClawRoutingMode, IOpenClawModeHandler> _handlers;
    private readonly Func<string, string, Task<string?>> _messageProcessor;
    private readonly ILogger<OpenClawRoutingService> _logger;

    /// <summary>
    /// Maps session keys to their channel name for routing. Populated from OpenClaw session info.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _sessionChannelCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Recently processed run IDs to prevent duplicate handling.</summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _processedRuns = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores recent events for diagnostic inspection.</summary>
    private readonly ConcurrentQueue<(DateTimeOffset Time, string EventName, string Summary)> _recentEvents = new();

    public OpenClawRoutingService(
        OpenClawBridgeChannel bridge,
        IOptions<OpenClawRoutingConfig> routingConfig,
        IEnumerable<IOpenClawModeHandler> handlers,
        Func<string, string, Task<string?>> messageProcessor,
        ILogger<OpenClawRoutingService> logger)
    {
        _bridge = bridge;
        _routingConfig = routingConfig.Value;
        _handlers = handlers.ToDictionary(h => h.Mode);
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    /// <summary>Gets recent events for diagnostic inspection.</summary>
    public IReadOnlyList<(DateTimeOffset Time, string EventName, string Summary)> GetRecentEvents()
    {
        return _recentEvents.ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_routingConfig.AutoConnect)
        {
            _logger.LogInformation("OpenClaw routing auto-connect disabled");
            return;
        }

        _logger.LogInformation("OpenClaw routing service starting");

        // Subscribe to raw RPC events for routing dispatch
        _bridge.RpcClient.EventReceived += evt => OnOpenClawEvent(evt, stoppingToken);

        try
        {
            await _bridge.ConnectAsync(stoppingToken);
            _logger.LogInformation(
                "OpenClaw routing active. Default mode: {Mode}. Configured channels: {Count}",
                _routingConfig.DefaultMode,
                _routingConfig.Channels.Count);

            // Log per-channel configuration
            foreach (var (name, config) in _routingConfig.Channels)
            {
                _logger.LogInformation(
                    "  Channel '{Channel}': mode={Mode}, prefix={Prefix}, profile={Profile}",
                    name, config.Mode, config.CommandPrefix ?? "(none)", config.AgentProfile);
            }

            // Periodically clean up stale entries from caches
            using var cleanupTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await cleanupTimer.WaitForNextTickAsync(stoppingToken))
            {
                CleanupCaches();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("OpenClaw routing service shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenClaw routing service failed");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _bridge.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting OpenClaw bridge during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }

    private void OnOpenClawEvent(OpenClawEvent evt, CancellationToken ct)
    {
        // Track event for diagnostics
        TrackEvent(evt);

        // Fire-and-forget but with error handling
        _ = Task.Run(async () =>
        {
            try
            {
                await DispatchEventAsync(evt, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching OpenClaw event '{Event}'", evt.EventName);
            }
        }, ct);
    }

    private async Task DispatchEventAsync(OpenClawEvent evt, CancellationToken ct)
    {
        if (!evt.Payload.HasValue)
            return;

        // Handle both "agent" and "chat" events
        if (string.Equals(evt.EventName, "agent", StringComparison.Ordinal))
        {
            await HandleAgentEventAsync(evt, ct);
        }
        else if (string.Equals(evt.EventName, "chat", StringComparison.Ordinal))
        {
            await HandleChatEventAsync(evt, ct);
        }
        else
        {
            _logger.LogDebug("Ignoring event type '{EventName}'", evt.EventName);
        }
    }

    /// <summary>
    /// Handles "agent" events — the primary event type for detecting new user messages.
    /// When a new agent run starts (lifecycle.start), we query the session for the latest
    /// user message and route it through our mode handlers.
    /// </summary>
    private async Task HandleAgentEventAsync(OpenClawEvent evt, CancellationToken ct)
    {
        var payload = evt.Payload!.Value;

        var stream = payload.TryGetProperty("stream", out var s) ? s.GetString() : null;
        var sessionKey = payload.TryGetProperty("sessionKey", out var sk) ? sk.GetString() ?? "" : "";
        var runId = payload.TryGetProperty("runId", out var rid) ? rid.GetString() ?? "" : "";

        _logger.LogDebug(
            "[Agent] stream={Stream} session={Session} runId={RunId}",
            stream, sessionKey, runId);

        // We're interested in lifecycle "start" events — these indicate a new agent run
        if (!string.Equals(stream, "lifecycle", StringComparison.Ordinal))
            return;

        var phase = payload.TryGetProperty("data", out var data)
            && data.TryGetProperty("phase", out var p)
                ? p.GetString()
                : null;

        if (!string.Equals(phase, "start", StringComparison.Ordinal))
            return;

        // Prevent duplicate handling (same run ID)
        if (!_processedRuns.TryAdd(runId, DateTimeOffset.UtcNow))
        {
            _logger.LogDebug("[Agent] Already processed run '{RunId}'", runId);
            return;
        }

        _logger.LogInformation(
            "[Agent] New run started: session={Session} runId={RunId}",
            sessionKey, runId);

        // Query chat.history to get the latest user message
        var userMessage = await QueryLatestUserMessageAsync(sessionKey, ct);
        if (string.IsNullOrEmpty(userMessage))
        {
            _logger.LogDebug("[Agent] No user message found for session '{Session}'", sessionKey);
            return;
        }

        _logger.LogInformation(
            "[Agent] User message: '{Preview}' session={Session}",
            userMessage.Length > 80 ? userMessage[..80] + "..." : userMessage,
            sessionKey);

        // Determine channel from session key or cache
        var channelName = ResolveChannelName(sessionKey);

        // Create a synthetic event with the user message for the mode handlers
        var syntheticPayload = JsonSerializer.SerializeToElement(new
        {
            sessionKey,
            runId,
            stream = "user",
            data = new { text = userMessage },
        });

        var syntheticEvent = new OpenClawEvent
        {
            EventName = "agent",
            Payload = syntheticPayload,
        };

        await RouteToHandlerAsync(syntheticEvent, channelName, ct);
    }

    /// <summary>
    /// Handles "chat" events — deltas and finals from agent output.
    /// These are secondary; we primarily use them for logging and diagnostics.
    /// </summary>
    private Task HandleChatEventAsync(OpenClawEvent evt, CancellationToken ct)
    {
        var payload = evt.Payload!.Value;
        var state = payload.TryGetProperty("state", out var st) ? st.GetString() : null;
        var sessionKey = payload.TryGetProperty("sessionKey", out var sk) ? sk.GetString() ?? "" : "";

        _logger.LogDebug("[Chat] state={State} session={Session}", state, sessionKey);

        // Chat events are for assistant output; we don't route them to handlers
        // They're useful for tracking active sessions and diagnostics
        return Task.CompletedTask;
    }

    private async Task RouteToHandlerAsync(OpenClawEvent evt, string channelName, CancellationToken ct)
    {
        // Look up per-channel config or fall back to default
        var routeConfig = _routingConfig.Channels.TryGetValue(channelName, out var cfg)
            ? cfg
            : new OpenClawChannelRouteConfig { Mode = _routingConfig.DefaultMode };

        _logger.LogInformation(
            "[Route] channel={Channel} mode={Mode}",
            channelName, routeConfig.Mode);

        // Get the appropriate mode handler
        if (!_handlers.TryGetValue(routeConfig.Mode, out var handler))
        {
            _logger.LogWarning("No handler for routing mode '{Mode}'", routeConfig.Mode);
            return;
        }

        await handler.HandleAsync(evt, channelName, routeConfig, _bridge, _messageProcessor, ct);
    }

    /// <summary>
    /// Queries the latest user message from an OpenClaw session via <c>chat.history</c>.
    /// </summary>
    private async Task<string?> QueryLatestUserMessageAsync(string sessionKey, CancellationToken ct)
    {
        try
        {
            var response = await _bridge.RpcAsync("chat.history", new { sessionKey, limit = 5 }, ct);
            if (!response.Ok || !response.Payload.HasValue)
            {
                _logger.LogDebug("chat.history failed for session '{Session}'", sessionKey);
                return null;
            }

            // Search for the last user message in history
            var payloadRoot = response.Payload.Value;

            // Try payload.messages array
            if (payloadRoot.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                // Iterate backwards to find the last user message
                for (var i = messages.GetArrayLength() - 1; i >= 0; i--)
                {
                    var msg = messages[i];
                    var role = msg.TryGetProperty("role", out var r) ? r.GetString() : null;
                    if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract text content
                        if (msg.TryGetProperty("content", out var content))
                        {
                            if (content.ValueKind == JsonValueKind.String)
                                return content.GetString();

                            // Content may be an array of content parts
                            if (content.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var part in content.EnumerateArray())
                                {
                                    if (part.TryGetProperty("type", out var type)
                                        && string.Equals(type.GetString(), "text", StringComparison.Ordinal)
                                        && part.TryGetProperty("text", out var text))
                                    {
                                        return text.GetString();
                                    }
                                }
                            }
                        }

                        // Try message.text directly
                        if (msg.TryGetProperty("text", out var directText))
                            return directText.GetString();
                    }
                }
            }

            // Try payload.history array (alternative format)
            if (payloadRoot.TryGetProperty("history", out var history) && history.ValueKind == JsonValueKind.Array)
            {
                for (var i = history.GetArrayLength() - 1; i >= 0; i--)
                {
                    var entry = history[i];
                    var role = entry.TryGetProperty("role", out var r) ? r.GetString() : null;
                    if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
                    {
                        if (entry.TryGetProperty("text", out var text))
                            return text.GetString();
                        if (entry.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                            return c.GetString();
                    }
                }
            }

            _logger.LogDebug("No user message found in chat.history for '{Session}'", sessionKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query chat.history for session '{Session}'", sessionKey);
            return null;
        }
    }

    /// <summary>
    /// Resolves which OpenClaw channel a session belongs to.
    /// Uses cached mappings and session key heuristics.
    /// </summary>
    private string ResolveChannelName(string sessionKey)
    {
        // Check cache first
        if (_sessionChannelCache.TryGetValue(sessionKey, out var cached))
            return cached;

        // Session keys are typically "agent:{agentId}:{suffix}"
        // The suffix may contain channel hints (e.g., "discord-123456", "signal-+1234")
        var parts = sessionKey.Split(':', 3);
        if (parts.Length >= 3)
        {
            var suffix = parts[2].ToLowerInvariant();
            foreach (var channelName in _routingConfig.Channels.Keys)
            {
                if (suffix.Contains(channelName, StringComparison.OrdinalIgnoreCase))
                {
                    _sessionChannelCache[sessionKey] = channelName;
                    _logger.LogDebug("Resolved session '{Session}' → channel '{Channel}' via suffix", sessionKey, channelName);
                    return channelName;
                }
            }
        }

        // For "agent:main:main" and similar generic sessions, check all configured channels
        // If there's only one non-Passthrough channel, route there as a reasonable default
        var activeChannels = _routingConfig.Channels
            .Where(c => c.Value.Mode != OpenClawRoutingMode.Passthrough)
            .ToList();

        if (activeChannels.Count == 1)
        {
            var singleChannel = activeChannels[0].Key;
            _sessionChannelCache[sessionKey] = singleChannel;
            _logger.LogDebug(
                "Resolved session '{Session}' → channel '{Channel}' (only active channel)",
                sessionKey, singleChannel);
            return singleChannel;
        }

        // Fall back to matching against all active channels using a "try all" approach
        // Since we can't determine the exact channel, route through all non-Passthrough handlers
        _logger.LogDebug(
            "Cannot determine channel for session '{Session}', using default routing",
            sessionKey);
        return "__unknown";
    }

    private void TrackEvent(OpenClawEvent evt)
    {
        var summary = evt.Payload.HasValue
            ? TruncateJson(evt.Payload.Value.GetRawText(), 200)
            : "(no payload)";

        _recentEvents.Enqueue((DateTimeOffset.UtcNow, evt.EventName, summary));

        // Keep only last 100 events
        while (_recentEvents.Count > 100)
            _recentEvents.TryDequeue(out _);
    }

    private void CleanupCaches()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);
        foreach (var (key, time) in _processedRuns)
        {
            if (time < cutoff)
                _processedRuns.TryRemove(key, out _);
        }
    }

    private static string TruncateJson(string json, int maxLength) =>
        json.Length <= maxLength ? json : json[..maxLength] + "...";
}
