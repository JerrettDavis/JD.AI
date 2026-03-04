using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Gateway Overview (Home) page.
/// Route: /
/// </summary>
public sealed class HomePage : BasePage
{
    public HomePage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/";

    // ── Navigation helpers ──
    public async Task NavigateToHome() => await NavigateAsync();

    // ── Stat cards ──
    public ILocator StatCards => Page.Locator("[data-testid='stat-card']");
    public ILocator AgentsStatCard => Page.Locator("[data-testid='stat-card-agents']");
    public ILocator ChannelsStatCard => Page.Locator("[data-testid='stat-card-channels']");
    public ILocator SessionsStatCard => Page.Locator("[data-testid='stat-card-sessions']");
    public ILocator OpenClawStatCard => Page.Locator("[data-testid='stat-card-openclaw']");
    public ILocator StatCardValue(string name) =>
        Page.Locator($"[data-testid='stat-card-{name}'] [data-testid='stat-value']");

    // ── Recent Activity ──
    public ILocator RecentActivitySection => Page.Locator("[data-testid='recent-activity']");
    public ILocator RecentActivityTitle => Page.Locator("[data-testid='recent-activity'] >> text=Recent Activity");
    public ILocator ActivityItems => Page.Locator("[data-testid='activity-item']");
    public ILocator ActivityEmptyState => Page.Locator("[data-testid='activity-empty']");

    // ── OpenClaw Bridge ──
    public ILocator OpenClawBridgeSection => Page.Locator("[data-testid='openclaw-bridge']");

    // ── Skeleton loaders ──
    public ILocator SkeletonCards => Page.Locator(".jd-stat-card .mud-skeleton");

    // ── Nav links for navigation tests ──
    public ILocator AgentsNavLink => Page.Locator("a[href='/agents']");
}
