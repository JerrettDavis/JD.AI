using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Base page object providing shared navigation and wait helpers
/// for the Blazor WASM dashboard.
/// </summary>
public abstract class BasePage
{
    protected IPage Page { get; }
    protected string BaseUrl { get; }

    protected BasePage(IPage page, string baseUrl)
    {
        Page = page;
        BaseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>The route path relative to the base URL (e.g. "/agents").</summary>
    protected abstract string PagePath { get; }

    /// <summary>Navigate to this page.</summary>
    public async Task NavigateAsync()
    {
        await Page.GotoAsync($"{BaseUrl}{PagePath}");
    }

    /// <summary>
    /// Wait for the Blazor WASM app to finish loading.
    /// This waits for the network to be idle and the DOM to settle.
    /// </summary>
    public async Task WaitForLoadAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Give Blazor WASM a moment to finish rendering after load
        await Page.WaitForTimeoutAsync(500);
    }

    /// <summary>Page title element (h4 header).</summary>
    public ILocator PageTitle => Page.Locator("[data-testid='page-title']");

    /// <summary>Refresh / reload button common to most pages.</summary>
    public ILocator RefreshButton => Page.Locator("[data-testid='refresh-button']");

    /// <summary>The nav menu sidebar.</summary>
    public ILocator NavMenu => Page.Locator("[data-testid='nav-menu']");

    /// <summary>The top application bar.</summary>
    public ILocator AppBar => Page.Locator("[data-testid='app-bar']");

    /// <summary>The connection status chip in the app bar.</summary>
    public ILocator ConnectionStatus => Page.Locator("[data-testid='connection-status']");

    /// <summary>The hamburger menu button to toggle the drawer.</summary>
    public ILocator HamburgerMenu => Page.Locator("[data-testid='hamburger-menu']");

    /// <summary>The sidebar drawer.</summary>
    public ILocator Drawer => Page.Locator("[data-testid='drawer']");

    /// <summary>
    /// Parameterized nav link locator by link name (e.g. "overview", "chat", "agents").
    /// </summary>
    public ILocator NavLink(string name) => Page.Locator($"[data-testid='nav-{name}']");

    /// <summary>
    /// Get the browser page title via Playwright's TitleAsync().
    /// </summary>
    public async Task<string> GetPageTitleTextAsync() => await Page.TitleAsync();
}
