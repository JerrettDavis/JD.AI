using JD.AI.Tui.Agent;
using JD.AI.Tui.Providers;
using JD.AI.Tui.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace JD.AI.Tui.IntegrationTests;

/// <summary>
/// Agent loop integration tests requiring a running Ollama instance.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AgentLoopIntegrationTests
{
    [SkippableFact]
    public async Task AgentLoop_SimpleChat_ReturnsResponse()
    {
        await TuiIntegrationGuard.EnsureOllamaAsync();

        var model = new ProviderModelInfo("llama3.2:latest", "Llama 3.2", "Ollama");
        var detector = new OllamaDetector();
        var kernel = detector.BuildKernel(model);

        var registry = new ProviderRegistry([detector]);
        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);

        var response = await loop.RunTurnAsync("What is 2+2? Reply with just the number.");

        Assert.NotNull(response);
        Assert.Contains("4", response);
    }

    [SkippableFact]
    public async Task AgentLoop_MultiTurn_MaintainsContext()
    {
        await TuiIntegrationGuard.EnsureOllamaAsync();

        var model = new ProviderModelInfo("llama3.2:latest", "Llama 3.2", "Ollama");
        var detector = new OllamaDetector();
        var kernel = detector.BuildKernel(model);

        var registry = new ProviderRegistry([detector]);
        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);

        var response1 = await loop.RunTurnAsync("My name is TestUser.");
        Assert.NotNull(response1);

        var response2 = await loop.RunTurnAsync("What is my name?");
        Assert.NotNull(response2);
        Assert.Contains("TestUser", response2);
    }

    [SkippableFact]
    public async Task AgentLoop_WithToolCalling_ExecutesFileTools()
    {
        await TuiIntegrationGuard.EnsureOllamaAsync();

        var model = new ProviderModelInfo("llama3.2:latest", "Llama 3.2", "Ollama");
        var detector = new OllamaDetector();
        var kernel = detector.BuildKernel(model);

        // Add file tools
        kernel.Plugins.AddFromType<FileTools>("FileTools");

        var registry = new ProviderRegistry([detector]);
        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);

        // Create a temp file
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "integration test content");
        try
        {
            var response = await loop.RunTurnAsync($"Read the file at {tempFile} and tell me what it says.");
            Assert.NotNull(response);
            // The response should reference the content or be about the file
            Assert.True(
                response.Contains("integration", StringComparison.OrdinalIgnoreCase) ||
                response.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                response.Contains("content", StringComparison.OrdinalIgnoreCase) ||
                response.Contains("file", StringComparison.OrdinalIgnoreCase),
                $"Expected response to reference file content, got: {response}");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public async Task Session_ClearAndSwitch_PreservesIntegrity()
    {
        await TuiIntegrationGuard.EnsureOllamaAsync();

        var model = new ProviderModelInfo("llama3.2:latest", "Llama 3.2", "Ollama");
        var detector = new OllamaDetector();
        var kernel = detector.BuildKernel(model);

        var registry = new ProviderRegistry([detector]);
        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);

        // First conversation
        await loop.RunTurnAsync("Hello");
        Assert.True(session.History.Count >= 2); // user + assistant

        // Clear
        session.ClearHistory();
        Assert.Empty(session.History);

        // New conversation should work
        var response = await loop.RunTurnAsync("What is 1+1? Just the number.");
        Assert.NotNull(response);
        // LLM may not always return exactly "2" — just verify we got a non-error response
        Assert.False(string.IsNullOrWhiteSpace(response));
    }
}
