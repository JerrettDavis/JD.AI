using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class AgentsPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private AgentsPage _agentsPage = null!;

    public AgentsPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupAgentsPage()
    {
        _page = _context.Get<IPage>();
        _agentsPage = new AgentsPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the agents page")]
    public async Task GivenIAmOnTheAgentsPage()
    {
        await _agentsPage.NavigateToAgents();
        await _agentsPage.WaitForLoadAsync();
    }

    [Then(@"I should see the agents page heading ""(.*)""")]
    public async Task ThenIShouldSeeTheAgentsPageHeading(string heading)
    {
        var headingLocator = _page.Locator($"text={heading}").First;
        await Expect(headingLocator).ToBeVisibleAsync();
    }

    [Then(@"I should see the spawn agent button")]
    public async Task ThenIShouldSeeTheSpawnAgentButton()
    {
        var spawnButton = _page.Locator("button:has-text('Spawn Agent')");
        await Expect(spawnButton).ToBeVisibleAsync();
    }

    [When(@"I click the spawn agent button")]
    public async Task WhenIClickTheSpawnAgentButton()
    {
        var spawnButton = _page.Locator("button:has-text('Spawn Agent')");
        await spawnButton.ClickAsync();
        // Wait for dialog animation
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"the spawn agent dialog should be visible")]
    public async Task ThenTheSpawnAgentDialogShouldBeVisible()
    {
        await Expect(_agentsPage.SpawnDialogTitle).ToBeVisibleAsync();
    }

    [Then(@"the dialog should contain an agent ID input")]
    public async Task ThenTheDialogShouldContainAnAgentIdInput()
    {
        var input = _page.Locator(".mud-dialog >> label:has-text('Agent ID')");
        await Expect(input).ToBeVisibleAsync();
    }

    [Then(@"the dialog should contain a provider input")]
    public async Task ThenTheDialogShouldContainAProviderInput()
    {
        var input = _page.Locator(".mud-dialog >> label:has-text('Provider')");
        await Expect(input).ToBeVisibleAsync();
    }

    [Then(@"the dialog should contain a model input")]
    public async Task ThenTheDialogShouldContainAModelInput()
    {
        var input = _page.Locator(".mud-dialog >> label:has-text('Model')");
        await Expect(input).ToBeVisibleAsync();
    }

    [Given(@"there are no active agents")]
    public async Task GivenThereAreNoActiveAgents()
    {
        // The page is already loaded; if the API returns empty agents,
        // the empty state renders. We verify the empty state is shown.
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the agents empty state")]
    public async Task ThenIShouldSeeTheAgentsEmptyState()
    {
        var emptyState = _page.Locator("text=No active agents");
        await Expect(emptyState).ToBeVisibleAsync();
    }

    [Then(@"the empty state should display ""(.*)""")]
    public async Task ThenTheEmptyStateShouldDisplay(string text)
    {
        var element = _page.Locator($"text={text}");
        await Expect(element).ToBeVisibleAsync();
    }

    [Given(@"there are active agents")]
    public async Task GivenThereAreActiveAgents()
    {
        // Agents should be present from the API. Wait for data grid to load.
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the agents data grid")]
    public async Task ThenIShouldSeeTheAgentsDataGrid()
    {
        var grid = _page.Locator(".mud-table");
        await Expect(grid).ToBeVisibleAsync();
    }

    [Then(@"the data grid should contain agent rows")]
    public async Task ThenTheDataGridShouldContainAgentRows()
    {
        var rows = _page.Locator(".mud-table-body tr");
        var count = await rows.CountAsync();
        Assert.True(count > 0, "Expected at least one agent row in the data grid");
    }

    [When(@"I click the delete button on the first agent")]
    public async Task WhenIClickTheDeleteButtonOnTheFirstAgent()
    {
        var deleteButton = _page.Locator(".mud-table-body tr >> button").First;
        await deleteButton.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"a confirmation dialog should appear")]
    public async Task ThenAConfirmationDialogShouldAppear()
    {
        var dialog = _page.Locator(".mud-dialog >> text=Stop Agent");
        await Expect(dialog).ToBeVisibleAsync();
    }
}
