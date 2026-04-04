using FluentAssertions;
using JD.AI.Core.LocalModels;
using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Providers;

public sealed class ProviderServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDefaultProviderRegistry_RegistersExpectedDetectorsAndRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDefaultProviderRegistry();

        using var provider = services.BuildServiceProvider();
        var detectors = provider.GetServices<IProviderDetector>().Select(d => d.GetType()).ToArray();

        detectors.Should().Contain(typeof(ClaudeCodeDetector));
        detectors.Should().Contain(typeof(CopilotDetector));
        detectors.Should().Contain(typeof(OpenAICodexDetector));
        detectors.Should().Contain(typeof(OllamaDetector));
        detectors.Should().Contain(typeof(FoundryLocalDetector));
        detectors.Should().Contain(typeof(LocalModelDetector));
        provider.GetRequiredService<IProviderRegistry>().Should().NotBeNull();
    }
}
