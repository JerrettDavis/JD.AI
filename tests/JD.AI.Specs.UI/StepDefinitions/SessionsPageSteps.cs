using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class SessionsPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private SessionsPage _sessionsPage = null!;

    public SessionsPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupSessionsPage()
    {
        _page = _context.Get<IPage>();
        _sessionsPage = new SessionsPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the sessions page")]
    public async Task GivenIAmOnTheSessionsPage()
    {
        await _sessionsPage.NavigateToSessions();
        await _sessionsPage.WaitForLoadAsync();
    }

    [Then(@"I should see the sessions page heading ""(.*)""")]
    public async Task ThenIShouldSeeTheSessionsPageHeading(string heading)
    {
        var headingLocator = _page.Locator($"text={heading}").First;
        await Expect(headingLocator).ToBeVisibleAsync();
    }

    [Given(@"there are active sessions")]
    public async Task GivenThereAreActiveSessions()
    {
        // Wait for session data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the sessions data grid")]
    public async Task ThenIShouldSeeTheSessionsDataGrid()
    {
        var grid = _page.Locator(".mud-table");
        await Expect(grid).ToBeVisibleAsync();
    }

    [Then(@"the grid should contain session rows")]
    public async Task ThenTheGridShouldContainSessionRows()
    {
        var rows = _page.Locator(".mud-table-body tr");
        var count = await rows.CountAsync();
        Assert.True(count > 0, "Expected at least one session row");
    }

    [Then(@"each session row should have a view button")]
    public async Task ThenEachSessionRowShouldHaveAViewButton()
    {
        var rows = _page.Locator(".mud-table-body tr");
        var count = await rows.CountAsync();

        for (var i = 0; i < count; i++)
        {
            // View button is the first icon button in each row's action cell
            var buttons = rows.Nth(i).Locator("button");
            var buttonCount = await buttons.CountAsync();
            Assert.True(buttonCount > 0,
                $"Session row {i} should have action buttons");
        }
    }

    [Then(@"each session row should have an export button")]
    public async Task ThenEachSessionRowShouldHaveAnExportButton()
    {
        var rows = _page.Locator(".mud-table-body tr");
        var count = await rows.CountAsync();

        for (var i = 0; i < count; i++)
        {
            // Export button is the second icon button in each row
            var buttons = rows.Nth(i).Locator("button");
            var buttonCount = await buttons.CountAsync();
            Assert.True(buttonCount >= 2,
                $"Session row {i} should have at least 2 action buttons (view + export)");
        }
    }

    [Given(@"there are no sessions")]
    public async Task GivenThereAreNoSessions()
    {
        // If the API returns no sessions, the empty state renders
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the sessions empty state")]
    public async Task ThenIShouldSeeTheSessionsEmptyState()
    {
        var emptyText = _page.Locator("text=No sessions found");
        await Expect(emptyText).ToBeVisibleAsync();
    }

    [Then(@"session rows should display status chips")]
    public async Task ThenSessionRowsShouldDisplayStatusChips()
    {
        var chips = _page.Locator(".mud-table-body .mud-chip");
        var count = await chips.CountAsync();
        Assert.True(count > 0, "Expected status chips in session rows");

        for (var i = 0; i < count; i++)
        {
            var text = await chips.Nth(i).TextContentAsync();
            Assert.NotNull(text);
            Assert.True(
                text.Contains("Active", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Closed", StringComparison.OrdinalIgnoreCase),
                $"Expected 'Active' or 'Closed' status chip, got '{text}'");
        }
    }
}
