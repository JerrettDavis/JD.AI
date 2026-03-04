using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class ProvidersPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private ProvidersPage _providersPage = null!;

    public ProvidersPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupProvidersPage()
    {
        _page = _context.Get<IPage>();
        _providersPage = new ProvidersPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the providers page")]
    public async Task GivenIAmOnTheProvidersPage()
    {
        await _providersPage.NavigateToProviders();
        await _providersPage.WaitForLoadAsync();
    }

    [Then(@"I should see the providers page heading ""(.*)""")]
    public async Task ThenIShouldSeeTheProvidersPageHeading(string heading)
    {
        var headingLocator = _page.Locator($"text={heading}").First;
        await Expect(headingLocator).ToBeVisibleAsync();
    }

    [Then(@"I should see the providers refresh button")]
    public async Task ThenIShouldSeeTheProvidersRefreshButton()
    {
        // The refresh icon button on the providers page
        var refreshButton = _page.Locator(".jd-page-header button").Last;
        await Expect(refreshButton).ToBeVisibleAsync();
    }

    [Given(@"there are configured providers")]
    public async Task GivenThereAreConfiguredProviders()
    {
        // Wait for provider data to load from the API
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see provider cards")]
    public async Task ThenIShouldSeeProviderCards()
    {
        var cards = _page.Locator(".mud-card");
        var count = await cards.CountAsync();
        Assert.True(count > 0, "Expected at least one provider card");
    }

    [Then(@"each provider card should display a name")]
    public async Task ThenEachProviderCardShouldDisplayAName()
    {
        var cards = _page.Locator(".mud-card");
        var count = await cards.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var nameElement = cards.Nth(i).Locator(".mud-card-header-content");
            await Expect(nameElement).ToBeVisibleAsync();
            var text = await nameElement.TextContentAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Provider card {i} should have a name");
        }
    }

    [Then(@"each provider card should show an availability status")]
    public async Task ThenEachProviderCardShouldShowAnAvailabilityStatus()
    {
        var chips = _page.Locator(".mud-card .mud-chip");
        var count = await chips.CountAsync();
        Assert.True(count > 0, "Expected availability status chips on provider cards");
    }

    [Then(@"availability status should display ""Online"" or ""Offline""")]
    public async Task ThenAvailabilityStatusShouldDisplayOnlineOrOffline()
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
                $"Expected availability to show 'Online' or 'Offline', got '{text}'");
        }
    }

    [Given(@"there are no configured providers")]
    public async Task GivenThereAreNoConfiguredProviders()
    {
        // If the API returns no providers, empty state renders
        await _page.WaitForTimeoutAsync(500);
    }

    [Then(@"I should see the providers empty state")]
    public async Task ThenIShouldSeeTheProvidersEmptyState()
    {
        var emptyText = _page.Locator("text=No providers configured");
        await Expect(emptyText).ToBeVisibleAsync();
    }
}
