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

    // ── Extended coverage ─────────────────────────────────────────────

    [Fact]
    public void GetAll_InitiallyEmpty()
    {
        new ModelCapabilityRegistry().GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Register_SameModel_OverwritesExisting()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("m1", "Old", "p"));
        registry.Register(new ProviderModelInfo("m1", "New", "p"));
        registry.GetAll().Should().HaveCount(1);
        registry.GetAll()[0].DisplayName.Should().Be("New");
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var registry = new ModelCapabilityRegistry();
        registry.RegisterRange([
            new ProviderModelInfo("a", "A", "p"),
            new ProviderModelInfo("b", "B", "p"),
        ]);
        registry.Clear();
        registry.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Register_ChatOnly_HasChatAndStreamingButNotToolCalling()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("m1", "M1", "p", Capabilities: ModelCapabilities.Chat));
        var entry = registry.GetAll()[0];
        entry.Capabilities.Should().HaveFlag(ModelCapability.ChatCompletion);
        entry.Capabilities.Should().HaveFlag(ModelCapability.Streaming);
        entry.Capabilities.Should().NotHaveFlag(ModelCapability.ToolCalling);
    }

    [Fact]
    public void Register_WithVision_HasVisionCapability()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("v1", "V1", "p",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.Vision));
        registry.GetAll()[0].Capabilities.Should().HaveFlag(ModelCapability.Vision);
    }

    [Fact]
    public void Register_WithEmbeddings_HasEmbeddingsCapability()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("e1", "E1", "p",
            Capabilities: ModelCapabilities.Embeddings));
        registry.GetAll()[0].Capabilities.Should().HaveFlag(ModelCapability.Embeddings);
    }

    [Fact]
    public void Register_BudgetCost_AssignsBudgetTier()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("m1", "M1", "p",
            InputCostPerToken: 0.0000005m, OutputCostPerToken: 0.0000005m)); // $0.50/M avg
        registry.GetAll()[0].CostTier.Should().Be(ModelCostTier.Budget);
    }

    [Fact]
    public void Register_StandardCost_AssignsStandardTier()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("m1", "M1", "p",
            InputCostPerToken: 0.000003m, OutputCostPerToken: 0.000003m)); // $3/M avg
        registry.GetAll()[0].CostTier.Should().Be(ModelCostTier.Standard);
    }

    [Fact]
    public void Register_PremiumCost_AssignsPremiumTier()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("m1", "M1", "p",
            InputCostPerToken: 0.00003m, OutputCostPerToken: 0.00006m)); // $45/M avg
        registry.GetAll()[0].CostTier.Should().Be(ModelCostTier.Premium);
    }

    [Fact]
    public void FindModels_SortsByContextWindowDescending()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("small", "Small", "p", ContextWindowTokens: 4_096));
        registry.Register(new ProviderModelInfo("large", "Large", "p", ContextWindowTokens: 200_000));
        var results = registry.FindModels(ModelCapability.ChatCompletion);
        results[0].ModelId.Should().Be("large");
        results[1].ModelId.Should().Be("small");
    }

    [Fact]
    public void FindModels_NoMatch_ReturnsEmpty()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("m1", "M1", "p", Capabilities: ModelCapabilities.Chat));
        registry.FindModels(ModelCapability.Embeddings).Should().BeEmpty();
    }

    [Fact]
    public void FindModels_NullProvider_ReturnsAll()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("a", "A", "p1"));
        registry.Register(new ProviderModelInfo("b", "B", "p2"));
        registry.FindModels(ModelCapability.ChatCompletion, providerName: null)
            .Should().HaveCount(2);
    }

    [Fact]
    public void FindModels_EmptyProvider_ReturnsAll()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("a", "A", "p1"));
        registry.FindModels(ModelCapability.ChatCompletion, providerName: "")
            .Should().HaveCount(1);
    }

    [Fact]
    public void Register_PreservesContextWindow()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("m1", "M1", "p", ContextWindowTokens: 256_000));
        registry.GetAll()[0].ContextWindowTokens.Should().Be(256_000);
    }

    [Fact]
    public void Register_PreservesProviderName()
    {
        var registry = new ModelCapabilityRegistry();
        registry.Register(new ProviderModelInfo("m1", "M1", "anthropic"));
        registry.GetAll()[0].ProviderName.Should().Be("anthropic");
    }
}
