using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Sessions page.
/// Route: /sessions
/// </summary>
public sealed class SessionsPage : BasePage
{
    public SessionsPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/sessions";

    // ── Navigation helpers ──
    public async Task NavigateToSessions() => await NavigateAsync();

    // ── Page header ──
    public ILocator PageHeading => Page.Locator("[data-testid='page-title']");

    // ── Session data grid ──
    public ILocator SessionDataGrid => Page.Locator("[data-testid='sessions-grid']");
    public ILocator SessionRows => Page.Locator("[data-testid='sessions-grid'] tr.mud-table-row");

    // ── Session action buttons ──
    public ILocator ViewButtons => Page.Locator("[data-testid='session-view-button']");
    public ILocator ExportButtons => Page.Locator("[data-testid='session-export-button']");
    public ILocator CloseButtons => Page.Locator("[data-testid='session-close-button']");

    // ── Session detail panel ──
    public ILocator SessionDetail => Page.Locator("[data-testid='session-detail']");
    public ILocator SessionDetailTitle => Page.Locator("[data-testid='session-detail'] >> text=Conversation:");
    public ILocator SessionTurns => Page.Locator("[data-testid='session-turn']");

    // ── Empty state ──
    public ILocator EmptyState => Page.Locator("[data-testid='sessions-empty']");
    public ILocator EmptyStateText =>
        Page.Locator("[data-testid='sessions-empty'] >> text=No sessions found");

    // ── Status chips ──
    public ILocator ActiveStatusChips => Page.Locator("[data-testid='session-status']:has-text('Active')");
    public ILocator ClosedStatusChips => Page.Locator("[data-testid='session-status']:has-text('Closed')");
}
