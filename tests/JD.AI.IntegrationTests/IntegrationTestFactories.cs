using JD.AI.Core.Providers;
using JD.AI.Workflows;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.IntegrationTests;

internal static class IntegrationTestFactories
{
    public static HeadlessAgentIntegrationHarness CreateOllamaHarness()
    {
        var model = new ProviderModelInfo(IntegrationTestGuard.OllamaModel, "Ollama Chat", "Ollama");
        var detector = new OllamaDetector();
        return HeadlessAgentIntegrationHarness.Create(detector, model);
    }

    public static Kernel CreateOllamaWorkflowKernel() =>
        Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: IntegrationTestGuard.OllamaModel,
                apiKey: "ollama",
                endpoint: new Uri($"{IntegrationTestGuard.OllamaEndpoint}/v1"))
            .Build();
}

internal sealed class TempTestDirectory : IDisposable
{
    public string Path { get; }

    public TempTestDirectory(string prefix)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public FileWorkflowCatalog CreateWorkflowCatalog() => new(Path);

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
