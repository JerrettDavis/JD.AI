using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Proxy mode: JD.AI is the sole agent backend for a dedicated OpenClaw session.
/// OpenClaw acts purely as transport. No need to abort — there's no competing agent.
/// </summary>
public sealed class ProxyModeHandler(ILogger<ProxyModeHandler> logger) : IOpenClawModeHandler
{
    public OpenClawRoutingMode Mode => OpenClawRoutingMode.Proxy;

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
            "[Proxy] Processing message on '{Channel}' session='{Session}'",
            channelName, sessionKey);

        var response = await messageProcessor(sessionKey, content!);
        if (string.IsNullOrEmpty(response))
        {
            logger.LogWarning("[Proxy] JD.AI returned empty response for '{Session}'", sessionKey);
            return true;
        }

        if (bridge?.IsConnected == true)
            await bridge.SendMessageAsync(sessionKey, response, ct);

        logger.LogInformation("[Proxy] Sent JD.AI response to '{Channel}' session='{Session}'", channelName, sessionKey);

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
