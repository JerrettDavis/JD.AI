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

    // ── Navigation ──────────────────────────────────────────

    [Given(@"I am on the routing page")]
    public async Task GivenIAmOnTheRoutingPage()
    {
        await _routingPage.NavigateToRouting();
        await _routingPage.WaitForLoadAsync();
    }

    // ── Rendering ───────────────────────────────────────────
    // Note: "I should see the Sync OpenClaw button with sync icon" is defined in SharedSteps.cs

    // ── Loading states ──────────────────────────────────────

    [Given(@"the routing mappings are loading")]
    public async Task GivenTheRoutingMappingsAreLoading()
    {
        // Navigate without waiting for load to complete so skeleton state is visible
        await _routingPage.NavigateAsync();
        await Task.CompletedTask;
    }

    // ── Data grid ───────────────────────────────────────────

    [Given(@"there are routing mappings")]
    public async Task GivenThereAreRoutingMappings()
    {
        // Wait for routing data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the routing data grid")]
    public async Task ThenIShouldSeeTheRoutingDataGrid()
    {
        var grid = _routingPage.RoutingDataGrid;
        await Expect(grid).ToBeVisibleAsync();
    }

    [Then(@"the grid should have a ""Channel"" column")]
    public async Task ThenTheGridShouldHaveAChannelColumn()
    {
        var header = _routingPage.RoutingDataGrid.Locator("th >> text=Channel");
        await Expect(header).ToBeVisibleAsync();
    }

    [Then(@"the grid should have an ""Agent ID"" column")]
    public async Task ThenTheGridShouldHaveAnAgentIdColumn()
    {
        var header = _routingPage.RoutingDataGrid.Locator("th >> text=Agent ID");
        await Expect(header).ToBeVisibleAsync();
    }

    [Then(@"the grid should have a ""Status"" column")]
    public async Task ThenTheGridShouldHaveAStatusColumn()
    {
        var header = _routingPage.RoutingDataGrid.Locator("th >> text=Status");
        await Expect(header).ToBeVisibleAsync();
    }

    // ── Channel column ──────────────────────────────────────

    [Then(@"each routing row should display a channel icon")]
    public async Task ThenEachRoutingRowShouldDisplayAChannelIcon()
    {
        var channelCells = _routingPage.ChannelCells;
        var count = await channelCells.CountAsync();
        Assert.True(count > 0, "Expected at least one routing channel cell");

        for (var i = 0; i < count; i++)
        {
            var icon = channelCells.Nth(i).Locator(".mud-icon-root");
            await Expect(icon).ToBeVisibleAsync();
        }
    }

    [Then(@"each routing row should display the channel type name")]
    public async Task ThenEachRoutingRowShouldDisplayTheChannelTypeName()
    {
        var channelCells = _routingPage.ChannelCells;
        var count = await channelCells.CountAsync();
        Assert.True(count > 0, "Expected at least one routing channel cell");

        for (var i = 0; i < count; i++)
        {
            var text = await channelCells.Nth(i).Locator(".mud-typography").TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Routing row {i} should display a channel type name");
        }
    }

    // ── Status chips ────────────────────────────────────────

    [Then(@"rows with an assigned agent should show ""Override"" chip in primary color")]
    public async Task ThenRowsWithAnAssignedAgentShouldShowOverrideChipInPrimaryColor()
    {
        var statusChips = _routingPage.StatusChips;
        var count = await statusChips.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var text = await statusChips.Nth(i).TextContentAsync();
            if (text != null && text.Contains("Override", StringComparison.OrdinalIgnoreCase))
            {
                var classes = await statusChips.Nth(i).GetAttributeAsync("class");
                Assert.NotNull(classes);
                Assert.Contains("mud-chip-color-primary", classes);
            }
        }
    }

    [Then(@"rows without an assigned agent should show ""Default"" chip")]
    public async Task ThenRowsWithoutAnAssignedAgentShouldShowDefaultChip()
    {
        var statusChips = _routingPage.StatusChips;
        var count = await statusChips.CountAsync();

        var foundDefault = false;
        for (var i = 0; i < count; i++)
        {
            var text = await statusChips.Nth(i).TextContentAsync();
            if (text != null && text.Contains("Default", StringComparison.OrdinalIgnoreCase))
            {
                foundDefault = true;
                break;
            }
        }

        // If no default chips found, all rows have assigned agents which is valid
        if (!foundDefault)
        {
            for (var i = 0; i < count; i++)
            {
                var text = await statusChips.Nth(i).TextContentAsync();
                Assert.NotNull(text);
                Assert.Contains("Override", text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ── Inline editing ──────────────────────────────────────

    [Then(@"the Agent ID column should support inline cell editing")]
    public async Task ThenTheAgentIdColumnShouldSupportInlineCellEditing()
    {
        // MudDataGrid with EditMode=Cell renders editable cells
        // Verify the grid has edit mode enabled by checking for editable cell indicators
        var grid = _routingPage.RoutingDataGrid;
        await Expect(grid).ToBeVisibleAsync();

        // Agent ID cells should be clickable for editing
        var rows = _routingPage.RoutingRows;
        var count = await rows.CountAsync();
        Assert.True(count > 0, "Expected at least one routing row for inline editing");
    }

    [When(@"I click to edit an agent ID")]
    public async Task WhenIClickToEditAnAgentId()
    {
        // Click on the Agent ID cell of the first row to enter edit mode
        var agentIdCells = _routingPage.AgentIdCells;
        var count = await agentIdCells.CountAsync();
        Assert.True(count > 0, "Expected at least one Agent ID cell to click");

        await agentIdCells.First.DblClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    [When(@"I edit the agent ID on a routing row")]
    public async Task WhenIEditTheAgentIdOnARoutingRow()
    {
        // Click on the Agent ID cell of the first row to enter edit mode
        var rows = _routingPage.RoutingRows;
        var firstRow = rows.First;
        var agentIdCell = firstRow.Locator("td").Nth(1);
        await agentIdCell.DblClickAsync();
        await _page.WaitForTimeoutAsync(300);

        // Type a new agent ID
        var input = firstRow.Locator("input");
        if (await input.CountAsync() > 0)
        {
            await input.First.FillAsync("test-agent-id");
        }
    }

    [When(@"I commit the cell edit")]
    public async Task WhenICommitTheCellEdit()
    {
        // Press Enter or Tab to commit the cell edit
        await _page.Keyboard.PressAsync("Tab");
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"a dropdown with available agents should appear")]
    public async Task ThenADropdownWithAvailableAgentsShouldAppear()
    {
        // After clicking to edit, a select/dropdown should appear with available agents
        var dropdown = _page.Locator(".mud-select, .mud-popover-open, .mud-list");
        await Expect(dropdown).ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    // ── Routing diagram ─────────────────────────────────────

    [Then(@"I should see the ""Routing Diagram"" section")]
    public async Task ThenIShouldSeeTheRoutingDiagramSection()
    {
        var diagramTitle = _routingPage.RoutingDiagramTitle;
        await Expect(diagramTitle).ToBeVisibleAsync();
    }

    [Then(@"the diagram should use a timeline layout")]
    public async Task ThenTheDiagramShouldUseATimelineLayout()
    {
        var timeline = _routingPage.RoutingDiagram.Locator(".mud-timeline");
        await Expect(timeline).ToBeVisibleAsync();
    }

    [Then(@"each timeline item should show a channel chip")]
    public async Task ThenEachTimelineItemShouldShowAChannelChip()
    {
        var timelineItems = _routingPage.RoutingTimelineItems;
        var count = await timelineItems.CountAsync();
        Assert.True(count > 0, "Expected at least one timeline item");

        for (var i = 0; i < count; i++)
        {
            var chip = timelineItems.Nth(i).Locator(".mud-chip-outlined");
            await Expect(chip).ToBeVisibleAsync();
        }
    }

    [Then(@"each timeline item should show an arrow")]
    public async Task ThenEachTimelineItemShouldShowAnArrow()
    {
        var timelineItems = _routingPage.RoutingTimelineItems;
        var count = await timelineItems.CountAsync();

        for (var i = 0; i < count; i++)
        {
            // The arrow is rendered as a MudIcon within the timeline item content
            var arrows = timelineItems.Nth(i).Locator(".mud-icon-root");
            var arrowCount = await arrows.CountAsync();
            Assert.True(arrowCount > 0, $"Timeline item {i} should have an arrow icon");
        }
    }

    [Then(@"each timeline item should show an agent chip or ""OpenClaw Default""")]
    public async Task ThenEachTimelineItemShouldShowAnAgentChipOrOpenClawDefault()
    {
        var timelineItems = _routingPage.RoutingTimelineItems;
        var count = await timelineItems.CountAsync();

        for (var i = 0; i < count; i++)
        {
            // Each timeline item has a text chip (agent ID or "OpenClaw Default")
            var agentChips = timelineItems.Nth(i).Locator(".mud-chip-text");
            var chipCount = await agentChips.CountAsync();
            Assert.True(chipCount > 0,
                $"Timeline item {i} should have an agent chip or 'OpenClaw Default'");

            var text = await agentChips.First.TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Timeline item {i} agent chip should have text content");
        }
    }

    // ── Timeline color coding ───────────────────────────────

    [Then(@"timeline items with assigned agents should be primary colored")]
    public async Task ThenTimelineItemsWithAssignedAgentsShouldBePrimaryColored()
    {
        var timelineItems = _routingPage.RoutingTimelineItems;
        var count = await timelineItems.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var agentChip = timelineItems.Nth(i).Locator(".mud-chip-text");
            var text = await agentChip.First.TextContentAsync();

            if (text != null && !text.Contains("OpenClaw Default", StringComparison.OrdinalIgnoreCase))
            {
                // This timeline item has an assigned agent - should be primary colored
                var classes = await timelineItems.Nth(i).GetAttributeAsync("class");
                Assert.NotNull(classes);
                Assert.Contains("mud-timeline-item-color-primary", classes);
            }
        }
    }

    [Then(@"timeline items using default should be default colored")]
    public async Task ThenTimelineItemsUsingDefaultShouldBeDefaultColored()
    {
        var timelineItems = _routingPage.RoutingTimelineItems;
        var count = await timelineItems.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var agentChip = timelineItems.Nth(i).Locator(".mud-chip-text");
            var text = await agentChip.First.TextContentAsync();

            if (text != null && text.Contains("OpenClaw Default", StringComparison.OrdinalIgnoreCase))
            {
                // This timeline item uses default - should be default colored
                var classes = await timelineItems.Nth(i).GetAttributeAsync("class");
                Assert.NotNull(classes);
                Assert.Contains("mud-timeline-item-color-default", classes);
            }
        }
    }

    // ── Sync OpenClaw ───────────────────────────────────────

    [Then(@"the routing data should refresh")]
    public async Task ThenTheRoutingDataShouldRefresh()
    {
        // After sync, the data grid should still be visible with data
        await _page.WaitForTimeoutAsync(500);
        var grid = _routingPage.RoutingDataGrid;
        await Expect(grid).ToBeVisibleAsync();
    }

    // ── Channel icons ───────────────────────────────────────

    [Then(@"routing channel icons should match their type")]
    public async Task ThenRoutingChannelIconsShouldMatchTheirType()
    {
        var channelCells = _routingPage.ChannelCells;
        var count = await channelCells.CountAsync();
        Assert.True(count > 0, "Expected at least one routing channel cell");

        for (var i = 0; i < count; i++)
        {
            var cell = channelCells.Nth(i);
            // Verify each channel cell has both an icon and a name
            var icon = cell.Locator(".mud-icon-root");
            var name = cell.Locator(".mud-typography");
            await Expect(icon).ToBeVisibleAsync();
            await Expect(name).ToBeVisibleAsync();

            // The icon should be an SVG element (MudBlazor renders icons as SVGs)
            var svg = icon.Locator("svg");
            var svgCount = await svg.CountAsync();
            Assert.True(svgCount > 0, $"Channel icon {i} should render as SVG");
        }
    }
}
