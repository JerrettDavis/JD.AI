using Microsoft.Playwright;
using Reqnroll;
using Xunit;

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
    private static readonly SemaphoreSlim BrowserLock = new(1, 1);

    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [BeforeScenario("@ui", Order = 0)]
    public async Task BeforeScenario(ScenarioContext context)
    {
        var fixture = new DashboardFixture();
        context.Set(fixture);

        await fixture.EnsureScenarioPrerequisitesAsync(context.ScenarioInfo.Tags);

        var browser = await EnsureBrowserAsync();
        var browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });
        var page = await browserContext.NewPageAsync();

        context.Set(browserContext);
        context.Set(page);
    }

    [AfterScenario("@ui")]
    public async Task AfterScenario(ScenarioContext context)
    {
        if (context.TryGetValue<IPage>(out var page) &&
            context.TryGetValue<DashboardFixture>(out var fixture))
        {
            Directory.CreateDirectory(fixture.ScreenshotDirectory);
            var scenarioName = SanitizeFileName(context.ScenarioInfo.Title);
            var status = context.TestError is null ? "passed" : "failed";
            var screenshotPath = Path.Combine(fixture.ScreenshotDirectory, $"{status}-{scenarioName}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true,
            });
        }

        if (context.TryGetValue<IBrowserContext>(out var browserContext))
            await browserContext.DisposeAsync();
    }

    private static async Task<IBrowser> EnsureBrowserAsync()
    {
        if (_browser is { } browser)
            return browser;

        await BrowserLock.WaitAsync();
        try
        {
            if (_browser is { } lockedBrowser)
                return lockedBrowser;

            _playwright ??= await Playwright.CreateAsync();
            try
            {
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                });
            }
            catch (PlaywrightException ex)
            {
                Skip.If(true,
                    "Playwright Chromium could not launch. Build the Specs.UI project and run the generated playwright.ps1 install script, then rerun the UI specs. " +
                    $"Underlying error: {ex.Message}");
                throw;
            }

            return _browser ?? throw new InvalidOperationException("Playwright browser was not initialized.");
        }
        finally
        {
            BrowserLock.Release();
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return sanitized.Replace(' ', '-');
    }
}
