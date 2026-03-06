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

    // ── Skeleton loading ──
    public ILocator SkeletonCards => Page.Locator(".jd-skeleton-card");

    // ── Provider subtitle (caption text under name) ──
    public ILocator ProviderSubtitles => Page.Locator("[data-testid='provider-card'] .mud-card-header-content .mud-typography-caption");

    // ── Provider avatars ──
    public ILocator ProviderAvatars => Page.Locator("[data-testid='provider-card'] .mud-avatar");

    /// <summary>Check if a provider avatar has the success (green) color class.</summary>
    public ILocator SuccessAvatars => Page.Locator("[data-testid='provider-card'] .mud-avatar.mud-success-text");

    /// <summary>Check if a provider avatar has the error (red) color class.</summary>
    public ILocator ErrorAvatars => Page.Locator("[data-testid='provider-card'] .mud-avatar.mud-error-text");

    // ── Model ID cells with monospace font ──
    public ILocator ModelIdCells => Page.Locator("[data-testid='provider-models'] tbody td:first-child .mud-typography");

    // ── Empty state ──
    public ILocator EmptyState => Page.Locator("[data-testid='providers-empty']");
    public ILocator EmptyStateText =>
        Page.Locator("[data-testid='providers-empty'] >> text=No providers configured");
}
