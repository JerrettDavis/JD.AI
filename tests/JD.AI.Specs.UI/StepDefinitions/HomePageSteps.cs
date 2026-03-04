using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class HomePageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private HomePage _homePage = null!;

    public HomePageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupHomePage()
    {
        _page = _context.Get<IPage>();
        _homePage = new HomePage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the home page")]
    public async Task GivenIAmOnTheHomePage()
    {
        await _homePage.NavigateToHome();
        await _homePage.WaitForLoadAsync();
    }

    [Then(@"I should see the gateway overview heading")]
    public async Task ThenIShouldSeeTheGatewayOverviewHeading()
    {
        var heading = _page.Locator("text=Gateway Overview");
        await Expect(heading).ToBeVisibleAsync();
    }

    [Then(@"I should see (\d+) stat cards")]
    public async Task ThenIShouldSeeStatCards(int count)
    {
        await Expect(_homePage.StatCards).ToHaveCountAsync(count);
    }

    [Then(@"I should see a stat card for ""(.*)""")]
    public async Task ThenIShouldSeeAStatCardFor(string label)
    {
        var card = _page.Locator($"[data-testid='stat-card'] >> text={label}");
        await Expect(card).ToBeVisibleAsync();
    }

    [Then(@"the ""(.*)"" stat card should display a count")]
    public async Task ThenTheStatCardShouldDisplayACount(string cardName)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase -- data-testid convention is lowercase
        var valueLocator = _homePage.StatCardValue(cardName.ToLowerInvariant());
#pragma warning restore CA1308
        await Expect(valueLocator).ToBeVisibleAsync();
        var text = await valueLocator.TextContentAsync();
        Assert.NotNull(text);
        Assert.Matches(@"\d+", text);
    }

    [Then(@"I should see the recent activity section")]
    public async Task ThenIShouldSeeTheRecentActivitySection()
    {
        await Expect(_homePage.RecentActivitySection).ToBeVisibleAsync();
    }

    [Then(@"the recent activity section should have a title ""(.*)""")]
    public async Task ThenTheRecentActivitySectionShouldHaveATitle(string title)
    {
        var titleLocator = _page.Locator($"[data-testid='recent-activity'] >> text={title}");
        await Expect(titleLocator).ToBeVisibleAsync();
    }

    [When(@"I click the ""(.*)"" navigation link")]
    public async Task WhenIClickTheNavigationLink(string linkText)
    {
        var link = _page.Locator($"a:has-text('{linkText}')");
        await link.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    [Then(@"I should be on the ""(.*)"" page")]
    public async Task ThenIShouldBeOnThePage(string path)
    {
        await _page.WaitForURLAsync($"**{path}");
        Assert.Contains(path, _page.Url);
    }
}
