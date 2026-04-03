using System.Diagnostics;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Utilities;

/// <summary>
/// Checks whether the gateway daemon is running by probing its health endpoint.
/// Tries 127.0.0.1 first (avoids IPv6 resolution issues on Windows), then localhost.
/// </summary>
internal static class GatewayHealthChecker
{
    private static readonly string[] DefaultCandidates =
    [
        $"http://127.0.0.1:{GatewayRuntimeDefaults.DefaultPort}",
        $"http://localhost:{GatewayRuntimeDefaults.DefaultPort}"
    ];

    public static string DefaultBaseUrl =>
        $"http://127.0.0.1:{GatewayRuntimeDefaults.DefaultPort}";

    public static async Task<bool> IsRunningAsync(string? baseUrl = null, int timeoutMs = 2000)
        => await GetReachableBaseUrlAsync(baseUrl, timeoutMs).ConfigureAwait(false) is not null;

    public static async Task<string?> GetReachableBaseUrlAsync(string? baseUrl = null, int timeoutMs = 2000)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };

        var candidates = GetCandidates(baseUrl);

        foreach (var candidate in candidates)
        {
            try
            {
                using var response = await client
                    .GetAsync(new Uri($"{candidate}{GatewayRuntimeDefaults.HealthPath}"))
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return candidate;
            }
            catch (HttpRequestException)
            {
                // Try next candidate
            }
            catch (TaskCanceledException)
            {
                // Try next candidate
            }
        }

        return null;
    }

    public static async Task<bool> WaitForHealthyAsync(string? baseUrl = null, int maxWaitMs = 10000)
        => await WaitForHealthyBaseUrlAsync(baseUrl, maxWaitMs).ConfigureAwait(false) is not null;

    public static async Task<string?> WaitForHealthyBaseUrlAsync(string? baseUrl = null, int maxWaitMs = 10000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < maxWaitMs)
        {
            var candidate = await GetReachableBaseUrlAsync(baseUrl, 1000).ConfigureAwait(false);
            if (candidate is not null)
                return candidate;
            await Task.Delay(500).ConfigureAwait(false);
        }

        return null;
    }

    private static IReadOnlyList<string> GetCandidates(string? baseUrl)
    {
        if (baseUrl is null)
            return DefaultCandidates;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
            throw new ArgumentException("Base URL must be an absolute URI.", nameof(baseUrl));

        return [baseUrl];
    }
}
