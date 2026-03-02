using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Passthrough mode: observes events for logging/analytics but never responds.
/// </summary>
public sealed class PassthroughModeHandler(ILogger<PassthroughModeHandler> logger) : IOpenClawModeHandler
{
    public OpenClawRoutingMode Mode => OpenClawRoutingMode.Passthrough;

    public Task<bool> HandleAsync(
        OpenClawEvent evt,
        string channelName,
        OpenClawChannelRouteConfig routeConfig,
        OpenClawBridgeChannel bridge,
        Func<string, string, Task<string?>> messageProcessor,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "[Passthrough] Observed event '{Event}' on channel '{Channel}'",
            evt.EventName, channelName);
        return Task.FromResult(false);
    }
}
