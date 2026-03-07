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

    // ── Navigation ──────────────────────────────────────────

    [Given(@"I am on the sessions page")]
    public async Task GivenIAmOnTheSessionsPage()
    {
        await _sessionsPage.NavigateToSessions();
        await _sessionsPage.WaitForLoadAsync();
    }

    // ── Loading states ──────────────────────────────────────

    [Given(@"the sessions list is loading")]
    public async Task GivenTheSessionsListIsLoading()
    {
        // Navigate fresh so we catch the loading state before data arrives
        await _sessionsPage.NavigateToSessions();
    }

    // Note: "I should see 5 skeleton loading rows" is handled by SharedSteps

    // ── Empty state ─────────────────────────────────────────

    [Given(@"there are no sessions")]
    public async Task GivenThereAreNoSessions()
    {
        // If the API returns no sessions, the empty state renders
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the sessions empty state")]
    public async Task ThenIShouldSeeTheSessionsEmptyState()
    {
        await Expect(_sessionsPage.EmptyState).ToBeVisibleAsync();
    }

    // ── Data display ────────────────────────────────────────

    [Given(@"there are sessions")]
    public async Task GivenThereAreSessions()
    {
        // Wait for session data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the sessions data grid")]
    public async Task ThenIShouldSeeTheSessionsDataGrid()
    {
        await Expect(_sessionsPage.SessionDataGrid).ToBeVisibleAsync();
    }

    [Then(@"the grid should have a ""(.*)"" column")]
    public async Task ThenTheGridShouldHaveAColumn(string columnName)
    {
        var header = _page.Locator($"[data-testid='sessions-grid'] th:has-text('{columnName}')");
        await Expect(header).ToBeVisibleAsync();
    }

    // ── Status chips ────────────────────────────────────────

    [Then(@"active sessions should show ""Active"" chip in green")]
    public async Task ThenActiveSessionsShouldShowActiveChipInGreen()
    {
        var activeChips = _sessionsPage.ActiveStatusChips;
        var count = await activeChips.CountAsync();
        if (count > 0)
        {
            for (var i = 0; i < count; i++)
            {
                var cssClass = await activeChips.Nth(i).GetAttributeAsync("class") ?? "";
                Assert.Contains("success", cssClass, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Then(@"closed sessions should show ""Closed"" chip in default color")]
    public async Task ThenClosedSessionsShouldShowClosedChipInDefaultColor()
    {
        var closedChips = _sessionsPage.ClosedStatusChips;
        var count = await closedChips.CountAsync();
        if (count > 0)
        {
            for (var i = 0; i < count; i++)
            {
                var cssClass = await closedChips.Nth(i).GetAttributeAsync("class") ?? "";
                Assert.Contains("default", cssClass, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Then(@"session rows should display status chips")]
    public async Task ThenSessionRowsShouldDisplayStatusChips()
    {
        var chips = _page.Locator("[data-testid='session-status']");
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

    // ── Filter controls ─────────────────────────────────────

    [Then(@"the data grid should have filter controls")]
    public async Task ThenTheDataGridShouldHaveFilterControls()
    {
        // MudDataGrid with Filterable="true" renders filter icons in column headers
        var filterIcons = _page.Locator("[data-testid='sessions-grid'] .mud-table-head button");
        var count = await filterIcons.CountAsync();
        Assert.True(count > 0, "Expected filter controls in the data grid header");
    }

    // ── Sorting ─────────────────────────────────────────────

    [When(@"I click a column header")]
    public async Task WhenIClickAColumnHeader()
    {
        // Click the first sortable column header (Session ID)
        var header = _page.Locator("[data-testid='sessions-grid'] th").First;
        await header.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"the grid should sort by that column")]
    public async Task ThenTheGridShouldSortByThatColumn()
    {
        // After clicking a column header, MudDataGrid adds a sort indicator class
        var sortIndicator = _page.Locator("[data-testid='sessions-grid'] .mud-table-head .mud-sort-icon, [data-testid='sessions-grid'] th .mud-table-sort-label");
        var count = await sortIndicator.CountAsync();
        Assert.True(count > 0, "Expected a sort indicator after clicking column header");
    }

    // ── Row action buttons ──────────────────────────────────

    [Then(@"each session row should have a view button")]
    public async Task ThenEachSessionRowShouldHaveAViewButton()
    {
        var viewButtons = _sessionsPage.ViewButtons;
        var count = await viewButtons.CountAsync();
        Assert.True(count > 0, "Expected at least one view button");
    }

    [Then(@"each session row should have an export button")]
    public async Task ThenEachSessionRowShouldHaveAnExportButton()
    {
        var exportButtons = _sessionsPage.ExportButtons;
        var count = await exportButtons.CountAsync();
        Assert.True(count > 0, "Expected at least one export button");
    }

    // ── Active sessions have close button ───────────────────

    [Given(@"there are active sessions")]
    public async Task GivenThereAreActiveSessions()
    {
        // Wait for session data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"active session rows should have a close button in warning color")]
    public async Task ThenActiveSessionRowsShouldHaveACloseButtonInWarningColor()
    {
        var closeButtons = _sessionsPage.CloseButtons;
        var count = await closeButtons.CountAsync();
        Assert.True(count > 0, "Expected at least one close button for active sessions");

        for (var i = 0; i < count; i++)
        {
            var cssClass = await closeButtons.Nth(i).GetAttributeAsync("class") ?? "";
            Assert.Contains("warning", cssClass, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Closed sessions do not have close button ────────────

    [Given(@"there are closed sessions")]
    public async Task GivenThereAreClosedSessions()
    {
        // Wait for session data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"closed session rows should not have a close button")]
    public async Task ThenClosedSessionRowsShouldNotHaveACloseButton()
    {
        // Closed sessions render only view + export buttons, not the close button.
        // The close button is only conditionally rendered for active sessions.
        // We verify by checking the rows with "Closed" status chips don't have close buttons.
        var rows = _page.Locator("[data-testid='sessions-grid'] tr.mud-table-row");
        var count = await rows.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var statusChip = rows.Nth(i).Locator("[data-testid='session-status']");
            var statusText = await statusChip.TextContentAsync();
            if (statusText != null && statusText.Contains("Closed", StringComparison.OrdinalIgnoreCase))
            {
                var closeBtn = rows.Nth(i).Locator("[data-testid='session-close-button']");
                var closeBtnCount = await closeBtn.CountAsync();
                Assert.Equal(0, closeBtnCount);
            }
        }
    }

    // ── Turn viewer ─────────────────────────────────────────

    [When(@"I click the view button on a session")]
    public async Task WhenIClickTheViewButtonOnASession()
    {
        var viewButton = _sessionsPage.ViewButtons.First;
        await viewButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"the turn viewer panel should appear below the grid")]
    public async Task ThenTheTurnViewerPanelShouldAppearBelowTheGrid()
    {
        await Expect(_sessionsPage.SessionDetail).ToBeVisibleAsync();
    }

    [Then(@"the turn viewer should show ""Conversation:"" with the session ID")]
    public async Task ThenTheTurnViewerShouldShowConversationWithTheSessionId()
    {
        await Expect(_sessionsPage.SessionDetailTitle).ToBeVisibleAsync();
    }

    // ── Turn viewer: conversation turns ─────────────────────

    [Given(@"there are sessions with turns")]
    public async Task GivenThereAreSessionsWithTurns()
    {
        // Wait for session data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"each turn should show a role chip")]
    public async Task ThenEachTurnShouldShowARoleChip()
    {
        var roleChips = _sessionsPage.TurnRoleChips;
        var count = await roleChips.CountAsync();
        Assert.True(count > 0, "Expected role chips in turn entries");

        for (var i = 0; i < count; i++)
        {
            var text = await roleChips.Nth(i).TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Turn {i} role chip should have text");
        }
    }

    [Then(@"each turn should show message content")]
    public async Task ThenEachTurnShouldShowMessageContent()
    {
        var turns = _sessionsPage.SessionTurns;
        var count = await turns.CountAsync();
        Assert.True(count > 0, "Expected at least one turn");

        for (var i = 0; i < count; i++)
        {
            var text = await turns.Nth(i).TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Turn {i} should have message content");
        }
    }

    [Then(@"each turn should show token counts")]
    public async Task ThenEachTurnShouldShowTokenCounts()
    {
        var tokenTexts = _sessionsPage.TurnTokenCounts;
        var count = await tokenTexts.CountAsync();
        Assert.True(count > 0, "Expected token count text in turns");
    }

    [Then(@"each turn should show response duration")]
    public async Task ThenEachTurnShouldShowResponseDuration()
    {
        var durationTexts = _sessionsPage.TurnDurations;
        var count = await durationTexts.CountAsync();
        Assert.True(count > 0, "Expected duration text in turns");
    }

    [Then(@"each turn should have a colored left border")]
    public async Task ThenEachTurnShouldHaveAColoredLeftBorder()
    {
        var turns = _sessionsPage.TurnBorders;
        var count = await turns.CountAsync();
        Assert.True(count > 0, "Expected at least one turn");

        for (var i = 0; i < count; i++)
        {
            var style = await turns.Nth(i).GetAttributeAsync("style") ?? "";
            Assert.Contains("border-left", style,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Turn viewer: dismiss ────────────────────────────────

    [Given(@"I am viewing a session's turns")]
    public async Task GivenIAmViewingASessionsTurns()
    {
        await _page.WaitForTimeoutAsync(500);
        // Click the first view button to open the turn viewer
        var viewButton = _sessionsPage.ViewButtons.First;
        await viewButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Expect(_sessionsPage.SessionDetail).ToBeVisibleAsync();
    }

    [When(@"I click the close button on the turn viewer")]
    public async Task WhenIClickTheCloseButtonOnTheTurnViewer()
    {
        await _sessionsPage.TurnViewerCloseButton.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"the turn viewer should disappear")]
    public async Task ThenTheTurnViewerShouldDisappear()
    {
        await Expect(_sessionsPage.SessionDetail).ToBeHiddenAsync(new() { Timeout = 3000 });
    }

    // ── Empty session ───────────────────────────────────────

    [Given(@"there is a session with no turns")]
    public async Task GivenThereIsASessionWithNoTurns()
    {
        // Wait for session data to load
        await _page.WaitForTimeoutAsync(500);
    }

    [When(@"I click the view button on that session")]
    public async Task WhenIClickTheViewButtonOnThatSession()
    {
        var viewButton = _sessionsPage.ViewButtons.First;
        await viewButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"the turn viewer should show ""No turns in this session""")]
    public async Task ThenTheTurnViewerShouldShowNoTurnsInThisSession()
    {
        var noTurnsText = _page.Locator("[data-testid='session-detail'] >> text=No turns in this session");
        await Expect(noTurnsText).ToBeVisibleAsync();
    }

    // ── Export ───────────────────────────────────────────────

    [When(@"I click the export button on a session")]
    public async Task WhenIClickTheExportButtonOnASession()
    {
        var exportButton = _sessionsPage.ExportButtons.First;
        await exportButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    // ── Close session ───────────────────────────────────────

    [When(@"I click the close button on an active session")]
    public async Task WhenIClickTheCloseButtonOnAnActiveSession()
    {
        var closeButton = _sessionsPage.CloseButtons.First;
        await closeButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"the session list should refresh")]
    public async Task ThenTheSessionListShouldRefresh()
    {
        // After closing a session, the data grid reloads
        // Wait for the grid to be visible as confirmation of refresh
        await _page.WaitForTimeoutAsync(1000);
        await Expect(_sessionsPage.SessionDataGrid).ToBeVisibleAsync();
    }
}
