using JD.AI.Tui.Providers;
using Xunit;

namespace JD.AI.Tui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ProviderDetectionTests
{
    [SkippableFact]
    public async Task OllamaDetector_DetectsRunningInstance()
    {
        TuiIntegrationGuard.EnsureEnabled();
        await TuiIntegrationGuard.EnsureOllamaAsync();

        var detector = new OllamaDetector();
        var result = await detector.DetectAsync();

        Assert.True(result.IsAvailable);
        Assert.NotEmpty(result.Models);
        Assert.All(result.Models, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Id));
            Assert.Equal("Ollama", m.ProviderName);
        });
    }

    [SkippableFact]
    public async Task OllamaDetector_HandlesUnavailableInstance()
    {
        TuiIntegrationGuard.EnsureEnabled();

        // Point to a port that's not running Ollama
        var detector = new OllamaDetector("http://localhost:59999");
        var result = await detector.DetectAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal("Not running", result.StatusMessage);
        Assert.Empty(result.Models);
    }

    [SkippableFact]
    public async Task OllamaDetector_BuildKernel_ProducesValidKernel()
    {
        TuiIntegrationGuard.EnsureEnabled();
        await TuiIntegrationGuard.EnsureOllamaAsync();

        var detector = new OllamaDetector();
        var info = await detector.DetectAsync();
        Skip.If(!info.IsAvailable || info.Models.Count == 0, "No Ollama models available");

        var kernel = detector.BuildKernel(info.Models[0]);

        Assert.NotNull(kernel);
        // Verify a chat completion service is registered
        var chatService = kernel.GetAllServices<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
        Assert.NotEmpty(chatService);
    }

    [SkippableFact]
    public async Task ClaudeCodeDetector_DetectsOrSkips()
    {
        TuiIntegrationGuard.EnsureEnabled();

        var detector = new ClaudeCodeDetector();
        var result = await detector.DetectAsync();

        // Either available with models or not available — both are valid
        if (result.IsAvailable)
        {
            Assert.NotEmpty(result.Models);
            Assert.All(result.Models, m => Assert.Equal("Claude Code", m.ProviderName));
        }
        else
        {
            Assert.Empty(result.Models);
        }
    }

    [SkippableFact]
    public async Task CopilotDetector_DetectsOrSkips()
    {
        TuiIntegrationGuard.EnsureEnabled();

        var detector = new CopilotDetector();
        var result = await detector.DetectAsync();

        if (result.IsAvailable)
        {
            Assert.NotEmpty(result.Models);
            Assert.All(result.Models, m => Assert.Equal("GitHub Copilot", m.ProviderName));
        }
        else
        {
            Assert.Empty(result.Models);
        }
    }

    [SkippableFact]
    public async Task ProviderRegistry_AggregatesAllProviders()
    {
        TuiIntegrationGuard.EnsureEnabled();

        var registry = new ProviderRegistry([
            new ClaudeCodeDetector(),
            new CopilotDetector(),
            new OllamaDetector(),
        ]);

        var providers = await registry.DetectProvidersAsync();

        Assert.Equal(3, providers.Count);
        Assert.Contains(providers, p => string.Equals(p.Name, "Claude Code", StringComparison.Ordinal));
        Assert.Contains(providers, p => string.Equals(p.Name, "GitHub Copilot", StringComparison.Ordinal));
        Assert.Contains(providers, p => string.Equals(p.Name, "Ollama", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task ProviderRegistry_GetModels_ReturnsUnifiedCatalog()
    {
        TuiIntegrationGuard.EnsureEnabled();
        await TuiIntegrationGuard.EnsureOllamaAsync();

        var registry = new ProviderRegistry([new OllamaDetector()]);
        var models = await registry.GetModelsAsync();

        Assert.NotEmpty(models);
        Assert.All(models, m => Assert.Equal("Ollama", m.ProviderName));
    }
}
