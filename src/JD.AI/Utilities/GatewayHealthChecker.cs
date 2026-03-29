using System.Diagnostics;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Utilities;

/// <summary>
/// Checks whether the gateway daemon is running by probing its health endpoint.
/// </summary>
internal static class GatewayHealthChecker
{
    public static string DefaultBaseUrl =>
        $"http://{GatewayRuntimeDefaults.DefaultHost}:{GatewayRuntimeDefaults.DefaultPort}";

    public static async Task<bool> IsRunningAsync(string? baseUrl = null, int timeoutMs = 2000)
    {
        baseUrl ??= DefaultBaseUrl;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            var response = await client.GetAsync(new Uri($"{baseUrl}{GatewayRuntimeDefaults.HealthPath}"))
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
#pragma warning disable CA1031
        catch
        {
            return false;
        }
#pragma warning restore CA1031
    }

    public static async Task<bool> WaitForHealthyAsync(string? baseUrl = null, int maxWaitMs = 10000)
    {
        baseUrl ??= DefaultBaseUrl;
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
