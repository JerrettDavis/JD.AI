using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

/// <summary>
/// Step definitions shared across multiple page feature files.
/// These bindings are intentionally generic so they can be reused
/// without causing Reqnroll ambiguous-binding errors.
/// </summary>
[Binding]
public sealed class SharedSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;

    public SharedSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void Setup()
    {
        _page = _context.Get<IPage>();
    }

    // ── Heading ──────────────────────────────────────────────

    [Then(@"I should see the heading ""(.*)""")]
    public async Task ThenIShouldSeeTheHeading(string heading)
    {
        var headingLocator = _page.Locator($"text={heading}").First;
        await Expect(headingLocator).ToBeVisibleAsync();
    }

    // ── Page title ───────────────────────────────────────────

    [Then(@"the browser page title should be ""(.*)""")]
    public async Task ThenTheBrowserPageTitleShouldBe(string expectedTitle)
    {
        await Expect(_page).ToHaveTitleAsync(expectedTitle);
    }

    // ── Empty state ──────────────────────────────────────────

    [Then(@"the empty state should display ""(.*)""")]
    public async Task ThenTheEmptyStateShouldDisplay(string text)
    {
        var element = _page.Locator($"text={text}");
        await Expect(element).ToBeVisibleAsync();
    }

    // ── Snackbar ─────────────────────────────────────────────

    [Then(@"a success snackbar should appear with ""(.*)""")]
    public async Task ThenASuccessSnackbarShouldAppearWith(string text)
    {
        var snackbar = _page.Locator($".mud-snackbar >> text={text}");
        await Expect(snackbar).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Then(@"a warning snackbar should appear with ""(.*)""")]
    public async Task ThenAWarningSnackbarShouldAppearWith(string text)
    {
        var snackbar = _page.Locator($".mud-snackbar >> text={text}");
        await Expect(snackbar).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Then(@"an error snackbar should appear")]
    public async Task ThenAnErrorSnackbarShouldAppear()
    {
        var snackbar = _page.Locator(".mud-snackbar-error, .mud-alert-filled-error");
        await Expect(snackbar).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    // ── Refresh button ─────────────────────────────────────────

    [Then(@"I should see the refresh button")]
    public async Task ThenIShouldSeeTheRefreshButton()
    {
        var refreshButton = _page.Locator("[data-testid='refresh-button']");
        await Expect(refreshButton).ToBeVisibleAsync();
    }

    // ── Sync OpenClaw button ────────────────────────────────────

    [Then(@"I should see the ""Sync OpenClaw"" button with sync icon")]
    public async Task ThenIShouldSeeTheSyncOpenClawButtonWithSyncIcon()
    {
        var syncButton = _page.Locator("[data-testid='sync-openclaw-button']");
        await Expect(syncButton).ToBeVisibleAsync();

        // Verify the button contains a sync icon (MudBlazor renders icons as SVGs)
        var icon = syncButton.Locator("svg, .mud-icon-root");
        await Expect(icon.First).ToBeVisibleAsync();
    }

    // ── Skeleton loading rows ───────────────────────────────────

    [Then(@"I should see (\d+) skeleton loading rows")]
    public async Task ThenIShouldSeeSkeletonLoadingRows(int count)
    {
        var skeletonRows = _page.Locator(".jd-skeleton-row");
        await Expect(skeletonRows).ToHaveCountAsync(count);
    }

    // ── Generic tab clicks ──────────────────────────────────────

    [When(@"I click the ""(.*)"" tab")]
    public async Task WhenIClickTheTab(string tabName)
    {
        var tab = _page.GetByRole(AriaRole.Tab, new() { Name = tabName, Exact = true });
        await Expect(tab).ToBeVisibleAsync();
        await tab.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    // ── Generic button clicks ────────────────────────────────

    [When(@"I click the ""(.*)"" button")]
    public async Task WhenIClickTheButton(string buttonText)
    {
        var button = _page.Locator($"button:has-text('{buttonText}')");
        await button.ClickAsync();
        // Wait for dialog animation / state transition
        await _page.WaitForTimeoutAsync(300);
    }

    // ── Generic dialog assertions ────────────────────────────

    [Then(@"the dialog should close")]
    public async Task ThenTheDialogShouldClose()
    {
        var dialog = _page.Locator(".mud-dialog");
        await Expect(dialog).ToBeHiddenAsync(new() { Timeout = 3000 });
    }

    [Then(@"the dialog should remain open")]
    public async Task ThenTheDialogShouldRemainOpen()
    {
        var dialog = _page.Locator(".mud-dialog");
        await Expect(dialog).ToBeVisibleAsync();
    }

    [Then(@"the dialog title should be ""(.*)""")]
    public async Task ThenTheDialogTitleShouldBe(string title)
    {
        var titleLocator = _page.Locator($".mud-dialog >> text={title}");
        await Expect(titleLocator).ToBeVisibleAsync();
    }

    [Then(@"the dialog should have a ""(.*)"" button")]
    public async Task ThenTheDialogShouldHaveAButton(string buttonText)
    {
        var button = _page.Locator($".mud-dialog >> button:has-text('{buttonText}')");
        await Expect(button).ToBeVisibleAsync();
    }
}
