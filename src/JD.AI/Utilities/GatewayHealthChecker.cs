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
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };

        var candidates = baseUrl != null ? [baseUrl] : DefaultCandidates;

        foreach (var candidate in candidates)
        {
            try
            {
                var response = await client
                    .GetAsync(new Uri($"{candidate}{GatewayRuntimeDefaults.HealthPath}"))
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return true;
            }
#pragma warning disable CA1031
            catch
            {
                // Try next candidate
            }
#pragma warning restore CA1031
        }

        return false;
    }

    public static async Task<bool> WaitForHealthyAsync(string? baseUrl = null, int maxWaitMs = 10000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < maxWaitMs)
        {
            if (await IsRunningAsync(baseUrl, 1000).ConfigureAwait(false))
                return true;
            await Task.Delay(500).ConfigureAwait(false);
        }

        return false;
    }
}
