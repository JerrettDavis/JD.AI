using FluentAssertions;
using JD.AI.Core.Providers;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ModelSearchSteps
{
    private readonly ScenarioContext _context;

    public ModelSearchSteps(ScenarioContext context) => _context = context;

    [When(@"models are filtered by provider ""(.*)""")]
    public async Task WhenModelsAreFilteredByProvider(string providerName)
    {
        var registry = _context.Get<IProviderRegistry>();
        var allModels = await registry.GetModelsAsync();
        var filtered = allModels.Where(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)).ToList();
        _context.Set<IReadOnlyList<ProviderModelInfo>>(filtered, "filteredModels");
    }

    [Then(@"the filtered model list should contain (\d+) models")]
    public void ThenTheFilteredModelListShouldContainModels(int count)
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("filteredModels");
        models.Should().HaveCount(count);
    }

    [Then(@"all filtered models should be from provider ""(.*)""")]
    public void ThenAllFilteredModelsShouldBeFromProvider(string providerName)
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("filteredModels");
        models.Should().OnlyContain(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
    }

    [Then(@"the filtered model list should be empty")]
    public void ThenTheFilteredModelListShouldBeEmpty()
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("filteredModels");
        models.Should().BeEmpty();
    }

    [Then(@"each model should have a non-empty ID")]
    public void ThenEachModelShouldHaveANonEmptyId()
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("modelCatalog");
        models.Should().OnlyContain(m => !string.IsNullOrEmpty(m.Id));
    }

    [Then(@"each model should have a non-empty display name")]
    public void ThenEachModelShouldHaveANonEmptyDisplayName()
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("modelCatalog");
        models.Should().OnlyContain(m => !string.IsNullOrEmpty(m.DisplayName));
    }

    [Then(@"each model should have a non-empty provider name")]
    public void ThenEachModelShouldHaveANonEmptyProviderName()
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("modelCatalog");
        models.Should().OnlyContain(m => !string.IsNullOrEmpty(m.ProviderName));
    }

    [Then(@"each model should have a positive context window size")]
    public void ThenEachModelShouldHaveAPositiveContextWindowSize()
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("modelCatalog");
        models.Should().OnlyContain(m => m.ContextWindowTokens > 0);
    }

    [Then(@"each model should have capabilities flags")]
    public void ThenEachModelShouldHaveCapabilitiesFlags()
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("modelCatalog");
        models.Should().OnlyContain(m => m.Capabilities != ModelCapabilities.None);
    }
}
