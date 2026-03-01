using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.OpenClaw.Routing;

/// <summary>
/// Sidecar mode: both JD.AI and OpenClaw process messages.
/// JD.AI only responds when the message matches a trigger (command prefix, regex, or @mention).
/// OpenClaw handles everything else normally.
/// </summary>
public sealed class SidecarModeHandler(ILogger<SidecarModeHandler> logger) : IOpenClawModeHandler
{
    public OpenClawRoutingMode Mode => OpenClawRoutingMode.Sidecar;

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

        // Check if the message matches any trigger
        var (triggered, strippedContent) = CheckTrigger(content!, routeConfig);
        if (!triggered)
        {
            logger.LogDebug(
                "[Sidecar] Message on '{Channel}' did not match trigger, skipping",
                channelName);
            return false;
        }

        logger.LogInformation(
            "[Sidecar] Triggered on '{Channel}' session='{Session}'",
            channelName, sessionKey);

        // Abort OpenClaw's agent for this message so it doesn't also respond
        if (bridge?.IsConnected == true)
        {
            try
            {
                await bridge.AbortSessionAsync(sessionKey, ct);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to abort OpenClaw agent (may not be running)");
            }
        }

        var response = await messageProcessor(sessionKey, strippedContent);
        if (string.IsNullOrEmpty(response))
        {
            logger.LogWarning("[Sidecar] JD.AI returned empty response for '{Session}'", sessionKey);
            return true;
        }

        if (bridge?.IsConnected == true)
            await bridge.InjectMessageAsync(sessionKey, response, ct);

        logger.LogInformation("[Sidecar] Sent JD.AI response to '{Channel}'", channelName);

        return true;
    }

    /// <summary>
    /// Checks if the message matches the configured trigger.
    /// Returns (matched, content-with-prefix-stripped).
    /// </summary>
    private static (bool Matched, string Content) CheckTrigger(string content, OpenClawChannelRouteConfig config)
    {
        // Check command prefix (e.g., "/jdai hello" → "hello")
        if (!string.IsNullOrEmpty(config.CommandPrefix))
        {
            if (content.StartsWith(config.CommandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = content[config.CommandPrefix.Length..].TrimStart();
                return (true, stripped);
            }
        }

        // Check regex pattern
        if (!string.IsNullOrEmpty(config.TriggerPattern))
        {
            var match = Regex.IsMatch(content, config.TriggerPattern, RegexOptions.IgnoreCase);
            if (match)
                return (true, content);
        }

        return (false, content);
    }

    private static bool TryExtractUserMessage(OpenClawEvent evt, out string sessionKey, out string? content)
    {
        sessionKey = "";
        content = null;

        if (!string.Equals(evt.EventName, "chat", StringComparison.Ordinal) || !evt.Payload.HasValue)
            return false;

        var payload = evt.Payload.Value;
        var stream = payload.TryGetProperty("stream", out var s) ? s.GetString() : null;
        if (!string.Equals(stream, "user", StringComparison.Ordinal))
            return false;

        content = payload.TryGetProperty("data", out var data)
            && data.TryGetProperty("text", out var t)
                ? t.GetString()
                : null;

        sessionKey = payload.TryGetProperty("sessionKey", out var sk) ? sk.GetString() ?? "" : "";

        return !string.IsNullOrEmpty(content);
    }
}
