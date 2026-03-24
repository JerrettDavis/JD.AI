using System.Reflection;
using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Tests;

public sealed class AgentPoolServiceIdentityPromptTests
{
    [Fact]
    public void EnrichSystemPrompt_PrependsIdentityBlock_WhenBasePromptProvided()
    {
        var model = new ProviderModelInfo(
            Id: "qwen3.5:9b",
            DisplayName: "Qwen 3.5 9B",
            ProviderName: "Ollama",
            ContextWindowTokens: 256_000,
            MaxOutputTokens: 8_192,
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var method = typeof(AgentPoolService).GetMethod(
            "EnrichSystemPrompt",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var result = (string?)method!.Invoke(null, ["You are a helpful assistant.", model]);

        result.Should().NotBeNull();
        result.Should().Contain("[System Identity]");
        result.Should().Contain("powered by Ollama/qwen3.5:9b");
        result.Should().Contain("Context window: 256,000 tokens");
        result.Should().Contain("Max output: 8,192 tokens");
        result.Should().Contain("You are a helpful assistant.");
    }

    [Fact]
    public void EnrichSystemPrompt_ReturnsIdentityOnly_WhenBasePromptEmpty()
    {
        var model = new ProviderModelInfo(
            Id: "qwen3.5:9b",
            DisplayName: "Qwen 3.5 9B",
            ProviderName: "Ollama");

        var method = typeof(AgentPoolService).GetMethod(
            "EnrichSystemPrompt",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (string?)method!.Invoke(null, [null, model]);

        result.Should().NotBeNull();
        result.Should().StartWith("[System Identity]");
        result.Should().NotContain("\n\n\n");
    }
}
