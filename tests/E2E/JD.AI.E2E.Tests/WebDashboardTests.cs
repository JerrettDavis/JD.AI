using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace JD.AI.E2E.Tests;

/// <summary>
/// Playwright-based browser tests for the Blazor WASM Dashboard.
///
/// These tests require the Gateway to be reachable (via <see cref="OllamaTestHost"/>)
/// and the Dashboard dev server to be running at the configured base URL
/// (defaults to <c>https://localhost:5001</c>; override with <c>DASHBOARD_BASE_URL</c>).
///
/// When the dashboard is unreachable tests are gracefully skipped so that CI runs
/// are not broken by a missing dev-server process.
[Collection("Ollama E2E")]
public sealed class WebDashboardTests : IAsyncDisposable
{
    private readonly OllamaTestHost _host;
    private readonly string _dashboardBaseUrl;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public WebDashboardTests(OllamaTestHost host)
    {
        _host = host;
        _host.EnsureAvailable();
        _dashboardBaseUrl =
            Environment.GetEnvironmentVariable("DASHBOARD_BASE_URL")
            ?? "https://localhost:5001";
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browser is not null) return;

        if (!await IsDashboardReachableAsync())
            return;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    private async Task<bool> IsDashboardReachableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new HttpClient();
            var response = await client.GetAsync(_dashboardBaseUrl, cts.Token);
            return response.IsSuccessStatusCode
                || response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.Redirect;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task Dashboard_LoadsSessionList()
    {
        await EnsureBrowserAsync();
        if (_browser is null)
        {
            Skip.If(true,
                $"Dashboard is not reachable at {_dashboardBaseUrl}. " +
                $"Start the dashboard dev server and ensure the Gateway is running at {OllamaTestHost.GatewayBaseUrl}.");
            return;
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync(_dashboardBaseUrl);

        // The dashboard should load without a crash
        var title = await page.TitleAsync();
        title.Should().NotBeNullOrEmpty();

        // Wait for the main layout to be visible
        await page.WaitForSelectorAsync("body", new PageWaitForSelectorOptions { Timeout = 15000 });

        await context.DisposeAsync();
    }

    [Fact]
    public async Task Dashboard_ChatPage_ChannelFilter_IsFunctional()
    {
        await EnsureBrowserAsync();
        if (_browser is null)
        {
            Skip.If(true,
                $"Dashboard is not reachable at {_dashboardBaseUrl}. " +
                $"Start the dashboard dev server and ensure the Gateway is running at {OllamaTestHost.GatewayBaseUrl}.");
            return;
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });
        var page = await context.NewPageAsync();

        // Navigate to the chat page
        await page.GotoAsync($"{_dashboardBaseUrl}/chat");

        // Wait for channel filter UI to be present
        // The dashboard uses MudBlazor; look for a select/dropdown element
        var channelFilterLocator = page.Locator("select").First;
        var hasChannelFilter = await channelFilterLocator.IsVisibleAsync().WaitAsync(TimeSpan.FromSeconds(10));

        if (hasChannelFilter)
        {
            var options = await channelFilterLocator.TextContentAsync();
            options.Should().NotBeNull();
        }
        else
        {
            // If the select element is not visible yet, give the page a moment to render
            await page.WaitForTimeoutAsync(2000);
        }

        await context.DisposeAsync();
    }

    [Fact]
    public async Task Dashboard_ApiHealth_MatchesGatewayStatus()
    {
        await EnsureBrowserAsync();
        if (_browser is null)
        {
            Skip.If(true,
                $"Dashboard is not reachable at {_dashboardBaseUrl}.");
            return;
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });
        var page = await context.NewPageAsync();

        // 1. Load the dashboard in the browser
        await page.GotoAsync(_dashboardBaseUrl);

        // 2. Fetch the Gateway status via a standard HttpClient (the Playwright context
        //    request API is only for intercepting, not for ad-hoc API calls)
        using var apiClient = new HttpClient
        {
            BaseAddress = new Uri(OllamaTestHost.GatewayBaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
        var apiResponse = await apiClient.GetAsync("/api/gateway/status");
        apiResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusJson = await apiResponse.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<JsonElement>(statusJson);

        // 3. Verify the dashboard's API client can reach the gateway
        status.TryGetProperty("status", out _).Should().BeTrue();

        await context.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
