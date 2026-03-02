using JD.AI.Core.Channels;

namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Handles messages for a specific <see cref="OpenClawRoutingMode"/>.
/// Each mode handler decides whether to process a message and how to respond.
/// </summary>
public interface IOpenClawModeHandler
{
    /// <summary>The routing mode this handler supports.</summary>
    OpenClawRoutingMode Mode { get; }

    /// <summary>
    /// Processes an incoming OpenClaw event. Returns true if the message was handled.
    /// </summary>
    /// <param name="evt">The raw OpenClaw event.</param>
    /// <param name="channelName">The OpenClaw channel name (e.g., "discord", "signal").</param>
    /// <param name="routeConfig">The per-channel routing configuration.</param>
    /// <param name="bridge">The OpenClaw bridge for sending responses.</param>
    /// <param name="messageProcessor">Callback that runs user content through the JD.AI agent and returns the response.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> HandleAsync(
        OpenClawEvent evt,
        string channelName,
        OpenClawChannelRouteConfig routeConfig,
        OpenClawBridgeChannel bridge,
        Func<string, string, Task<string?>> messageProcessor,
        CancellationToken ct = default);
}
