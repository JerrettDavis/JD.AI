using FluentAssertions;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ProviderRegistrySteps
{
    private readonly ScenarioContext _context;

    public ProviderRegistrySteps(ScenarioContext context) => _context = context;

    [When(@"the detector for ""(.*)"" is requested")]
    public void WhenTheDetectorForIsRequested(string providerName)
    {
        var registry = _context.Get<IProviderRegistry>();
        var detector = registry.GetDetector(providerName);
        _context.Set(detector, "requestedDetector");
    }

    [When(@"a kernel is built for model ""(.*)"" from provider ""(.*)""")]
    public void WhenAKernelIsBuiltForModel(string modelId, string providerName)
    {
        var registry = _context.Get<IProviderRegistry>();
        var model = new ProviderModelInfo(modelId, modelId, providerName);
        var kernel = registry.BuildKernel(model);
        _context.Set(kernel, "builtKernel");
    }

    [When(@"all available models are listed")]
    public async Task WhenAllAvailableModelsAreListed()
    {
        var registry = _context.Get<IProviderRegistry>();
        var models = await registry.GetModelsAsync();
        _context.Set(models, "modelList");
    }

    [When(@"building a kernel for an unregistered provider ""(.*)"" with model ""(.*)""")]
    public void WhenBuildingAKernelForAnUnregisteredProvider(string providerName, string modelId)
    {
        var registry = _context.Get<IProviderRegistry>();
        var model = new ProviderModelInfo(modelId, modelId, providerName);

        try
        {
            registry.BuildKernel(model);
        }
        catch (InvalidOperationException ex)
        {
            _context.Set(ex, "thrownInvalidOperationException");
        }
    }

    [Then(@"the detector should not be null")]
    public void ThenTheDetectorShouldNotBeNull()
    {
        var detector = _context.Get<IProviderDetector?>("requestedDetector");
        detector.Should().NotBeNull();
    }

    [Then(@"the detector should be null")]
    public void ThenTheDetectorShouldBeNull()
    {
        _context.TryGetValue<IProviderDetector?>("requestedDetector", out var detector);
        detector.Should().BeNull();
    }

    [Then(@"the detector provider name should be ""(.*)""")]
    public void ThenTheDetectorProviderNameShouldBe(string providerName)
    {
        var detector = _context.Get<IProviderDetector?>("requestedDetector");
        detector!.ProviderName.Should().Be(providerName);
    }

    [Then(@"the kernel should not be null")]
    public void ThenTheKernelShouldNotBeNull()
    {
        var kernel = _context.Get<Kernel>("builtKernel");
        kernel.Should().NotBeNull();
    }

    [Then(@"the model list should contain (\d+) models")]
    public void ThenTheModelListShouldContainModels(int count)
    {
        var models = _context.Get<IReadOnlyList<ProviderModelInfo>>("modelList");
        models.Should().HaveCount(count);
    }

    [Then(@"an InvalidOperationException should be thrown")]
    public void ThenAnInvalidOperationExceptionShouldBeThrown()
    {
        _context.TryGetValue<InvalidOperationException>("thrownInvalidOperationException", out var ex)
            .Should().BeTrue("an InvalidOperationException should have been thrown");
        ex.Should().NotBeNull();
    }
}
