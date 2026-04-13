using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

/// <summary>
/// Step definitions for the Navigation Menu feature.
/// </summary>
[Binding]
public sealed class NavMenuSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;

    public NavMenuSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void Setup()
    {
        _page = _context.Get<IPage>();
    }

    // ── Nav links ──────────────────────────────────────────────────────────

    [Then(@"I should see a nav link labeled ""(.*)"" with href ""(.*)""")]
    public async Task ThenIShouldSeeANavLinkLabeledWithHref(string label, string href)
    {
        var link = _page.Locator($"[data-testid='nav-menu'] a[href='{href}']");
        await Expect(link.First).ToBeVisibleAsync();
    }

    [Then(@"I should see a nav link labeled ""(.*)""")]
    public async Task ThenIShouldSeeANavLinkLabeled(string label)
    {
        var link = _page.Locator($"[data-testid='nav-menu'] a:has-text('{label}')").First;
        await Expect(link).ToBeVisibleAsync();
    }

    // ── Nav groups ─────────────────────────────────────────────────────────

    [Then(@"I should see the nav group ""(.*)""")]
    public async Task ThenIShouldSeeTheNavGroup(string groupName)
    {
#pragma warning disable CA1308
        var group = _page.Locator($"[data-testid='nav-group-{groupName.ToLowerInvariant()}']");
#pragma warning restore CA1308
        await Expect(group.First).ToBeVisibleAsync();
    }

    [Then(@"the ""(.*)"" group should be expanded")]
    public async Task ThenTheGroupShouldBeExpanded(string groupName)
    {
        // When the MudNavGroup is expanded, its child nav links are visible in the DOM.
#pragma warning disable CA1308
        var groupSection = _page.Locator($"[data-testid='nav-section-{groupName.ToLowerInvariant()}']");
#pragma warning restore CA1308
        // At least one child nav link inside the section should be visible.
        var childLinks = groupSection.Locator("a.mud-nav-link");
        await Expect(childLinks.First).ToBeVisibleAsync();
    }

    [Then(@"the ""(.*)"" group should be collapsed")]
    public async Task ThenTheGroupShouldBeCollapsed(string groupName)
    {
#pragma warning disable CA1308
        var groupSection = _page.Locator($"[data-testid='nav-section-{groupName.ToLowerInvariant()}']");
#pragma warning restore CA1308
        var childLinks = groupSection.Locator("a.mud-nav-link");
        await Expect(childLinks.First).ToBeHiddenAsync();
    }

    [Then(@"I should not see a nav link labeled ""(.*)""")]
    public async Task ThenIShouldNotSeeANavLinkLabeled(string label)
    {
        var link = _page.Locator($"[data-testid='nav-menu'] a:has-text('{label}')").First;
        await Expect(link).ToBeHiddenAsync();
    }

    // ── Group toggle ───────────────────────────────────────────────────────

    [When(@"I click the ""(.*)"" nav group toggle")]
    public async Task WhenIClickTheNavGroupToggle(string groupName)
    {
#pragma warning disable CA1308
        var groupTitle = _page.Locator($"[data-testid='nav-group-{groupName.ToLowerInvariant()}'] button").First;
#pragma warning restore CA1308
        await groupTitle.ClickAsync(new LocatorClickOptions { Force = true });
        await _page.WaitForTimeoutAsync(300);
    }

    // ── Page reload ────────────────────────────────────────────────────────

    [When(@"I reload the page")]
    public async Task WhenIReloadThePage()
    {
        await _page.ReloadAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(500);
    }
}
