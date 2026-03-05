using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class SettingsPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private SettingsPage _settingsPage = null!;

    public SettingsPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupSettingsPage()
    {
        _page = _context.Get<IPage>();
        _settingsPage = new SettingsPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the settings page")]
    public async Task GivenIAmOnTheSettingsPage()
    {
        await _settingsPage.NavigateToSettings();
        await _settingsPage.WaitForLoadAsync();
    }

    [Then(@"I should see the settings page heading ""(.*)""")]
    public async Task ThenIShouldSeeTheSettingsPageHeading(string heading)
    {
        var headingLocator = _page.Locator($"text={heading}").First;
        await Expect(headingLocator).ToBeVisibleAsync();
    }

    [Then(@"I should see the settings tab strip")]
    public async Task ThenIShouldSeeTheSettingsTabStrip()
    {
        var tabs = _page.Locator(".mud-tabs");
        await Expect(tabs).ToBeVisibleAsync();
    }

    [Then(@"the tab strip should contain a ""(.*)"" tab")]
    public async Task ThenTheTabStripShouldContainATab(string tabName)
    {
        var tab = _page.Locator($".mud-tab:has-text('{tabName}')");
        await Expect(tab).ToBeVisibleAsync();
    }

    [Then(@"the tab strip should contain an ""(.*)"" tab")]
    public async Task ThenTheTabStripShouldContainAnTab(string tabName)
    {
        var tab = _page.Locator($".mud-tab:has-text('{tabName}')");
        await Expect(tab).ToBeVisibleAsync();
    }

    [When(@"I click the ""(.*)"" settings tab")]
    public async Task WhenIClickTheSettingsTab(string tabName)
    {
        await _settingsPage.ClickTabAsync(tabName);
        await _page.WaitForTimeoutAsync(300);
    }

    [Then(@"the agents settings panel should be visible")]
    public async Task ThenTheAgentsSettingsPanelShouldBeVisible()
    {
        // The agents tab shows agent definitions with "Agent ID" labels
        var panel = _page.Locator("text=Define agents that can handle conversations");
        await Expect(panel).ToBeVisibleAsync();
    }

    [Then(@"a save agents button should be available")]
    public async Task ThenASaveAgentsButtonShouldBeAvailable()
    {
        var saveButton = _page.Locator("button:has-text('Save Agents')");
        await Expect(saveButton).ToBeVisibleAsync();
    }

    [Then(@"the channels settings panel should be visible")]
    public async Task ThenTheChannelsSettingsPanelShouldBeVisible()
    {
        // The channels tab shows channel configuration description
        var panel = _page.Locator("text=Channels connect the gateway to messaging platforms");
        await Expect(panel).ToBeVisibleAsync();
    }

    [Then(@"a save channels button should be available")]
    public async Task ThenASaveChannelsButtonShouldBeAvailable()
    {
        var saveButton = _page.Locator("button:has-text('Save Channels')");
        await Expect(saveButton).ToBeVisibleAsync();
    }

    [Then(@"the providers settings panel should be visible")]
    public async Task ThenTheProvidersSettingsPanelShouldBeVisible()
    {
        // The providers tab shows AI provider description
        var panel = _page.Locator("text=AI providers supply the models your agents use");
        await Expect(panel).ToBeVisibleAsync();
    }

    [Then(@"a save providers button should be available")]
    public async Task ThenASaveProvidersButtonShouldBeAvailable()
    {
        var saveButton = _page.Locator("button:has-text('Save Providers')");
        await Expect(saveButton).ToBeVisibleAsync();
    }
}
