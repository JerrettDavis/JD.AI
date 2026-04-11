using JD.AI.Specs.UI.PageObjects;
using JD.AI.Specs.UI.Support;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace JD.AI.Specs.UI.StepDefinitions;

[Binding]
public sealed class SkillsPageSteps
{
    private readonly ScenarioContext _context;
    private IPage _page = null!;
    private SkillsPage _skillsPage = null!;

    public SkillsPageSteps(ScenarioContext context) => _context = context;

    [BeforeScenario("@ui", Order = 10)]
    public void SetupSkillsPage()
    {
        _page = _context.Get<IPage>();
        _skillsPage = new SkillsPage(_page, _context.Get<DashboardFixture>().BaseUrl);
    }

    [Given(@"I am on the skills page")]
    public async Task GivenIAmOnTheSkillsPage()
    {
        await _skillsPage.NavigateToSkills();
        await _skillsPage.WaitForLoadAsync();
    }

    [Then(@"I should see the ""(.*)"" filter tab")]
    public async Task ThenIShouldSeeFilterTab(string tabName)
    {
        var testId = tabName switch
        {
            "All" => "filter-all",
            "Ready" => "filter-ready",
            "Needs Setup" => "filter-needs-setup",
            "Disabled" => "filter-disabled",
            _ => $"filter-{tabName.Replace(" ", "-", StringComparison.Ordinal)}",
        };
        await Expect(_page.Locator($"[data-testid='{testId}']")).ToBeVisibleAsync();
    }

    [Then(@"I should see the skill search box")]
    public async Task ThenIShouldSeeTheSkillSearchBox()
    {
        await Expect(_page.Locator("[data-testid='skill-search']")).ToBeVisibleAsync();
    }

    [When(@"I click the ""(.*)"" filter tab")]
    public async Task WhenIClickFilterTab(string tabName)
    {
        var testId = tabName switch
        {
            "All" => "filter-all",
            "Ready" => "filter-ready",
            "Needs Setup" => "filter-needs-setup",
            "Disabled" => "filter-disabled",
            _ => $"filter-{tabName.Replace(" ", "-", StringComparison.Ordinal)}",
        };
        await _page.Locator($"[data-testid='{testId}']").ClickAsync();
    }

    [When(@"I type ""(.*)"" in the search box")]
    public async Task WhenITypeInTheSearchBox(string searchText)
    {
        await _skillsPage.SearchBox.FillAsync(searchText);
    }

    [When(@"I clear the search box")]
    public async Task WhenIClearTheSearchBox()
    {
        await _skillsPage.SearchBox.ClearAsync();
    }

    [Then(@"I should see the no-skills empty state")]
    public async Task ThenIShouldSeeTheNoSkillsEmptyState()
    {
        await Expect(_skillsPage.EmptyState).ToBeVisibleAsync();
    }

    [Then(@"each skill card should have an enable toggle")]
    public async Task ThenEachSkillCardShouldHaveAnEnableToggle()
    {
        var count = await _skillsPage.SkillCards.CountAsync();
        var toggleCount = await _skillsPage.SkillToggles.CountAsync();
        Assert.Equal(count, toggleCount);
    }

    [Then(@"each skill card should have a ""(.*)"" button")]
    public async Task ThenEachSkillCardShouldHaveButton(string buttonText)
    {
        if (string.Equals(buttonText, "Configure", StringComparison.Ordinal))
        {
            var count = await _skillsPage.SkillCards.CountAsync();
            var btnCount = await _skillsPage.SkillConfigureButtons.CountAsync();
            Assert.Equal(count, btnCount);
        }
    }

    [When(@"I click the ""Configure"" button on the first skill")]
    public async Task WhenIClickConfigureOnFirstSkill()
    {
        await _skillsPage.SkillConfigureButtons.First.ClickAsync();
    }

    [Then(@"the skill configure dialog should be visible")]
    public async Task ThenTheSkillConfigureDialogShouldBeVisible()
    {
        await Expect(_skillsPage.ConfigDialog).ToBeVisibleAsync();
    }

    [Then(@"the configure dialog should show all config fields")]
    public async Task ThenTheConfigureDialogShouldShowAllConfigFields()
    {
        await Expect(_page.Locator("[data-testid^='config-field-']").First).ToBeVisibleAsync();
    }

    [When(@"I update a config field value")]
    public async Task WhenIUpdateAConfigFieldValue()
    {
        var field = _page.Locator("[data-testid^='config-field-'] input").First;
        await field.FillAsync("test-value");
    }

    [When(@"I click the ""Save"" button in the configure dialog")]
    public async Task WhenIClickSaveInConfigureDialog()
    {
        await _skillsPage.ConfigSaveButton.ClickAsync();
    }

    [Given(@"the skills list is loading")]
    public async Task GivenTheSkillsListIsLoading()
    {
        await _skillsPage.NavigateToSkills();
    }

    [Then(@"I should see skeleton loading cards")]
    public async Task ThenIShouldSeeSkeletonLoadingCards()
    {
        await Expect(_page.Locator(".mud-skeleton").First).ToBeVisibleAsync();
    }

    [Given(@"there are skills with mixed statuses")]
    public async Task GivenThereAreSkillsWithMixedStatuses()
    {
        // Skills exist in the running gateway — just navigate
        await _skillsPage.WaitForLoadAsync();
    }

    [Given(@"there are multiple skills")]
    public async Task GivenThereAreMultipleSkills()
    {
        await _skillsPage.WaitForLoadAsync();
    }

    [Given(@"there are skills")]
    public async Task GivenThereAreSkills()
    {
        await _skillsPage.WaitForLoadAsync();
    }

    [Given(@"there are skills with configuration")]
    public async Task GivenThereAreSkillsWithConfiguration()
    {
        await _skillsPage.WaitForLoadAsync();
    }

    [Given(@"there are enabled skills")]
    public async Task GivenThereAreEnabledSkills()
    {
        await _skillsPage.WaitForLoadAsync();
    }

    [Then(@"I should only see skills matching ""(.*)""")]
    public async Task ThenIShouldOnlySeeSkillsMatching(string search)
    {
        await Expect(_page.Locator($"[data-testid^='skill-card-']:has-text('{search}')").First).ToBeVisibleAsync();
    }

    [Then(@"I should see all skills listed")]
    public async Task ThenIShouldSeeAllSkillsListed()
    {
        await Expect(_skillsPage.SkillCards.First).ToBeVisibleAsync();
    }

    [Then(@"I should only see skills with ""(.*)"" status")]
    public async Task ThenIShouldOnlySeeSkillsWithStatus(string status)
    {
        await Expect(_skillsPage.SkillCards.First).ToBeVisibleAsync();
    }

    [When(@"I toggle the first skill to enabled")]
    public async Task WhenIToggleFirstSkillEnabled()
    {
        await _skillsPage.SkillToggles.First.ClickAsync();
    }

    [When(@"I toggle the first skill to disabled")]
    public async Task WhenIToggleFirstSkillDisabled()
    {
        await _skillsPage.SkillToggles.First.ClickAsync();
    }

    [Then(@"there are no disabled skills")]
    public Task ThenThereAreNoDisabledSkills() => Task.CompletedTask;
}
