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

    // ── Activity event details ──
    public ILocator ActivityEventTypeChips => Page.Locator("[data-testid='activity-event-type']");
    public ILocator ActivityEventMessages => Page.Locator("[data-testid='activity-event-message']");
    public ILocator ActivityEventTimestamps => Page.Locator("[data-testid='activity-event-timestamp']");

    // ── OpenClaw Bridge ──
    public ILocator OpenClawBridgeSection => Page.Locator("[data-testid='openclaw-bridge']");

    // ── Skeleton loaders ──
    public ILocator SkeletonStatCards => Page.Locator("[data-testid='skeleton-stat-card']");
    public ILocator SkeletonActivity => Page.Locator("[data-testid='skeleton-activity']");
    public ILocator SkeletonCards => Page.Locator(".jd-stat-card .mud-skeleton");

    // ── Connection status ──
    public ILocator ConnectionIndicator => Page.Locator("[data-testid='connection-status']");

    // ── App bar logo ──
    public ILocator AppBarLogoText => Page.Locator("[data-testid='app-bar'] .jd-logo-text");
    public ILocator AppBarLogoSub => Page.Locator("[data-testid='app-bar'] .jd-logo-sub");

    // ── Nav links for navigation tests ──
    public ILocator AgentsNavLink => Page.Locator("[data-testid='nav-agents']");
}
