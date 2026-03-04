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
    public ILocator AgentIdCells => Page.Locator("[data-testid='routing-agent-id']");
    public ILocator StatusChips => Page.Locator("[data-testid='routing-status']");

    // ── Routing diagram ──
    public ILocator RoutingDiagram => Page.Locator("[data-testid='routing-diagram']");
    public ILocator RoutingDiagramTitle =>
        Page.Locator("[data-testid='routing-diagram'] >> text=Routing Diagram");
    public ILocator RoutingTimelineItems => Page.Locator("[data-testid='routing-diagram'] .mud-timeline-item");

    // ── Add route button ──
    public ILocator AddRouteButton => Page.Locator("[data-testid='add-route-button']");
}
