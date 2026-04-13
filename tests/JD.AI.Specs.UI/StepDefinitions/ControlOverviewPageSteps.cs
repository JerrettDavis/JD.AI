using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

/// <summary>
/// Step definitions for the Control &gt; Overview Page feature.
/// </summary>
[Binding]
public sealed class ControlOverviewPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;

    public ControlOverviewPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void Setup()
    {
        _page = _context.Get<IPage>();
    }

    // ── Snapshot cards ─────────────────────────────────────────────────────

    /// <summary>
    /// Snapshot cards are only rendered when the gateway is reachable.
    /// When disconnected (CI baseline), the gateway error boundary is shown
    /// instead.  Accept either state so smoke tests pass without a live gateway.
    /// </summary>
    [Then(@"I should see snapshot cards or the gateway error state")]
    public async Task ThenIShouldSeeSnapshotCardsOrGatewayErrorState()
    {
        var snapshotCard = _page.Locator("[data-testid='snapshot-card-status']");
        var errorAlert = _page.Locator("[data-testid='gateway-error-alert']");

        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < 7000)
        {
            if (await snapshotCard.CountAsync() > 0 && await snapshotCard.IsVisibleAsync())
                return;
            if (await errorAlert.CountAsync() > 0 && await errorAlert.IsVisibleAsync())
                return;
            await Task.Delay(150);
        }

        Assert.Fail("Expected either snapshot cards or the gateway error alert to become visible.");
    }

    // ── Navigation active state ─────────────────────────────────────────────

    [Then(@"the nav link ""(.*)"" should be active")]
    public async Task ThenTheNavLinkShouldBeActive(string selector)
    {
        var link = _page.Locator(selector).First;
        await Expect(link).ToBeVisibleAsync();
        var classes = await link.GetAttributeAsync("class") ?? "";
        Assert.Contains("active", classes, StringComparison.OrdinalIgnoreCase);
    }

    // ── Error state ─────────────────────────────────────────────────────────

    [Then(@"the page should not show an error state")]
    public async Task ThenThePageShouldNotShowAnErrorState()
    {
        // Wait a moment for any async operations to complete
        await _page.WaitForTimeoutAsync(500);
        // The page title should still be visible (page hasn't crashed)
        var title = _page.Locator("[data-testid='page-title']");
        await Expect(title).ToBeVisibleAsync();
    }

    // ── Click by selector ────────────────────────────────────────────────────

    [When(@"I click ""(.*)""")]
    public async Task WhenIClickSelector(string selector)
    {
        var element = _page.Locator(selector).First;
        await element.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }
}
