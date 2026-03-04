using FluentAssertions;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ProviderDetectionSteps
{
    private readonly ScenarioContext _context;

    public ProviderDetectionSteps(ScenarioContext context) => _context = context;

    [Given(@"a provider registry with detectors for ""([^""]*)"" and ""([^""]*)""")]
    public void GivenAProviderRegistryWithDetectorsForTwo(string provider1, string provider2)
    {
        SetupRegistry([provider1, provider2]);
    }

    [Given(@"a provider registry with detectors for ""([^""]*)""$")]
    public void GivenAProviderRegistryWithDetectorsForSingle(string providerName)
    {
        SetupRegistry([providerName]);
    }

    private void SetupRegistry(IReadOnlyList<string> providerNames)
    {
        var detectors = new Dictionary<string, IProviderDetector>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in providerNames)
        {
            var detector = Substitute.For<IProviderDetector>();
            detector.ProviderName.Returns(name);
            detector.BuildKernel(Arg.Any<ProviderModelInfo>())
                .Returns(_ => Kernel.CreateBuilder().Build());
            detectors[name] = detector;
        }

        _context.Set(detectors, "detectors");
        var registry = new ProviderRegistry(detectors.Values);
        _context.Set<IProviderRegistry>(registry);
    }

    [Given(@"an empty provider registry")]
    public void GivenAnEmptyProviderRegistry()
    {
        _context.Set(new Dictionary<string, IProviderDetector>(StringComparer.OrdinalIgnoreCase), "detectors");
        var registry = new ProviderRegistry([]);
        _context.Set<IProviderRegistry>(registry);
    }

    [Given(@"the ""(.*)"" detector reports as available with models (.*)")]
    public void GivenTheDetectorReportsAsAvailableWithModels(string providerName, string modelsSpec)
    {
        var detectors = _context.Get<Dictionary<string, IProviderDetector>>("detectors");
        var detector = detectors[providerName];

        var modelNames = modelsSpec.Split(", ").Select(m => m.Trim('"')).ToList();
        var models = modelNames.Select(m =>
            new ProviderModelInfo(m, m, providerName)).ToList();

        detector.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProviderInfo(
                providerName, IsAvailable: true, StatusMessage: "OK", Models: models)));

        detector.BuildKernel(Arg.Any<ProviderModelInfo>())
            .Returns(_ => Kernel.CreateBuilder().Build());
    }

    [Given(@"the ""(.*)"" detector reports as unavailable with message ""(.*)""")]
    public void GivenTheDetectorReportsAsUnavailable(string providerName, string message)
    {
        var detectors = _context.Get<Dictionary<string, IProviderDetector>>("detectors");
        var detector = detectors[providerName];

        detector.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProviderInfo(
                providerName, IsAvailable: false, StatusMessage: message, Models: [])));
    }

    [Given(@"the ""(.*)"" detector throws an exception")]
    public void GivenTheDetectorThrowsAnException(string providerName)
    {
        var detectors = _context.Get<Dictionary<string, IProviderDetector>>("detectors");
        var detector = detectors[providerName];

        detector.DetectAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Detection failed"));
    }

    [When(@"providers are detected")]
    public async Task WhenProvidersAreDetected()
    {
        var registry = _context.Get<IProviderRegistry>();
        var providers = await registry.DetectProvidersAsync();
        _context.Set(providers, "detectedProviders");
    }

    [When(@"the model catalog is retrieved")]
    public async Task WhenTheModelCatalogIsRetrieved()
    {
        var registry = _context.Get<IProviderRegistry>();
        var models = await registry.GetModelsAsync();
        _context.Set(models, "modelCatalog");
    }

    [Then("the detected providers should include \"(.*)\"")]
    public void ThenTheDetectedProvidersShouldInclude(string providerName)
    {
        var providers = _context.Get<IReadOnlyList<ProviderInfo>>("detectedProviders");
        providers.Should().Contain(p => p.Name == providerName);
    }

    [Then(@"the detected providers should include ""(.*)"" as available")]
    public void ThenTheDetectedProvidersShouldIncludeAsAvailable(string providerName)
    {
        var providers = _context.Get<IReadOnlyList<ProviderInfo>>("detectedProviders");
        providers.Should().Contain(p => string.Equals(p.Name, providerName, StringComparison.Ordinal) && p.IsAvailable);
    }

    [Then(@"the detected providers should include ""(.*)"" as unavailable")]
    public void ThenTheDetectedProvidersShouldIncludeAsUnavailable(string providerName)
    {
        var providers = _context.Get<IReadOnlyList<ProviderInfo>>("detectedProviders");
        providers.Should().Contain(p => string.Equals(p.Name, providerName, StringComparison.Ordinal) && !p.IsAvailable);
    }

    [Then(@"the ""(.*)"" provider should have (\d+) models")]
    public void ThenTheProviderShouldHaveModels(string providerName, int count)
    {
        var providers = _context.Get<IReadOnlyList<ProviderInfo>>("detectedProviders");
        var provider = providers.First(p => string.Equals(p.Name, providerName, StringComparison.Ordinal));
        provider.Models.Should().HaveCount(count);
    }

    [Then(@"the catalog should contain (\d+) models")]
    public void ThenTheCatalogShouldContainModels(int count)
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("modelCatalog");
        models.Should().HaveCount(count);
    }

    [Then(@"the catalog should include model ""(.*)"" from provider ""(.*)""")]
    public void ThenTheCatalogShouldIncludeModelFromProvider(string modelId, string providerName)
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("modelCatalog");
        models.Should().Contain(m => m.Id == modelId && m.ProviderName == providerName);
    }

    [Then(@"the detected providers list should be empty")]
    public void ThenTheDetectedProvidersListShouldBeEmpty()
    {
        var providers = _context.Get<IReadOnlyList<ProviderInfo>>("detectedProviders");
        providers.Should().BeEmpty();
    }
}
