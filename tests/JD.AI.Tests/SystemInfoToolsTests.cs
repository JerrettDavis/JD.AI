using JD.AI.Core.Providers;
using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class SystemInfoToolsTests
{
    [Fact]
    public void GetIdentity_ReturnsModelProviderAndAgentId()
    {
        var model = new ProviderModelInfo(
            Id: "qwen3.5:9b",
            DisplayName: "Qwen 3.5 9B",
            ProviderName: "Ollama",
            ContextWindowTokens: 256_000,
            MaxOutputTokens: 8_192,
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling | ModelCapabilities.Vision);

        var sut = new SystemInfoTools();
        sut.SetModel(model);
        sut.SetAgentId("agent-123");

        var result = sut.GetIdentity();

        Assert.Contains("Model: Qwen 3.5 9B", result, StringComparison.Ordinal);
        Assert.Contains("Model ID: qwen3.5:9b", result, StringComparison.Ordinal);
        Assert.Contains("Provider: Ollama", result, StringComparison.Ordinal);
        Assert.Contains("Context Window: 256,000 tokens", result, StringComparison.Ordinal);
        Assert.Contains("Agent ID: agent-123", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSystemStatus_ReturnsUptimeAndAgentMetadata()
    {
        var model = new ProviderModelInfo(
            Id: "qwen3.5:9b",
            DisplayName: "Qwen 3.5 9B",
            ProviderName: "Ollama");

        var sut = new SystemInfoTools();
        sut.SetModel(model);
        sut.SetAgentId("agent-xyz");
        sut.SetStartedAt(DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(5)));

        var result = sut.GetSystemStatus();

        Assert.Contains("Status: Online", result, StringComparison.Ordinal);
        Assert.Contains("Uptime:", result, StringComparison.Ordinal);
        Assert.Contains("Agent ID: agent-xyz", result, StringComparison.Ordinal);
        Assert.Contains("Model: Ollama/qwen3.5:9b", result, StringComparison.Ordinal);
    }
}
