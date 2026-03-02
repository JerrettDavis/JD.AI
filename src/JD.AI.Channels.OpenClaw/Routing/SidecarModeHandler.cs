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
    /// Handles Discord mentions (e.g., &lt;@123456&gt;) that may precede the command prefix.
    /// </summary>
    private static (bool Matched, string Content) CheckTrigger(string content, OpenClawChannelRouteConfig config)
    {
        // Strip leading Discord mentions (e.g., "<@1234567890>" or "<@!1234567890>")
        var cleaned = StripDiscordMentions(content);

        // Check command prefix (e.g., "/jdai hello" → "hello")
        if (!string.IsNullOrEmpty(config.CommandPrefix))
        {
            if (cleaned.StartsWith(config.CommandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = cleaned[config.CommandPrefix.Length..].TrimStart();
                return (true, stripped);
            }
        }

        // Check regex pattern (against both raw and cleaned content)
        if (!string.IsNullOrEmpty(config.TriggerPattern))
        {
            var match = Regex.IsMatch(cleaned, config.TriggerPattern, RegexOptions.IgnoreCase);
            if (match)
                return (true, cleaned);
        }

        return (false, content);
    }

    /// <summary>
    /// Strips Discord mention tags (&lt;@123456&gt; or &lt;@!123456&gt;) from the beginning of a message.
    /// </summary>
    private static string StripDiscordMentions(string content)
    {
        return Regex.Replace(content, @"^(\s*<@!?\d+>\s*)+", "", RegexOptions.None).TrimStart();
    }

    /// <summary>
    /// Extracts user message from an agent lifecycle event (synthetic or real).
    /// Works with events that have stream="user" and data.text.
    /// Also works with the legacy format for backward compatibility.
    /// </summary>
    private static bool TryExtractUserMessage(OpenClawEvent evt, out string sessionKey, out string? content)
    {
        sessionKey = "";
        content = null;

        if (!evt.Payload.HasValue)
            return false;

        var payload = evt.Payload.Value;

        // Extract session key
        sessionKey = payload.TryGetProperty("sessionKey", out var sk) ? sk.GetString() ?? "" : "";

        // Try stream=user, data.text (synthetic events from routing service)
        var stream = payload.TryGetProperty("stream", out var s) ? s.GetString() : null;
        if (string.Equals(stream, "user", StringComparison.Ordinal))
        {
            content = payload.TryGetProperty("data", out var data)
                && data.TryGetProperty("text", out var t)
                    ? t.GetString()
                    : null;

            return !string.IsNullOrEmpty(content);
        }

        // Try direct "content" or "message" field (alternative formats)
        if (payload.TryGetProperty("content", out var directContent) && directContent.ValueKind == JsonValueKind.String)
        {
            content = directContent.GetString();
            return !string.IsNullOrEmpty(content);
        }

        return false;
    }
}
