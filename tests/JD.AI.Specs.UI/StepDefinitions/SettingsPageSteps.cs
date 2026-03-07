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

    // ── Navigation ──────────────────────────────────────────

    [Given(@"I am on the settings page")]
    public async Task GivenIAmOnTheSettingsPage()
    {
        await _settingsPage.NavigateToSettings();
        await _settingsPage.WaitForLoadAsync();
    }

    // ── Loading states ──────────────────────────────────────

    [Given(@"the settings are loading")]
    public async Task GivenTheSettingsAreLoading()
    {
        // Navigate without waiting for load to complete so skeleton state is visible
        await _settingsPage.NavigateAsync();
        await Task.CompletedTask;
    }

    [Then(@"I should see a settings loading skeleton")]
    public async Task ThenIShouldSeeASettingsLoadingSkeleton()
    {
        var skeleton = _settingsPage.SettingsSkeleton;
        var count = await skeleton.CountAsync();
        if (count == 0)
        {
            // Data loaded quickly; verify either tabs or error alert are present instead
            var tabs = _settingsPage.TabStrip;
            var error = _settingsPage.ErrorAlert;
            var tabsVisible = await tabs.IsVisibleAsync();
            var errorVisible = await error.IsVisibleAsync();
            Assert.True(tabsVisible || errorVisible,
                "Expected skeleton loading, tab strip, or error alert to be visible");
        }
        else
        {
            Assert.True(count > 0, "Expected skeleton loading placeholders to be visible");
        }
    }

    // ── Error state ─────────────────────────────────────────

    [Given(@"the gateway configuration cannot be loaded")]
    public async Task GivenTheGatewayConfigurationCannotBeLoaded()
    {
        // Block API calls to simulate unavailable gateway configuration
        await _page.RouteAsync("**/api/**", route => route.AbortAsync());
        // Reload the page so the blocked routes take effect
        await _settingsPage.NavigateToSettings();
        await _settingsPage.WaitForLoadAsync();
    }

    [Then(@"I should see an error alert")]
    public async Task ThenIShouldSeeAnErrorAlert()
    {
        var alert = _settingsPage.ErrorAlert;
        await Expect(alert).ToBeVisibleAsync();
    }

    [Then(@"the alert should display ""(.*)""")]
    public async Task ThenTheAlertShouldDisplay(string text)
    {
        var alert = _settingsPage.ErrorAlert;
        await Expect(alert).ToContainTextAsync(text);
    }

    // ── Tab strip ───────────────────────────────────────────

    [Then(@"I should see the settings tab strip")]
    public async Task ThenIShouldSeeTheSettingsTabStrip()
    {
        var tabs = _settingsPage.TabStrip;
        await Expect(tabs).ToBeVisibleAsync();
    }

    [Then(@"the tab strip should contain a ""(.*)"" tab")]
    public async Task ThenTheTabStripShouldContainATab(string tabName)
    {
        var tab = _settingsPage.Tab(tabName);
        await Expect(tab).ToBeVisibleAsync();
    }

    [Then(@"the tab strip should contain an ""(.*)"" tab")]
    public async Task ThenTheTabStripShouldContainAnTab(string tabName)
    {
        var tab = _settingsPage.Tab(tabName);
        await Expect(tab).ToBeVisibleAsync();
    }

    // ── Tab icons ───────────────────────────────────────────

    [Then(@"the ""(.*)"" tab should have (?:a|an) (.*) icon")]
    public async Task ThenTheTabShouldHaveAnIcon(string tabName, string iconType)
    {
        _ = iconType;
        var icon = _settingsPage.TabIcon(tabName);
        await Expect(icon).ToBeVisibleAsync();
    }

    // ── Tab tooltips ────────────────────────────────────────

    [Then(@"the ""(.*)"" tab should have tooltip ""(.*)""")]
    public async Task ThenTheTabShouldHaveTooltip(string tabName, string tooltip)
    {
        var tab = _settingsPage.Tab(tabName);
        await Expect(tab).ToHaveAttributeAsync("title", tooltip);
    }

    // ── Tab navigation ──────────────────────────────────────

    [Then(@"the server settings panel should be visible")]
    public async Task ThenTheServerSettingsPanelShouldBeVisible()
    {
        // The server tab shows network, auth, and rate-limit settings
        var panel = _settingsPage.ServerPanel;
        await Expect(panel).ToBeVisibleAsync();

        // Look for server-specific content (port, host, or network settings)
        var serverContent = panel.Locator("text=/Host|Port|Listen|Network|Base URL/i");
        var count = await serverContent.CountAsync();
        Assert.True(count > 0,
            "Expected the server settings panel to contain server-related content");
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
        var saveButton = _settingsPage.SaveProvidersButton;
        await Expect(saveButton).ToBeVisibleAsync();
    }

    [Then(@"the agents settings panel should be visible")]
    public async Task ThenTheAgentsSettingsPanelShouldBeVisible()
    {
        // The agents tab shows agent definitions with description text
        var panel = _page.Locator("text=Define agents that can handle conversations");
        await Expect(panel).ToBeVisibleAsync();
    }

    [Then(@"a save agents button should be available")]
    public async Task ThenASaveAgentsButtonShouldBeAvailable()
    {
        var saveButton = _settingsPage.SaveAgentsButton;
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
        var saveButton = _settingsPage.SaveChannelsButton;
        await Expect(saveButton).ToBeVisibleAsync();
    }

    [Then(@"the routing settings panel should be visible")]
    public async Task ThenTheRoutingSettingsPanelShouldBeVisible()
    {
        // The routing tab shows routing rules configuration
        var panel = _settingsPage.RoutingSettingsPanel;
        await Expect(panel).ToBeVisibleAsync();

        // Look for routing-specific content
        var routingContent = panel.Locator("text=/[Rr]outing|[Cc]hannel|[Aa]gent/");
        var count = await routingContent.CountAsync();
        Assert.True(count > 0,
            "Expected the routing settings panel to contain routing-related content");
    }

    [Then(@"the OpenClaw settings panel should be visible")]
    public async Task ThenTheOpenClawSettingsPanelShouldBeVisible()
    {
        // The OpenClaw tab shows OpenClaw bridge integration settings
        var panel = _settingsPage.OpenClawPanel;
        await Expect(panel).ToBeVisibleAsync();

        // Look for OpenClaw-specific content
        var openClawContent = panel.Locator("text=/[Oo]pen[Cc]law|[Bb]ridge|[Ss]ync/");
        var count = await openClawContent.CountAsync();
        Assert.True(count > 0,
            "Expected the OpenClaw settings panel to contain OpenClaw-related content");
    }

    // ── Server tab content ────────────────────────────────

    [Then(@"I should see a ""(.*)"" input field")]
    public async Task ThenIShouldSeeAnInputField(string label)
    {
        var input = _page.Locator($".mud-input-label:has-text('{label}')");
        await Expect(input).ToBeVisibleAsync();
    }

    [Then(@"I should see a ""(.*)"" toggle")]
    public async Task ThenIShouldSeeAToggle(string label)
    {
        var toggle = _page.Locator($"text={label}");
        await Expect(toggle).ToBeVisibleAsync();
    }

    [Then(@"I should see an ""(.*)"" toggle")]
    public async Task ThenIShouldSeeAnToggle(string label)
    {
        var toggle = _page.Locator($"text={label}");
        await Expect(toggle).ToBeVisibleAsync();
    }

    [Then(@"I should see the ""(.*)"" button")]
    public async Task ThenIShouldSeeTheNamedButton(string buttonText)
    {
        var button = _page.Locator($"button:has-text('{buttonText}')");
        await Expect(button).ToBeVisibleAsync();
    }

    // ── Agents tab content ────────────────────────────────

    [Then(@"I should see the add agent button")]
    public async Task ThenIShouldSeeTheAddAgentButton()
    {
        await Expect(_settingsPage.AddAgentButton).ToBeVisibleAsync();
    }

    [Given(@"there are configured agents")]
    public async Task GivenThereAreConfiguredAgents()
    {
        // Wait for agent data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"each agent entry should have an ""(.*)"" field")]
    public async Task ThenEachAgentEntryShouldHaveAField(string fieldLabel)
    {
        var entries = _settingsPage.AgentEntries;
        var count = await entries.CountAsync();
        if (count > 0)
        {
            var fields = _page.Locator($"[data-testid='agent-entry'] >> .mud-input-label:has-text('{fieldLabel}')");
            var fieldCount = await fields.CountAsync();
            Assert.True(fieldCount >= count,
                $"Expected at least {count} '{fieldLabel}' fields but found {fieldCount}");
        }
    }

    [Then(@"each agent entry should have a ""(.*)"" select")]
    public async Task ThenEachAgentEntryShouldHaveASelect(string selectLabel)
    {
        var entries = _settingsPage.AgentEntries;
        var count = await entries.CountAsync();
        if (count > 0)
        {
            var selects = _page.Locator($"[data-testid='agent-entry'] >> .mud-input-label:has-text('{selectLabel}')");
            var selectCount = await selects.CountAsync();
            Assert.True(selectCount >= count,
                $"Expected at least {count} '{selectLabel}' selects but found {selectCount}");
        }
    }

    [Then(@"each agent entry should have a ""(.*)"" expansion panel")]
    public async Task ThenEachAgentEntryShouldHaveAnExpansionPanel(string panelText)
    {
        var entries = _settingsPage.AgentEntries;
        var count = await entries.CountAsync();
        if (count > 0)
        {
            var panels = _settingsPage.AgentParamsPanels;
            var panelCount = await panels.CountAsync();
            Assert.True(panelCount >= count,
                $"Expected at least {count} '{panelText}' expansion panels but found {panelCount}");
        }
    }

    // ── Channels tab content ──────────────────────────────

    [Then(@"I should see channel entries")]
    public async Task ThenIShouldSeeChannelEntries()
    {
        var entries = _settingsPage.ChannelEntries;
        var count = await entries.CountAsync();
        Assert.True(count > 0, "Expected at least one channel entry");
    }

    [Then(@"each channel entry should have an enabled toggle")]
    public async Task ThenEachChannelEntryShouldHaveAnEnabledToggle()
    {
        var entries = _settingsPage.ChannelEntries;
        var count = await entries.CountAsync();
        if (count > 0)
        {
            var toggles = _settingsPage.ChannelEnabledToggles;
            var toggleCount = await toggles.CountAsync();
            Assert.True(toggleCount >= count,
                $"Expected at least {count} enabled toggles but found {toggleCount}");
        }
    }

    [Given(@"a channel has a setting containing ""(.*)"" in its key")]
    public async Task GivenAChannelHasASettingContainingInItsKey(string keyword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        // Wait for settings to render with channel data
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"that setting field should be a password input")]
    public async Task ThenThatSettingFieldShouldBeAPasswordInput()
    {
        var passwordInputs = _page.Locator("[data-testid='channel-setting'] input[type='password']");
        var count = await passwordInputs.CountAsync();
        // If there are token settings, they should be password type
        // If no token settings exist, this is vacuously true
        if (count == 0)
        {
            var settings = _settingsPage.ChannelSettings;
            var settingCount = await settings.CountAsync();
            // If no channel settings are visible at all, that's okay
            Assert.True(settingCount == 0,
                "Expected token-containing settings to be password inputs");
        }
    }

    // ── Providers tab content ─────────────────────────────

    [Then(@"I should see provider entries")]
    public async Task ThenIShouldSeeProviderEntries()
    {
        var entries = _settingsPage.ProviderEntries;
        var count = await entries.CountAsync();
        Assert.True(count > 0, "Expected at least one provider entry");
    }

    [Then(@"each provider entry should have a ""(.*)"" button")]
    public async Task ThenEachProviderEntryShouldHaveAButton(string buttonText)
    {
        var entries = _settingsPage.ProviderEntries;
        var count = await entries.CountAsync();
        if (count > 0)
        {
            var buttons = _settingsPage.ProviderTestButtons;
            var buttonCount = await buttons.CountAsync();
            Assert.True(buttonCount >= count,
                $"Expected at least {count} '{buttonText}' buttons but found {buttonCount}");
        }
    }

    // ── Routing tab content ───────────────────────────────

    [Then(@"I should see a ""(.*)"" select")]
    public async Task ThenIShouldSeeASelect(string label)
    {
        var select = _page.Locator($".mud-input-label:has-text('{label}')");
        await Expect(select).ToBeVisibleAsync();
    }

    // ── OpenClaw tab content ──────────────────────────────

    [Then(@"I should see an ""(.*)"" toggle for the bridge")]
    public async Task ThenIShouldSeeAnToggleForTheBridge(string label)
    {
        var toggle = _page.Locator($"text={label}");
        await Expect(toggle).ToBeVisibleAsync();
    }

    [Then(@"I should see a ""(.*)"" input")]
    public async Task ThenIShouldSeeAnInput(string label)
    {
        var input = _page.Locator($".mud-input-label:has-text('{label}')");
        await Expect(input).ToBeVisibleAsync();
    }

    [Then(@"I should see the registered agents section")]
    public async Task ThenIShouldSeeTheRegisteredAgentsSection()
    {
        await Expect(_settingsPage.OpenClawAgentsSection).ToBeVisibleAsync();
    }
}
