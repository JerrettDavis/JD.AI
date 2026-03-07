using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Settings page.
/// Route: /settings
/// </summary>
public sealed class SettingsPage : BasePage
{
    public SettingsPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/settings";

    // ── Navigation helpers ──
    public async Task NavigateToSettings() => await NavigateAsync();

    // ── Page header ──
    public ILocator PageHeading => Page.Locator("[data-testid='page-title']");

    // ── Tab strip ──
    public ILocator TabStrip => Page.Locator("[data-testid='settings-tabs']");
    public ILocator ServerTab => Page.Locator("[data-testid='settings-tab-server']");
    public ILocator ProvidersTab => Page.Locator("[data-testid='settings-tab-providers']");
    public ILocator AgentsTab => Page.Locator("[data-testid='settings-tab-agents']");
    public ILocator ChannelsTab => Page.Locator("[data-testid='settings-tab-channels']");
    public ILocator RoutingTab => Page.Locator("[data-testid='settings-tab-routing']");
    public ILocator OpenClawTab => Page.Locator("[data-testid='settings-tab-openclaw']");

    // ── Tab panels ──
    public ILocator ActiveTabPanel => Page.Locator(".mud-tabs-panels .mud-tabpanel");

    // ── Save buttons (each tab has one) ──
    public ILocator SaveButton => Page.Locator("[data-testid='save-button']");
    public ILocator SaveAgentsButton => Page.Locator("button:has-text('Save Agents')");
    public ILocator SaveChannelsButton => Page.Locator("button:has-text('Save Channels')");
    public ILocator SaveProvidersButton => Page.Locator("button:has-text('Save Providers')");

    // ── Error state ──
    public ILocator ErrorAlert => Page.Locator("[data-testid='settings-error']");

    // ── Skeleton loading ──
    public ILocator SettingsSkeleton => Page.Locator(".mud-skeleton");

    // ── Tab icon locators ──
    public ILocator TabIcon(string tabText) =>
        Page.GetByRole(AriaRole.Tab, new() { Name = tabText, Exact = true }).Locator("svg, .mud-icon-root");

    // ── Tab tooltip helpers ──
    public ILocator Tab(string tabText) =>
        Page.GetByRole(AriaRole.Tab, new() { Name = tabText, Exact = true });

    // ── Settings panel locators ──
    public ILocator ServerPanel => Page.Locator(".mud-tabpanel:visible");
    public ILocator RoutingSettingsPanel => Page.Locator(".mud-tabpanel:visible");
    public ILocator OpenClawPanel => Page.Locator(".mud-tabpanel:visible");

    // ── Server tab ──
    public ILocator ServerHostInput => Page.Locator("[data-testid='server-host-input']");
    public ILocator ServerPortInput => Page.Locator("[data-testid='server-port-input']");
    public ILocator ServerVerboseToggle => Page.Locator("[data-testid='server-verbose-toggle']");
    public ILocator ServerAuthToggle => Page.Locator("[data-testid='server-auth-toggle']");
    public ILocator ServerRateLimitToggle => Page.Locator("[data-testid='server-ratelimit-toggle']");
    public ILocator SaveServerButton => Page.Locator("[data-testid='save-server-button']");

    // ── Agents tab ──
    public ILocator AgentEntries => Page.Locator("[data-testid='agent-entry']");
    public ILocator AddAgentButton => Page.Locator("[data-testid='add-agent-button']");
    public ILocator AgentIdFields => Page.Locator("[data-testid='agent-id-field']");
    public ILocator AgentProviderSelects => Page.Locator("[data-testid='agent-provider-select']");
    public ILocator AgentModelSelects => Page.Locator("[data-testid='agent-model-select']");
    public ILocator AgentParamsPanels => Page.Locator("[data-testid='agent-params-panel']");

    // ── Channels tab ──
    public ILocator ChannelEntries => Page.Locator("[data-testid='channel-entry']");
    public ILocator ChannelEnabledToggles => Page.Locator("[data-testid='channel-enabled-toggle']");
    public ILocator ChannelSettings => Page.Locator("[data-testid='channel-setting']");
    public ILocator SaveChannelsButtonById => Page.Locator("[data-testid='save-channels-button']");

    // ── Providers tab ──
    public ILocator ProviderEntries => Page.Locator("[data-testid='provider-entry']");
    public ILocator ProviderEnabledToggles => Page.Locator("[data-testid='provider-enabled-toggle']");
    public ILocator ProviderTestButtons => Page.Locator("[data-testid='provider-test-button']");
    public ILocator ProviderTestResults => Page.Locator("[data-testid='provider-test-result']");
    public ILocator SaveProvidersButtonById => Page.Locator("[data-testid='save-providers-button']");

    // ── Routing tab ──
    public ILocator DefaultAgentSelect => Page.Locator("[data-testid='default-agent-select']");
    public ILocator RoutingRules => Page.Locator("[data-testid='routing-rule']");
    public ILocator AddRuleButton => Page.Locator("[data-testid='add-rule-button']");
    public ILocator SaveRoutingButton => Page.Locator("[data-testid='save-routing-button']");

    // ── OpenClaw tab ──
    public ILocator OpenClawEnabledToggle => Page.Locator("[data-testid='openclaw-enabled-toggle']");
    public ILocator OpenClawWsUrl => Page.Locator("[data-testid='openclaw-ws-url']");
    public ILocator OpenClawAgentsSection => Page.Locator("[data-testid='openclaw-agents-section']");
    public ILocator SaveOpenClawButton => Page.Locator("[data-testid='save-openclaw-button']");

    /// <summary>Click a settings tab by its visible text.</summary>
    public async Task ClickTabAsync(string tabText)
    {
        await Page.GetByRole(AriaRole.Tab, new() { Name = tabText, Exact = true }).ClickAsync();
    }
}
