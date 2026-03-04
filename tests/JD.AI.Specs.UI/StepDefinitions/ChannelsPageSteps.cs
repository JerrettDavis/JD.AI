using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class ChannelsPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private ChannelsPage _channelsPage = null!;

    public ChannelsPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupChannelsPage()
    {
        _page = _context.Get<IPage>();
        _channelsPage = new ChannelsPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the channels page")]
    public async Task GivenIAmOnTheChannelsPage()
    {
        await _channelsPage.NavigateToChannels();
        await _channelsPage.WaitForLoadAsync();
    }

    [Then(@"I should see the channels page heading ""(.*)""")]
    public async Task ThenIShouldSeeTheChannelsPageHeading(string heading)
    {
        var headingLocator = _page.Locator($"text={heading}").First;
        await Expect(headingLocator).ToBeVisibleAsync();
    }

    [Then(@"I should see the sync OpenClaw button")]
    public async Task ThenIShouldSeeTheSyncOpenClawButton()
    {
        var syncButton = _page.Locator("button:has-text('Sync OpenClaw')");
        await Expect(syncButton).ToBeVisibleAsync();
    }

    [Given(@"there are configured channels")]
    public async Task GivenThereAreConfiguredChannels()
    {
        // Wait for channel data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see channel cards")]
    public async Task ThenIShouldSeeChannelCards()
    {
        var cards = _page.Locator(".mud-card");
        var count = await cards.CountAsync();
        Assert.True(count > 0, "Expected at least one channel card");
    }

    [Then(@"each channel card should display a name")]
    public async Task ThenEachChannelCardShouldDisplayAName()
    {
        var cards = _page.Locator(".mud-card");
        var count = await cards.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var nameElement = cards.Nth(i).Locator(".mud-card-header-content");
            await Expect(nameElement).ToBeVisibleAsync();
            var text = await nameElement.TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Channel card {i} should have a display name");
        }
    }

    [Then(@"each channel card should show a status badge")]
    public async Task ThenEachChannelCardShouldShowAStatusBadge()
    {
        var chips = _page.Locator(".mud-card .mud-chip");
        var count = await chips.CountAsync();
        Assert.True(count > 0, "Expected status badges on channel cards");
    }

    [Then(@"status badges should display ""Online"" or ""Offline""")]
    public async Task ThenStatusBadgesShouldDisplayOnlineOrOffline()
    {
        var chips = _page.Locator(".mud-card .mud-card-header .mud-chip");
        var count = await chips.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var text = await chips.Nth(i).TextContentAsync();
            Assert.NotNull(text);
            Assert.True(
                text.Contains("Online", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Offline", StringComparison.OrdinalIgnoreCase),
                $"Expected status badge to show 'Online' or 'Offline', got '{text}'");
        }
    }

    [Then(@"each channel card should have an override button")]
    public async Task ThenEachChannelCardShouldHaveAnOverrideButton()
    {
        var overrideButtons = _page.Locator(".mud-card button:has-text('Override')");
        var count = await overrideButtons.CountAsync();
        Assert.True(count > 0, "Expected override buttons on channel cards");
    }
}
