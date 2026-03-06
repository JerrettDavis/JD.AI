using JD.AI.Core.Providers;
using JD.AI.Startup;

namespace JD.AI.Tests;

public sealed class ProviderOrchestratorSelectionTests
{
    [Fact]
    public void EvaluateSelection_CliModelTakesHighestPrecedence()
    {
        var models = CreateModels();
        var opts = new CliOptions
        {
            CliModel = "sonnet",
            CliProvider = "claudecode",
            PrintMode = true,
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: "Ollama",
            defaultModel: "llama");

        Assert.True(decision.Handled);
        Assert.Null(decision.ErrorMessage);
        Assert.Equal("claude-sonnet", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_CliProviderUsesPromptWhenInteractive()
    {
        var models = CreateModels();
        var opts = new CliOptions { CliProvider = "ollama", PrintMode = false };
        var promptInvoked = false;

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: null,
            defaultModel: null,
            promptSelector: candidates =>
            {
                promptInvoked = true;
                return candidates[^1];
            });

        Assert.True(promptInvoked);
        Assert.Equal("ollama-coder", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_DefaultModelPrecedesPrintModeFallback()
    {
        var models = CreateModels();
        var opts = new CliOptions { PrintMode = true };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: "claudecode",
            defaultModel: "haiku");

        Assert.Equal("claude-haiku", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_DefaultProviderAppliedWhenDefaultModelMissing()
    {
        var models = CreateModels();
        var opts = new CliOptions { PrintMode = false };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: "ollama",
            defaultModel: null,
            promptSelector: _ => throw new InvalidOperationException("Prompt should not be used."));

        Assert.Equal("ollama-chat", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_PrintModeFallsBackToFirstModel()
    {
        var models = CreateModels();
        var opts = new CliOptions { PrintMode = true };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: null,
            defaultModel: null);

        Assert.Equal("claude-sonnet", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_CliModelNotFoundReturnsError()
    {
        var models = CreateModels();
        var opts = new CliOptions { CliModel = "does-not-exist" };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: null,
            defaultModel: null);

        Assert.True(decision.Handled);
        Assert.Null(decision.SelectedModel);
        Assert.Contains("No model matching", decision.ErrorMessage);
    }

    private static List<ProviderModelInfo> CreateModels() =>
    [
        new("claude-sonnet", "Claude Sonnet", "ClaudeCode"),
        new("claude-haiku", "Claude Haiku", "ClaudeCode"),
        new("ollama-chat", "Ollama Chat", "Ollama"),
        new("ollama-coder", "Ollama Coder", "Ollama"),
    ];
}
