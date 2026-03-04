using Microsoft.Playwright;
using Reqnroll;

namespace JD.AI.Specs.UI.Support;

/// <summary>
/// Reqnroll hooks that manage Playwright browser lifecycle.
/// A single browser instance is shared per test run (FeatureContext),
/// and each scenario gets its own BrowserContext and Page.
/// </summary>
[Binding]
public sealed class PlaywrightHooks
{
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [BeforeScenario("@ui")]
    public async Task BeforeScenario(ScenarioContext context)
    {
        var browserContext = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });
        var page = await browserContext.NewPageAsync();

        context.Set(browserContext);
        context.Set(page);
        context.Set(new DashboardFixture());
    }

    [AfterScenario("@ui")]
    public async Task AfterScenario(ScenarioContext context)
    {
        if (context.TryGetValue<IBrowserContext>(out var browserContext))
            await browserContext.DisposeAsync();
    }
}
