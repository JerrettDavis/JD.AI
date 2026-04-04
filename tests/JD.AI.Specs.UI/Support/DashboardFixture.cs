using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace JD.AI.Specs.UI.Support;

/// <summary>
/// Provides the base URL for the dashboard under test.
/// In a full integration scenario this would wrap a WebApplicationFactory
/// to host the Blazor WASM app. For now it exposes a configurable base URL
/// that defaults to the dashboard and gateway development servers.
/// </summary>
public sealed class DashboardFixture : IDisposable
{
    public string BaseUrl { get; } =
        Environment.GetEnvironmentVariable("DASHBOARD_BASE_URL")
        ?? "http://localhost:5189";

    public string ScreenshotDirectory { get; } =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "ui-specs"));

    public async Task EnsureScenarioPrerequisitesAsync(string[] tags)
    {
        var gatewayBaseUrl = await GetGatewayBaseUrlAsync();

        Skip.IfNot(
            await IsUrlReachableAsync(BaseUrl),
            $"Dashboard is not reachable at {BaseUrl}. Start the dashboard dev server or set DASHBOARD_BASE_URL.");

        if (RequiresTag(tags, "requires-gateway") || RequiresTag(tags, "requires-agents") || RequiresTag(tags, "requires-audit"))
        {
            Skip.IfNot(
                await IsUrlReachableAsync($"{gatewayBaseUrl.TrimEnd('/')}/api/gateway/status"),
                $"Gateway is not reachable at {gatewayBaseUrl}. Start the gateway or set DASHBOARD_GATEWAY_BASE_URL.");
        }

        if (RequiresTag(tags, "requires-agents"))
        {
            Skip.IfNot(
                await HasItemsAsync($"{gatewayBaseUrl.TrimEnd('/')}/api/agents"),
                $"No running agents were found at {gatewayBaseUrl}. Start at least one agent for @requires-agents scenarios.");
        }

        if (RequiresTag(tags, "requires-audit"))
        {
            Skip.IfNot(
                await HasAuditEventsAsync($"{gatewayBaseUrl.TrimEnd('/')}/api/v1/audit/events?limit=1"),
                $"No audit events were found at {gatewayBaseUrl}. Generate gateway activity before running @requires-audit scenarios.");
        }
    }

    private static bool RequiresTag(IEnumerable<string> tags, string requiredTag) =>
        tags.Any(tag => string.Equals(tag, requiredTag, StringComparison.OrdinalIgnoreCase));

    private static async Task<bool> IsUrlReachableAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode
                || response.StatusCode == HttpStatusCode.Redirect
                || response.StatusCode == HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> HasItemsAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var items = await client.GetFromJsonAsync<object[]>(url);
            return items is { Length: > 0 };
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> HasAuditEventsAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            await using var stream = await client.GetStreamAsync(url);
            using var document = await JsonDocument.ParseAsync(stream);
            return document.RootElement.TryGetProperty("events", out var events)
                && events.ValueKind == JsonValueKind.Array
                && events.GetArrayLength() > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetGatewayBaseUrlAsync()
    {
        var dashboardGatewayOverride = Environment.GetEnvironmentVariable("DASHBOARD_GATEWAY_BASE_URL");
        if (!string.IsNullOrWhiteSpace(dashboardGatewayOverride))
            return dashboardGatewayOverride;

        var configuredGatewayBaseUrl = await TryGetConfiguredGatewayBaseUrlAsync();
        if (!string.IsNullOrWhiteSpace(configuredGatewayBaseUrl))
            return configuredGatewayBaseUrl;

        return Environment.GetEnvironmentVariable("GATEWAY_BASE_URL")
            ?? "http://localhost:15790";
    }

    private async Task<string?> TryGetConfiguredGatewayBaseUrlAsync()
    {
        var baseUri = new Uri($"{BaseUrl.TrimEnd('/')}/", UriKind.Absolute);

        foreach (var settingsFile in new[] { "appsettings.Development.json", "appsettings.json" })
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var response = await client.GetAsync(new Uri(baseUri, settingsFile));
                if (!response.IsSuccessStatusCode)
                    continue;

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);

                if (!document.RootElement.TryGetProperty("GatewayUrl", out var gatewayUrlElement)
                    || gatewayUrlElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var gatewayUrl = gatewayUrlElement.GetString();
                return string.IsNullOrWhiteSpace(gatewayUrl)
                    ? BaseUrl.TrimEnd('/')
                    : gatewayUrl;
            }
            catch
            {
                // Fall back to explicit environment variables when dashboard settings are unavailable.
            }
        }

        return null;
    }

    public void Dispose()
    {
        // Cleanup if needed when WebApplicationFactory is wired up
    }
}
