using JD.AI.Core.Config;
using System.Text;

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
        if (settings.MotdTimeoutMs <= 0 || settings.MotdTimeoutMs > 300_000)
            return null;

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(settings.MotdTimeoutMs),
        };

        return await TryGetMotdAsync(settings, http, ct).ConfigureAwait(false);
    }

    internal static async Task<string?> TryGetMotdAsync(
        WelcomePanelSettings settings,
        HttpClient http,
        CancellationToken ct = default)
    {
        var maxLength = Math.Clamp(settings.MotdMaxLength, 4, 1_000);

        if (!settings.ShowMotd || string.IsNullOrWhiteSpace(settings.MotdUrl))
            return null;

        if (!Uri.TryCreate(settings.MotdUrl, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            return null;

        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var raw = await ReadLimitedStringAsync(stream, maxLength + 32, ct).ConfigureAwait(false);
            return NormalizeMotd(raw, maxLength);
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

    private static async Task<string> ReadLimitedStringAsync(Stream stream, int maxChars, CancellationToken ct)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 256,
            leaveOpen: true);
        var buffer = new char[Math.Min(256, maxChars)];
        var text = new StringBuilder(maxChars);

        while (text.Length < maxChars)
        {
            var charsToRead = Math.Min(buffer.Length, maxChars - text.Length);
            var read = await reader.ReadAsync(buffer.AsMemory(0, charsToRead), ct).ConfigureAwait(false);
            if (read == 0)
                break;

            text.Append(buffer, 0, read);
        }

        return text.ToString();
    }

    internal static string? NormalizeMotd(string? raw, int maxLength)
    {
        if (maxLength < 4 || string.IsNullOrWhiteSpace(raw))
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
