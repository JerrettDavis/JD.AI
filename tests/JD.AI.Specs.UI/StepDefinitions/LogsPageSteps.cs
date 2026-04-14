using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class LogsPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private LogsPage _logsPage = null!;

    public LogsPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupLogsPage()
    {
        _page = _context.Get<IPage>();
        _logsPage = new LogsPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the logs page")]
    public async Task GivenIAmOnTheLogsPage()
    {
        await _logsPage.NavigateToLogs();
        await _logsPage.WaitForLoadAsync();
    }

    [Then(@"I should see the logs filter panel")]
    public async Task ThenIShouldSeeTheLogsFilterPanel() =>
        await Expect(_logsPage.FiltersPanel).ToBeVisibleAsync();

    [Then(@"I should see the auto-refresh toggle")]
    public async Task ThenIShouldSeeTheAutoRefreshToggle() =>
        await Expect(_logsPage.AutoRefreshToggle).ToBeVisibleAsync();

    [Then(@"I should see either the logs grid or the logs empty state")]
    public async Task ThenIShouldSeeEitherTheLogsGridOrTheLogsEmptyState()
    {
        var gridVisible = await _logsPage.LogsGrid.IsVisibleAsync();
        var emptyVisible = await _logsPage.LogsEmptyState.IsVisibleAsync();
        var errorVisible = await _page.Locator("[data-testid='gateway-error-alert']").IsVisibleAsync();
        Assert.True(gridVisible || emptyVisible || errorVisible,
            "Expected either the logs grid, logs empty state, or gateway error alert.");
    }

    [Then(@"I should see the logs empty state")]
    public async Task ThenIShouldSeeTheLogsEmptyState() =>
        await Expect(_logsPage.LogsEmptyState).ToBeVisibleAsync();

    [Given(@"there is at least one log event row")]
    public async Task GivenThereIsAtLeastOneLogEventRow()
    {
        await Expect(_logsPage.LogsGrid).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [When(@"I search logs for a guaranteed-miss term")]
    public async Task WhenISearchLogsForAGuaranteedMissTerm()
    {
        await _logsPage.SearchFilter.FillAsync("reqnroll-ui-no-match-zzzz");
        await _logsPage.ApplyFiltersButton.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }
}
