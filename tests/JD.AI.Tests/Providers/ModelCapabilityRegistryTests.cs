using FluentAssertions;
using JD.AI.Core.Providers;

namespace JD.AI.Tests.Providers;

public sealed class ModelCapabilityRegistryTests
{
    [Fact]
    public void RegisterRange_FindModels_FiltersByRequiredCapabilities()
    {
        var registry = new ModelCapabilityRegistry();
        registry.RegisterRange(
        [
            new ProviderModelInfo(
                "chat-only",
                "Chat Only",
                "ProviderA",
                Capabilities: ModelCapabilities.Chat),
            new ProviderModelInfo(
                "tool-model",
                "Tool Model",
                "ProviderB",
                ContextWindowTokens: 128_000,
                Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling),
        ]);

        var required = ModelCapability.ChatCompletion
            | ModelCapability.ToolCalling
            | ModelCapability.JsonMode;

        var matches = registry.FindModels(required);

        matches.Should().ContainSingle();
        matches[0].ProviderName.Should().Be("ProviderB");
        matches[0].ModelId.Should().Be("tool-model");
    }

    [Fact]
    public void FindModels_WithProviderFilter_ReturnsProviderScopedMatches()
    {
        var registry = new ModelCapabilityRegistry();
        registry.RegisterRange(
        [
            new ProviderModelInfo(
                "tool-a",
                "Tool A",
                "ProviderA",
                Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling),
            new ProviderModelInfo(
                "tool-b",
                "Tool B",
                "ProviderB",
                Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling),
        ]);

        var matches = registry.FindModels(
            ModelCapability.ChatCompletion | ModelCapability.ToolCalling,
            providerName: "providera");

        matches.Should().ContainSingle();
        matches[0].ProviderName.Should().Be("ProviderA");
        matches[0].ModelId.Should().Be("tool-a");
    }

    [Fact]
    public void Register_WithZeroCost_AssignsFreeTier()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(
            new ProviderModelInfo(
                "free-model",
                "Free Model",
                "Local",
                InputCostPerToken: 0m,
                OutputCostPerToken: 0m));

        var entries = registry.GetAll();

        entries.Should().ContainSingle();
        entries[0].CostTier.Should().Be(ModelCostTier.Free);
    }
}
