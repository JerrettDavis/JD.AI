using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Hosted service that connects the OpenClaw bridge and routes incoming messages
/// to JD.AI agents based on per-channel routing configuration.
/// </summary>
public sealed class OpenClawRoutingService : BackgroundService
{
    private readonly OpenClawBridgeChannel _bridge;
    private readonly OpenClawRoutingConfig _routingConfig;
    private readonly Dictionary<OpenClawRoutingMode, IOpenClawModeHandler> _handlers;
    private readonly Func<string, string, Task<string?>> _messageProcessor;
    private readonly ILogger<OpenClawRoutingService> _logger;

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
                    "  Channel '{Channel}': mode={Mode}, profile={Profile}",
                    name, config.Mode, config.AgentProfile);
            }

            // Keep alive until shutdown
            await Task.Delay(Timeout.Infinite, stoppingToken);
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
        if (!string.Equals(evt.EventName, "chat", StringComparison.Ordinal) || !evt.Payload.HasValue)
            return;

        // Determine which OpenClaw channel this event came from
        var channelName = ExtractChannelName(evt);
        if (string.IsNullOrEmpty(channelName))
        {
            _logger.LogDebug("Could not determine channel for event, using default mode");
            channelName = "__unknown";
        }

        // Look up per-channel config or fall back to default
        var routeConfig = _routingConfig.Channels.TryGetValue(channelName, out var cfg)
            ? cfg
            : new OpenClawChannelRouteConfig { Mode = _routingConfig.DefaultMode };

        // Get the appropriate mode handler
        if (!_handlers.TryGetValue(routeConfig.Mode, out var handler))
        {
            _logger.LogWarning("No handler for routing mode '{Mode}'", routeConfig.Mode);
            return;
        }

        await handler.HandleAsync(evt, channelName, routeConfig, _bridge, _messageProcessor, ct);
    }

    /// <summary>
    /// Extracts the source channel name from an OpenClaw chat event.
    /// OpenClaw includes channel info in the event metadata.
    /// </summary>
    private static string ExtractChannelName(OpenClawEvent evt)
    {
        if (!evt.Payload.HasValue)
            return "";

        var payload = evt.Payload.Value;

        // Try "channel" field first
        if (payload.TryGetProperty("channel", out var ch) && ch.ValueKind == JsonValueKind.String)
            return ch.GetString() ?? "";

        // Try "source" field
        if (payload.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.String)
            return src.GetString() ?? "";

        // Try nested metadata
        if (payload.TryGetProperty("metadata", out var meta)
            && meta.TryGetProperty("channel", out var metaCh)
            && metaCh.ValueKind == JsonValueKind.String)
            return metaCh.GetString() ?? "";

        return "";
    }
}
