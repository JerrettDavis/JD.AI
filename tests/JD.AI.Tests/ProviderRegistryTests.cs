using JD.AI.Tui.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tui.Tests;

public sealed class ProviderRegistryTests
{
    private readonly IProviderDetector _detector1 = Substitute.For<IProviderDetector>();
    private readonly IProviderDetector _detector2 = Substitute.For<IProviderDetector>();

    public ProviderRegistryTests()
    {
        _detector1.ProviderName.Returns("Provider1");
        _detector2.ProviderName.Returns("Provider2");
    }

    [Fact]
    public async Task DetectProvidersAsync_ReturnsAllProviders()
    {
        _detector1.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo("Provider1", true, "OK", [
                new ProviderModelInfo("model-a", "Model A", "Provider1"),
            ]));
        _detector2.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo("Provider2", false, "Not running", []));

        var registry = new ProviderRegistry([_detector1, _detector2]);
        var providers = await registry.DetectProvidersAsync();

        Assert.Equal(2, providers.Count);
        Assert.True(providers[0].IsAvailable);
        Assert.False(providers[1].IsAvailable);
    }

    [Fact]
    public async Task GetModelsAsync_ReturnsOnlyAvailableProviderModels()
    {
        _detector1.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo("Provider1", true, "OK", [
                new ProviderModelInfo("m1", "M1", "Provider1"),
                new ProviderModelInfo("m2", "M2", "Provider1"),
            ]));
        _detector2.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo("Provider2", false, "Down", []));

        var registry = new ProviderRegistry([_detector1, _detector2]);
        var models = await registry.GetModelsAsync();

        Assert.Equal(2, models.Count);
        Assert.All(models, m => Assert.Equal("Provider1", m.ProviderName));
    }

    [Fact]
    public async Task DetectProvidersAsync_HandlesDetectorExceptions()
    {
        _detector1.DetectAsync(Arg.Any<CancellationToken>())
            .Returns<ProviderInfo>(_ => throw new InvalidOperationException("boom"));
        _detector2.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo("Provider2", true, "OK", []));

        var registry = new ProviderRegistry([_detector1, _detector2]);
        var providers = await registry.DetectProvidersAsync();

        Assert.Equal(2, providers.Count);
        Assert.False(providers[0].IsAvailable);
        Assert.Contains("boom", providers[0].StatusMessage);
        Assert.True(providers[1].IsAvailable);
    }

    [Fact]
    public void BuildKernel_DelegatesToCorrectDetector()
    {
        var expectedKernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("m1", "M1", "Provider1");
        _detector1.BuildKernel(model).Returns(expectedKernel);

        var registry = new ProviderRegistry([_detector1, _detector2]);
        var kernel = registry.BuildKernel(model);

        Assert.Same(expectedKernel, kernel);
    }

    [Fact]
    public void BuildKernel_ThrowsForUnknownProvider()
    {
        var model = new ProviderModelInfo("m1", "M1", "UnknownProvider");
        var registry = new ProviderRegistry([_detector1, _detector2]);

        Assert.Throws<InvalidOperationException>(() => registry.BuildKernel(model));
    }

    [Fact]
    public async Task GetModelsAsync_CachesDetectionResults()
    {
        _detector1.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo("Provider1", true, "OK", [
                new ProviderModelInfo("m1", "M1", "Provider1"),
            ]));

        var registry = new ProviderRegistry([_detector1]);

        // First call detects
        await registry.DetectProvidersAsync();
        // Second call should use cached
        var models = await registry.GetModelsAsync();

        Assert.Single(models);
        await _detector1.Received(1).DetectAsync(Arg.Any<CancellationToken>());
    }
}
