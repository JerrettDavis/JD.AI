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
        Page.Locator($".mud-tab:has-text('{tabText}') .mud-icon-root");

    // ── Tab tooltip helpers ──
    public ILocator Tab(string tabText) =>
        Page.Locator($".mud-tab:has-text('{tabText}')");

    // ── Settings panel locators ──
    public ILocator ServerPanel => Page.Locator(".mud-tabpanel:visible");
    public ILocator RoutingSettingsPanel => Page.Locator(".mud-tabpanel:visible");
    public ILocator OpenClawPanel => Page.Locator(".mud-tabpanel:visible");

    /// <summary>Click a settings tab by its visible text.</summary>
    public async Task ClickTabAsync(string tabText)
    {
        await Page.Locator($".mud-tab:has-text('{tabText}')").ClickAsync();
    }
}
