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
        const int ExpectedMaxOutputTokens = 8192;
        var model = new ProviderModelInfo(
            "gpt-4.1", "GPT-4.1", "OpenAI",
            MaxOutputTokens: ExpectedMaxOutputTokens);
        var settings = Build(CreateLoop(model));

        var openAiSettings = Assert.IsType<OpenAIPromptExecutionSettings>(settings);
        Assert.Equal(ExpectedMaxOutputTokens, openAiSettings.MaxTokens);
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

    [Fact]
    public void Settings_SmallContextWithManyTools_DisablesFunctionChoiceBehavior()
    {
        // 28,672-token context with 154 tools (~30,800 estimated tokens) → tools disabled
        var model = new ProviderModelInfo(
            "qwen2.5-coder-1.5b-instruct-generic-cpu:4",
            "qwen2.5-coder-1.5b",
            "Foundry Local",
            ContextWindowTokens: 28_672,
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var registry = new ProviderRegistry([]);
        var kernel = Kernel.CreateBuilder().Build();
        // Register 154 tools to simulate the real jdai tool set
        for (var i = 0; i < 30; i++)
        {
            kernel.Plugins.AddFromFunctions($"plugin{i}", [
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_a", $"Tool {i} alpha"),
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_b", $"Tool {i} beta"),
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_c", $"Tool {i} gamma"),
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_d", $"Tool {i} delta"),
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_e", $"Tool {i} epsilon"),
            ]);
        }

        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);
        var settings = Build(loop);

        Assert.Null(settings.FunctionChoiceBehavior);
    }

    [Fact]
    public void Settings_LargeContextWithManyTools_EnablesFunctionChoiceBehavior()
    {
        // 128K context with 154 tools (~30,800 estimated tokens) → tools enabled
        var model = new ProviderModelInfo(
            "qwen2.5-coder-14b-instruct-generic-cpu:4",
            "qwen2.5-coder-14b",
            "Foundry Local",
            ContextWindowTokens: 128_000,
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var registry = new ProviderRegistry([]);
        var kernel = Kernel.CreateBuilder().Build();
        for (var i = 0; i < 30; i++)
        {
            kernel.Plugins.AddFromFunctions($"plugin{i}", [
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_a", $"Tool {i} alpha"),
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_b", $"Tool {i} beta"),
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_c", $"Tool {i} gamma"),
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_d", $"Tool {i} delta"),
                KernelFunctionFactory.CreateFromMethod((string x) => x, $"t{i}_e", $"Tool {i} epsilon"),
            ]);
        }

        var session = new AgentSession(registry, kernel, model);
        var loop = new AgentLoop(session);
        var settings = Build(loop);

        Assert.IsType<AutoFunctionChoiceBehavior>(settings.FunctionChoiceBehavior);
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

    [Fact]
    public void Settings_WithReasoningEffortOverride_OpenAISetsReasoningEffort()
    {
        var model = new ProviderModelInfo(
            "o3-mini",
            "o3-mini",
            "OpenAI",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var registry = new ProviderRegistry([]);
        var kernel = Kernel.CreateBuilder().Build();
        var session = new AgentSession(registry, kernel, model)
        {
            ReasoningEffortOverride = ReasoningEffort.High,
        };
        var loop = new AgentLoop(session);

        var settings = Build(loop);
        var openAiSettings = Assert.IsType<OpenAIPromptExecutionSettings>(settings);

        Assert.NotNull(openAiSettings.ExtensionData);
        Assert.True(openAiSettings.ExtensionData!.ContainsKey("reasoning_effort"));
        Assert.Equal("high", openAiSettings.ExtensionData["reasoning_effort"]);
    }

    [Fact]
    public void Settings_WithReasoningEffortOverride_AnthropicSetsOutputConfig()
    {
        var model = new ProviderModelInfo(
            "claude-sonnet-4-6",
            "Claude Sonnet 4.6",
            "Anthropic",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var registry = new ProviderRegistry([]);
        var kernel = Kernel.CreateBuilder().Build();
        var session = new AgentSession(registry, kernel, model)
        {
            ReasoningEffortOverride = ReasoningEffort.Max,
        };
        var loop = new AgentLoop(session);

        var settings = Build(loop);
        var openAiSettings = Assert.IsType<OpenAIPromptExecutionSettings>(settings);

        Assert.NotNull(openAiSettings.ExtensionData);
        Assert.True(openAiSettings.ExtensionData!.ContainsKey("output_config"));
        var outputConfig = Assert.IsType<Dictionary<string, object?>>(openAiSettings.ExtensionData["output_config"]);
        Assert.Equal("max", outputConfig["effort"]);
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
