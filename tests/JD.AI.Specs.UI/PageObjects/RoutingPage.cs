using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Routing page.
/// Route: /routing
/// </summary>
public sealed class RoutingPage : BasePage
{
    public RoutingPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/routing";

    // ── Navigation helpers ──
    public async Task NavigateToRouting() => await NavigateAsync();

    // ── Page header ──
    public ILocator PageHeading => Page.Locator("[data-testid='page-title']");
    public ILocator SyncOpenClawButton => Page.Locator("[data-testid='sync-openclaw-button']");

    // ── Routing data grid ──
    public ILocator RoutingDataGrid => Page.Locator("[data-testid='routing-grid']");
    public ILocator RoutingRows => Page.Locator("[data-testid='routing-grid'] tr.mud-table-row");

    // ── Column cells ──
    public ILocator ChannelCells => Page.Locator("[data-testid='routing-channel']");
    public ILocator AgentIdCells => Page.Locator("[data-testid='routing-grid'] tbody tr td:nth-child(2)");
    public ILocator StatusChips => Page.Locator("[data-testid='routing-status']");

    // ── Skeleton loading ──
    public ILocator SkeletonRows => Page.Locator(".jd-skeleton-row");

    // ── Channel icon within channel cells ──
    public ILocator ChannelIcons => Page.Locator("[data-testid='routing-channel'] .mud-icon-root");

    // ── Routing diagram ──
    public ILocator RoutingDiagram => Page.Locator("[data-testid='routing-diagram']");
    public ILocator RoutingDiagramTitle =>
        Page.Locator("[data-testid='routing-diagram'] >> text=Routing Diagram");
    public ILocator RoutingTimelineItems => Page.Locator("[data-testid='routing-diagram'] .mud-timeline-item");

    // ── Timeline channel/arrow/agent chips ──
    public ILocator TimelineChannelChips => Page.Locator("[data-testid='routing-diagram'] .mud-timeline-item .mud-chip-outlined");
    public ILocator TimelineArrows => Page.Locator("[data-testid='routing-diagram'] .mud-timeline-item .mud-icon-root");
    public ILocator TimelineAgentChips => Page.Locator("[data-testid='routing-diagram'] .mud-timeline-item .mud-chip-text");

    // ── Status chip text ──
    public async Task<string?> GetStatusChipTextAsync(int index)
    {
        return await StatusChips.Nth(index).TextContentAsync();
    }

    // ── Add route button ──
    public ILocator AddRouteButton => Page.Locator("[data-testid='add-route-button']");
}
