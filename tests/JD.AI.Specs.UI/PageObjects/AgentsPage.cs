using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Agents page.
/// Route: /agents
/// </summary>
public sealed class AgentsPage : BasePage
{
    public AgentsPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/agents";

    // ── Navigation helpers ──
    public async Task NavigateToAgents() => await NavigateAsync();

    // ── Page header ──
    public ILocator PageHeading => Page.Locator("[data-testid='page-title']");
    public ILocator SpawnButton => Page.Locator("[data-testid='spawn-agent-button']");

    // ── Agent data grid ──
    public ILocator AgentDataGrid => Page.Locator("[data-testid='agents-grid']");
    public ILocator AgentRows => Page.Locator("[data-testid='agents-grid'] tr.mud-table-row");
    public ILocator DeleteButtons => Page.Locator("[data-testid='delete-agent-button']");

    // ── Empty state ──
    public ILocator EmptyState => Page.Locator("[data-testid='agents-empty']");
    public ILocator EmptyStateText => Page.Locator("[data-testid='agents-empty'] >> text=No active agents");

    // ── Spawn Agent Dialog ──
    public ILocator SpawnDialog => Page.Locator("[data-testid='spawn-agent-dialog']");
    public ILocator SpawnDialogTitle => Page.Locator(".mud-dialog >> text=Spawn New Agent");
    public ILocator AgentIdInput => Page.Locator("[data-testid='agent-id-input'] input");
    public ILocator ProviderInput => Page.Locator("[data-testid='agent-provider-input'] input");
    public ILocator ModelInput => Page.Locator("[data-testid='agent-model-input'] input");
    public ILocator SystemPromptInput => Page.Locator("[data-testid='agent-systemprompt-input'] textarea");
    public ILocator MaxTurnsInput => Page.Locator("[data-testid='agent-maxturns-input'] input");
    public ILocator SpawnSubmitButton => Page.Locator(".mud-dialog >> button:has-text('Spawn')");
    public ILocator CancelButton => Page.Locator(".mud-dialog >> button:has-text('Cancel')");

    // ── Confirmation dialog ──
    public ILocator ConfirmStopButton => Page.Locator(".mud-dialog >> button:has-text('Stop')");
    public ILocator CancelDeletionButton => Page.Locator(".mud-dialog >> button:has-text('Cancel')");
    public ILocator ConfirmationDialogText => Page.Locator(".mud-dialog .mud-dialog-content");

    // ── Status indicators on agent cards ──
    public ILocator AgentStatusChips => Page.Locator("[data-testid='agent-status']");

    // ── Loading skeleton ──
    public ILocator LoadingSkeleton => Page.Locator(".jd-skeleton-row");

    // ── Data grid column headers ──
    public ILocator DataGridColumnHeader(string columnName) =>
        Page.Locator($"[data-testid='agents-grid'] th:has-text('{columnName}')");

    // ── Snackbar ──
    public ILocator SnackbarWithText(string text) =>
        Page.Locator($".mud-snackbar >> text={text}");
}
