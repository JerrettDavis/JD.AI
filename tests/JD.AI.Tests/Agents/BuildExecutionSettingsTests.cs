using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Tests.Agents;

/// <summary>
/// Guards the execution settings contract so that regressions like
/// missing ModelId or MaxTokens can never ship again.
/// </summary>
public sealed class BuildExecutionSettingsTests
{
    private static readonly System.Reflection.MethodInfo BuildMethod =
        typeof(AgentLoop).GetMethod(
            "BuildExecutionSettings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        ?? throw new InvalidOperationException("BuildExecutionSettings method not found");

    /// <summary>
    /// Invoke the private BuildExecutionSettings via reflection.
    /// </summary>
    private static PromptExecutionSettings Build(AgentLoop loop) =>
        (PromptExecutionSettings)BuildMethod.Invoke(loop, null)!;

    private static AgentLoop CreateLoop(ProviderModelInfo model)
    {
        var registry = new ProviderRegistry([]);
        var kernel = Kernel.CreateBuilder().Build();
        var session = new AgentSession(registry, kernel, model);
        return new AgentLoop(session);
    }

    // ── ModelId ────────────────────────────────────────────

    [Fact]
    public void Settings_Always_IncludeModelId()
    {
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6", "Claude Sonnet 4.6", "Claude Code");
        var settings = Build(CreateLoop(model));

        Assert.Equal("claude-sonnet-4-6", settings.ModelId);
    }

    [Fact]
    public void Settings_FoundryLocal_IncludeModelId()
    {
        var model = new ProviderModelInfo(
            "qwen2.5-coder-14b-instruct-generic-cpu:4",
            "qwen2.5-coder-14b",
            "Foundry Local");
        var settings = Build(CreateLoop(model));

        Assert.Equal("qwen2.5-coder-14b-instruct-generic-cpu:4", settings.ModelId);
    }

    // ── MaxTokens ──────────────────────────────────────────

    [Fact]
    public void Settings_Always_SetMaxTokens()
    {
        var model = new ProviderModelInfo(
            "gpt-4.1", "GPT-4.1", "OpenAI");
        var settings = Build(CreateLoop(model));

        var openAiSettings = Assert.IsType<OpenAIPromptExecutionSettings>(settings);
        Assert.NotNull(openAiSettings.MaxTokens);
        Assert.True(openAiSettings.MaxTokens > 0,
            "MaxTokens must be set to a positive value; some endpoints return 500 without it.");
    }

    [Fact]
    public void Settings_MaxTokens_ReflectsModelMaxOutputTokens()
    {
        const int expectedMaxOutputTokens = 8192;
        var model = new ProviderModelInfo(
            "gpt-4.1", "GPT-4.1", "OpenAI",
            MaxOutputTokens: expectedMaxOutputTokens);
        var settings = Build(CreateLoop(model));

        var openAiSettings = Assert.IsType<OpenAIPromptExecutionSettings>(settings);
        Assert.Equal(expectedMaxOutputTokens, openAiSettings.MaxTokens);
    }

    // ── FunctionChoiceBehavior ──────────────────────────────

    [Fact]
    public void Settings_ToolCapableModel_EnablesFunctionChoiceBehavior()
    {
        var model = new ProviderModelInfo(
            "qwen2.5-coder-14b-instruct-generic-cpu:4",
            "qwen2.5-coder-14b",
            "Foundry Local",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);
        var settings = Build(CreateLoop(model));

        Assert.IsType<AutoFunctionChoiceBehavior>(settings.FunctionChoiceBehavior);
    }

    [Fact]
    public void Settings_ChatOnlyModel_DisablesFunctionChoiceBehavior()
    {
        var model = new ProviderModelInfo(
            "phi-4-mini-reasoning-generic-cpu:3",
            "Phi-4 Mini Reasoning",
            "Foundry Local",
            Capabilities: ModelCapabilities.Chat);
        var settings = Build(CreateLoop(model));

        Assert.Null(settings.FunctionChoiceBehavior);
    }

    // ── Never uses deprecated ToolCallBehavior ─────────────

    [Fact]
    public void Settings_NeverUsesDeprecatedToolCallBehavior()
    {
        var model = new ProviderModelInfo(
            "qwen2.5-coder-14b-instruct-generic-cpu:4",
            "qwen2.5-coder-14b",
            "Foundry Local",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);
        var settings = Build(CreateLoop(model));

        var openAiSettings = Assert.IsType<OpenAIPromptExecutionSettings>(settings);
        Assert.Null(openAiSettings.ToolCallBehavior);
    }

    // ── Settings type ──────────────────────────────────────

    [Fact]
    public void Settings_IsOpenAIPromptExecutionSettings()
    {
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6", "Claude Sonnet 4.6", "Claude Code");
        var settings = Build(CreateLoop(model));

        Assert.IsType<OpenAIPromptExecutionSettings>(settings);
    }
}
