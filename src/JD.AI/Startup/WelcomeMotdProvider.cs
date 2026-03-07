using JD.AI.Core.Config;

namespace JD.AI.Startup;

/// <summary>
/// Fetches and formats an optional message of the day for the welcome panel.
/// </summary>
internal static class WelcomeMotdProvider
{
    public static async Task<string?> TryGetMotdAsync(
        WelcomePanelSettings settings,
        CancellationToken ct = default)
    {
        if (!settings.ShowMotd || string.IsNullOrWhiteSpace(settings.MotdUrl))
            return null;

        if (!Uri.TryCreate(settings.MotdUrl, UriKind.Absolute, out var uri))
            return null;

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(settings.MotdTimeoutMs),
        };

        try
        {
            var raw = await http.GetStringAsync(uri, ct).ConfigureAwait(false);
            return NormalizeMotd(raw, settings.MotdMaxLength);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    internal static string? NormalizeMotd(string? raw, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var line = raw
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

        if (string.IsNullOrWhiteSpace(line))
            return null;

        var normalized = line.Replace('\t', ' ').Trim();
        if (normalized.Length <= maxLength)
            return normalized;

        return $"{normalized[..(maxLength - 3)].TrimEnd()}...";
    }
}
