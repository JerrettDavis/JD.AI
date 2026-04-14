using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class SmokeSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private string _baseUrl = "";

    public SmokeSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@smoke", Order = 10)]
    public void Setup()
    {
        _page = _context.Get<IPage>();
        _baseUrl = _context.Get<DashboardFixture>().BaseUrl.TrimEnd('/');
    }

    [Given(@"I open the dashboard route ""(.*)""")]
    public async Task GivenIOpenTheDashboardRoute(string route)
    {
        await _page.GotoAsync($"{_baseUrl}{route}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the page heading ""(.*)""")]
    public async Task ThenIShouldSeeThePageHeading(string heading)
    {
        await Expect(_page.Locator($"h4:has-text('{heading}')")).ToBeVisibleAsync();
    }

    [Then(@"I should see (\d+) overview stat cards")]
    public async Task ThenIShouldSeeOverviewStatCards(int count)
    {
        await Expect(_page.Locator(".jd-stat-card")).ToHaveCountAsync(count);
    }

    [Then(@"I should see the recent activity panel")]
    public async Task ThenIShouldSeeTheRecentActivityPanel()
    {
        await Expect(_page.Locator("h6:has-text('Recent Activity')")).ToBeVisibleAsync();
    }

    [Then(@"I should see either an agents data grid or the agents empty state")]
    public async Task ThenIShouldSeeAgentsGridOrEmptyState()
    {
        await ExpectAnyVisibleAsync(
            _page.Locator(".mud-data-grid"),
            _page.Locator("[data-testid='agents-empty']"),
            _page.Locator("[data-testid='gateway-error-alert']"));
    }

    [Then(@"I should see the channels load error")]
    public async Task ThenIShouldSeeTheChannelsLoadError()
    {
        await Expect(_page.Locator("[data-testid='channels-load-error']")).ToBeVisibleAsync();
    }

    [Then(@"I should see the providers load error")]
    public async Task ThenIShouldSeeTheProvidersLoadError()
    {
        await Expect(_page.Locator("[data-testid='providers-load-error']")).ToBeVisibleAsync();
    }

    [Then(@"I should see the routing load error state")]
    public async Task ThenIShouldSeeTheRoutingLoadErrorState()
    {
        await Expect(_page.Locator("[data-testid='routing-load-error']")).ToBeVisibleAsync();
    }

    [Then(@"I should see the sync OpenClaw button")]
    public async Task ThenIShouldSeeTheSyncOpenClawButton()
    {
        await ExpectEitherVisibleAsync(
            _page.Locator("[data-testid='sync-openclaw-button']"),
            _page.Locator("button:has-text('Sync OpenClaw')"));
    }

    [Then(@"I should see either a sessions data grid or the sessions empty state")]
    public async Task ThenIShouldSeeSessionsGridOrEmptyState()
    {
        await ExpectEitherVisibleAsync(
            _page.Locator(".mud-data-grid"),
            _page.Locator("text=No sessions found"));
    }

    [Then(@"I should see either the settings tab strip or a settings unavailable message")]
    public async Task ThenIShouldSeeSettingsTabsOrUnavailableMessage()
    {
        await ExpectEitherVisibleAsync(
            _page.Locator(".jd-settings-tabs"),
            _page.Locator("text=Unable to load gateway configuration"));
    }

    private static async Task ExpectAnyVisibleAsync(
        params ILocator[] locators)
    {
        await ExpectAnyVisibleAsync(7000, locators);
    }

    private static async Task ExpectAnyVisibleAsync(
        int timeoutMs,
        params ILocator[] locators)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ArgumentNullException.ThrowIfNull(locators);

        if (locators.Length == 0)
            throw new ArgumentException("At least one locator is required.", nameof(locators));

        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            foreach (var locator in locators)
            {
                if (await locator.CountAsync() > 0 && await locator.First.IsVisibleAsync())
                    return;
            }

            await Task.Delay(150);
        }

        Assert.Fail("Expected one of the target UI states to become visible.");
    }

    private static Task ExpectEitherVisibleAsync(
        ILocator first,
        ILocator second,
        int timeoutMs = 7000)
    {
        return ExpectAnyVisibleAsync(timeoutMs, first, second);
    }
}
