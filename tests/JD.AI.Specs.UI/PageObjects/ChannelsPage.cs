using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Channels page.
/// Route: /channels
/// </summary>
public sealed class ChannelsPage : BasePage
{
    public ChannelsPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/channels";

    // ── Navigation helpers ──
    public async Task NavigateToChannels() => await NavigateAsync();

    // ── Page header ──
    public ILocator PageHeading => Page.Locator("[data-testid='page-title']");
    public ILocator SyncOpenClawButton => Page.Locator("[data-testid='sync-openclaw-button']");

    // ── Channel cards ──
    public ILocator ChannelCards => Page.Locator("[data-testid='channel-card']");
    public ILocator ChannelNames => Page.Locator("[data-testid='channel-name']");
    public ILocator ChannelStatusBadges => Page.Locator("[data-testid='channel-status']");

    // ── Channel card actions ──
    public ILocator ConnectButtons => Page.Locator("[data-testid='channel-connect-button']");
    public ILocator DisconnectButtons => Page.Locator("[data-testid='channel-disconnect-button']");
    public ILocator OverrideButtons => Page.Locator("[data-testid='channel-override-button']");

    // ── Empty state ──
    public ILocator EmptyState => Page.Locator("[data-testid='channels-empty']");
    public ILocator EmptyStateText =>
        Page.Locator("[data-testid='channels-empty'] >> text=No channels configured");

    // ── Override dialog ──
    public ILocator OverrideDialog => Page.Locator(".mud-dialog");
}
