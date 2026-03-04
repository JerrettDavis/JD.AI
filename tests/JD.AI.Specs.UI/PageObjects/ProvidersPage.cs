using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Model Providers page.
/// Route: /providers
/// </summary>
public sealed class ProvidersPage : BasePage
{
    public ProvidersPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/providers";

    // ── Navigation helpers ──
    public async Task NavigateToProviders() => await NavigateAsync();

    // ── Page header ──
    public ILocator PageHeading => Page.Locator("[data-testid='page-title']");

    // ── Provider cards ──
    public ILocator ProviderCards => Page.Locator("[data-testid='provider-card']");
    public ILocator ProviderNames => Page.Locator("[data-testid='provider-name']");
    public ILocator ProviderStatusBadges => Page.Locator("[data-testid='provider-status']");

    // ── Model tables within provider cards ──
    public ILocator ModelTables => Page.Locator("[data-testid='provider-models']");
    public ILocator ModelRows => Page.Locator("[data-testid='provider-models'] tbody tr");

    // ── Detect / refresh button ──
    public ILocator DetectButton => Page.Locator("[data-testid='refresh-button']");

    // ── Empty state ──
    public ILocator EmptyState => Page.Locator("[data-testid='providers-empty']");
    public ILocator EmptyStateText =>
        Page.Locator("[data-testid='providers-empty'] >> text=No providers configured");
}
