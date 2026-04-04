using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Logs page.
/// Route: /logs
/// </summary>
public sealed class LogsPage : BasePage
{
    public LogsPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/logs";

    public async Task NavigateToLogs() => await NavigateAsync();

    public ILocator AutoRefreshToggle => Page.Locator("[data-testid='auto-refresh-toggle']");
    public ILocator FiltersPanel => Page.Locator("[data-testid='log-filters']");
    public ILocator SearchFilter => Page.Locator("[data-testid='search-filter'] input");
    public ILocator ApplyFiltersButton => Page.Locator("[data-testid='apply-filters']");
    public ILocator LogsGrid => Page.Locator("[data-testid='logs-grid']");
    public ILocator LogsEmptyState => Page.Locator("[data-testid='logs-empty']");
}
