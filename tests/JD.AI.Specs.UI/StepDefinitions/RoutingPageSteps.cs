using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class RoutingPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private RoutingPage _routingPage = null!;

    public RoutingPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupRoutingPage()
    {
        _page = _context.Get<IPage>();
        _routingPage = new RoutingPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the routing page")]
    public async Task GivenIAmOnTheRoutingPage()
    {
        await _routingPage.NavigateToRouting();
        await _routingPage.WaitForLoadAsync();
    }

    [Then(@"I should see the routing page heading")]
    public async Task ThenIShouldSeeTheRoutingPageHeading()
    {
        // The heading is "Channel -> Agent Routing" which uses a special arrow
        var heading = _page.Locator("text=Agent Routing");
        await Expect(heading).ToBeVisibleAsync();
    }

    [Then(@"I should see the routing sync OpenClaw button")]
    public async Task ThenIShouldSeeTheRoutingSyncOpenClawButton()
    {
        var syncButton = _page.Locator("button:has-text('Sync OpenClaw')");
        await Expect(syncButton).ToBeVisibleAsync();
    }

    [Given(@"there are routing mappings")]
    public async Task GivenThereAreRoutingMappings()
    {
        // Wait for routing data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the routing data grid")]
    public async Task ThenIShouldSeeTheRoutingDataGrid()
    {
        var grid = _page.Locator(".mud-table");
        await Expect(grid).ToBeVisibleAsync();
    }

    [Then(@"the grid should contain routing rows")]
    public async Task ThenTheGridShouldContainRoutingRows()
    {
        var rows = _page.Locator(".mud-table-body tr");
        var count = await rows.CountAsync();
        Assert.True(count > 0, "Expected at least one routing row");
    }

    [Then(@"each routing row should display a channel type")]
    public async Task ThenEachRoutingRowShouldDisplayAChannelType()
    {
        var rows = _page.Locator(".mud-table-body tr");
        var count = await rows.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var row = rows.Nth(i);
            var firstCell = row.Locator("td").First;
            var text = await firstCell.TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Routing row {i} should display a channel type");
        }
    }

    [Then(@"each routing row should display an agent ID or default")]
    public async Task ThenEachRoutingRowShouldDisplayAnAgentIdOrDefault()
    {
        // Each row has a status chip showing "Default" or "Override"
        var chips = _page.Locator(".mud-table-body .mud-chip");
        var count = await chips.CountAsync();
        Assert.True(count > 0, "Expected status chips in routing rows");

        for (var i = 0; i < count; i++)
        {
            var text = await chips.Nth(i).TextContentAsync();
            Assert.NotNull(text);
            Assert.True(
                text.Contains("Default", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Override", StringComparison.OrdinalIgnoreCase),
                $"Expected 'Default' or 'Override' chip, got '{text}'");
        }
    }

    [Then(@"I should see the routing diagram section")]
    public async Task ThenIShouldSeeTheRoutingDiagramSection()
    {
        var diagramTitle = _page.Locator("text=Routing Diagram");
        await Expect(diagramTitle).ToBeVisibleAsync();
    }

    [Then(@"the diagram should contain timeline items")]
    public async Task ThenTheDiagramShouldContainTimelineItems()
    {
        var timelineItems = _page.Locator(".mud-timeline-item");
        var count = await timelineItems.CountAsync();
        Assert.True(count > 0, "Expected timeline items in routing diagram");
    }
}
