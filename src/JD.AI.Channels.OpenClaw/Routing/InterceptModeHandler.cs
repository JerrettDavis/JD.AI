using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Intercept mode: suppresses OpenClaw's agent and routes the message through JD.AI instead.
/// On receiving a user message, calls <c>chat.abort</c> to stop OpenClaw's processing,
/// runs the message through JD.AI (Semantic Kernel → Ollama/provider), then injects
/// the response back via <c>chat.inject</c> (without re-triggering OpenClaw's agent).
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
            "[Intercept] Handling user message on '{Channel}' session='{Session}': {Preview}",
            channelName, sessionKey, content![..Math.Min(80, content.Length)]);

        // Step 1: Abort OpenClaw's agent to prevent duplicate responses
        if (bridge?.IsConnected == true)
        {
            try
            {
                await bridge.AbortSessionAsync(sessionKey, ct);
                logger.LogDebug("Aborted OpenClaw agent for session '{Session}'", sessionKey);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to abort OpenClaw agent for '{Session}'", sessionKey);
            }
        }

        // Step 2: Route through JD.AI (Semantic Kernel → Ollama)
        var response = await messageProcessor(sessionKey, content!);
        if (string.IsNullOrEmpty(response))
        {
            logger.LogWarning("[Intercept] JD.AI returned empty response for '{Session}'", sessionKey);
            return true;
        }

        // Step 3: Inject the JD.AI response back into the OpenClaw session
        // Uses chat.inject (not chat.send) to avoid re-triggering the agent loop
        if (bridge?.IsConnected == true)
            await bridge.InjectMessageAsync(sessionKey, response, ct);

        logger.LogInformation(
            "[Intercept] Sent JD.AI response ({Length} chars) to '{Channel}' session='{Session}'",
            response.Length, channelName, sessionKey);

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
