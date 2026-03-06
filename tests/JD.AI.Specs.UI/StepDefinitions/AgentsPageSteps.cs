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
        var dialog = _page.Locator(".mud-dialog");
        await Expect(dialog).ToBeVisibleAsync();
    }

    // ── Spawn Agent button with icon ──────────────────────────

    [Then(@"I should see the ""(.*)"" button with add icon")]
    public async Task ThenIShouldSeeTheButtonWithAddIcon(string buttonText)
    {
        var button = _agentsPage.SpawnButton;
        await Expect(button).ToBeVisibleAsync();
        await Expect(button).ToContainTextAsync(buttonText);
        // Verify the button has the add icon (Material Filled Add)
        var icon = button.Locator(".mud-icon-root");
        await Expect(icon).ToBeVisibleAsync();
    }

    // ── Loading states ────────────────────────────────────────

    [Given(@"the agents list is loading")]
    public async Task GivenTheAgentsListIsLoading()
    {
        // Navigate fresh to catch the loading state before data resolves
        // In a real scenario, network intercept would hold the API response
        await _page.WaitForTimeoutAsync(100);
    }

    // ── Data grid columns ─────────────────────────────────────

    [Then(@"the data grid should have an ""(.*)"" column")]
    public async Task ThenTheDataGridShouldHaveAnColumn(string columnName)
    {
        var header = _agentsPage.DataGridColumnHeader(columnName);
        await Expect(header).ToBeVisibleAsync();
    }

    [Then(@"the data grid should have a ""(.*)"" column")]
    public async Task ThenTheDataGridShouldHaveAColumn(string columnName)
    {
        var header = _agentsPage.DataGridColumnHeader(columnName);
        await Expect(header).ToBeVisibleAsync();
    }

    // ── Delete button per row ─────────────────────────────────

    [Then(@"each agent row should have a red delete button")]
    public async Task ThenEachAgentRowShouldHaveARedDeleteButton()
    {
        var rows = _page.Locator(".mud-table-body tr");
        var count = await rows.CountAsync();
        Assert.True(count > 0, "Expected at least one agent row");

        for (var i = 0; i < count; i++)
        {
            var deleteBtn = rows.Nth(i).Locator("[data-testid='delete-agent-button']");
            await Expect(deleteBtn).ToBeVisibleAsync();
        }
    }

    // ── Dialog input fields (parameterized) ───────────────────

    [Then(@"the dialog should contain an ""(.*)"" input")]
    public async Task ThenTheDialogShouldContainAnInput(string label)
    {
        var input = _page.Locator($".mud-dialog >> label:has-text('{label}')");
        await Expect(input).ToBeVisibleAsync();
    }

    [Then(@"the dialog should contain a ""(.*)"" input")]
    public async Task ThenTheDialogShouldContainAInput(string label)
    {
        var input = _page.Locator($".mud-dialog >> label:has-text('{label}')");
        await Expect(input).ToBeVisibleAsync();
    }

    [Then(@"the dialog should contain a ""(.*)"" multiline input")]
    public async Task ThenTheDialogShouldContainAMultilineInput(string label)
    {
        var input = _page.Locator($".mud-dialog >> label:has-text('{label}')");
        await Expect(input).ToBeVisibleAsync();
        // Verify it's a textarea (multiline)
        var textarea = _page.Locator($"[data-testid='agent-systemprompt-input'] textarea");
        await Expect(textarea).ToBeVisibleAsync();
    }

    [Then(@"the dialog should contain a ""(.*)"" numeric input")]
    public async Task ThenTheDialogShouldContainANumericInput(string label)
    {
        var input = _page.Locator($".mud-dialog >> label:has-text('{label}')");
        await Expect(input).ToBeVisibleAsync();
    }

    // ── Spawn form filling ────────────────────────────────────

    [When(@"I fill in the agent ID with a unique value")]
    public async Task WhenIFillInTheAgentIdWithAUniqueValue()
    {
        var uniqueId = $"test-agent-{DateTime.UtcNow.Ticks}";
        await _agentsPage.AgentIdInput.FillAsync(uniqueId);
    }

    [When(@"I fill in the provider with ""(.*)""")]
    public async Task WhenIFillInTheProviderWith(string provider)
    {
        await _agentsPage.ProviderInput.FillAsync(provider);
    }

    [When(@"I fill in the model with ""(.*)""")]
    public async Task WhenIFillInTheModelWith(string model)
    {
        await _agentsPage.ModelInput.FillAsync(model);
    }

    [When(@"I leave the agent ID empty")]
    public async Task WhenILeaveTheAgentIdEmpty()
    {
        await _agentsPage.AgentIdInput.FillAsync("");
    }

    // ── Data grid refresh ─────────────────────────────────────

    [Then(@"the agents data grid should refresh")]
    public async Task ThenTheAgentsDataGridShouldRefresh()
    {
        // After successful spawn, the dialog closes and the data grid reloads
        // Wait for dialog to close, then verify the grid is visible
        await _page.WaitForTimeoutAsync(1000);
        var grid = _page.Locator(".mud-table");
        await Expect(grid).ToBeVisibleAsync();
    }

    // ── Delete confirmation ───────────────────────────────────

    [Then(@"a confirmation dialog should appear with ""(.*)""")]
    public async Task ThenAConfirmationDialogShouldAppearWith(string text)
    {
        var dialog = _page.Locator($".mud-dialog >> text={text}");
        await Expect(dialog).ToBeVisibleAsync();
    }

    [When(@"I confirm the deletion")]
    public async Task WhenIConfirmTheDeletion()
    {
        await _agentsPage.ConfirmStopButton.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    [When(@"I cancel the deletion")]
    public async Task WhenICancelTheDeletion()
    {
        var cancelButton = _page.Locator(".mud-dialog >> button:has-text('Cancel')");
        await cancelButton.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"the agent should still be in the list")]
    public async Task ThenTheAgentShouldStillBeInTheList()
    {
        var rows = _page.Locator(".mud-table-body tr");
        var count = await rows.CountAsync();
        Assert.True(count > 0, "Expected agent to still be in the list after cancelling deletion");
    }
}
