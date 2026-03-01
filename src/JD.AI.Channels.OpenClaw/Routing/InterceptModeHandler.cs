using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Intercept mode: suppresses OpenClaw's agent and routes the message through JD.AI instead.
/// On receiving a user message, calls chat.abort to stop OpenClaw's processing,
/// runs the message through JD.AI, then sends the response back via OpenClaw.
/// </summary>
public sealed class InterceptModeHandler(ILogger<InterceptModeHandler> logger) : IOpenClawModeHandler
{
    public OpenClawRoutingMode Mode => OpenClawRoutingMode.Intercept;

    public async Task<bool> HandleAsync(
        OpenClawEvent evt,
        string channelName,
        OpenClawChannelRouteConfig routeConfig,
        OpenClawBridgeChannel bridge,
        Func<string, string, Task<string?>> messageProcessor,
        CancellationToken ct = default)
    {
        if (!TryExtractUserMessage(evt, out var sessionKey, out var content))
            return false;

        logger.LogInformation(
            "[Intercept] Handling user message on '{Channel}' session='{Session}'",
            channelName, sessionKey);

        // Abort OpenClaw's agent to prevent duplicate responses
        if (bridge?.IsConnected == true)
        {
            try
            {
                await bridge.RpcAsync("chat.abort", new { session = sessionKey }, ct);
                logger.LogDebug("Aborted OpenClaw agent for session '{Session}'", sessionKey);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to abort OpenClaw agent for '{Session}'", sessionKey);
            }
        }

        // Route through JD.AI
        var response = await messageProcessor(sessionKey, content!);
        if (string.IsNullOrEmpty(response))
        {
            logger.LogWarning("[Intercept] JD.AI returned empty response for '{Session}'", sessionKey);
            return true;
        }

        // Send response back through OpenClaw
        if (bridge?.IsConnected == true)
            await bridge.SendMessageAsync(sessionKey, response, ct);

        logger.LogInformation("[Intercept] Sent JD.AI response to '{Channel}' session='{Session}'", channelName, sessionKey);

        return true;
    }

    private static bool TryExtractUserMessage(OpenClawEvent evt, out string sessionKey, out string? content)
    {
        sessionKey = "";
        content = null;

        if (evt.EventName != "chat" || !evt.Payload.HasValue)
            return false;

        var payload = evt.Payload.Value;
        var stream = payload.TryGetProperty("stream", out var s) ? s.GetString() : null;
        if (stream != "user")
            return false;

        content = payload.TryGetProperty("data", out var data)
            && data.TryGetProperty("text", out var t)
                ? t.GetString()
                : null;

        sessionKey = payload.TryGetProperty("sessionKey", out var sk) ? sk.GetString() ?? "" : "";

        return !string.IsNullOrEmpty(content);
    }
}
