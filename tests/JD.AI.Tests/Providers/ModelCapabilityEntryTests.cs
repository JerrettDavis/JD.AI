using FluentAssertions;
using JD.AI.Core.Providers;

namespace JD.AI.Tests.Providers;

public sealed class ModelCapabilityEntryTests
{
    [Fact]
    public void Construction_AllProperties()
    {
        var entry = new ModelCapabilityEntry(
            "claude-3.5-sonnet",
            "Claude 3.5 Sonnet",
            "Anthropic",
            ModelCapability.ChatCompletion | ModelCapability.ToolCalling,
            200_000,
            ModelCostTier.Premium);

        entry.ModelId.Should().Be("claude-3.5-sonnet");
        entry.DisplayName.Should().Be("Claude 3.5 Sonnet");
        entry.ProviderName.Should().Be("Anthropic");
        entry.Capabilities.Should().HaveFlag(ModelCapability.ChatCompletion);
        entry.Capabilities.Should().HaveFlag(ModelCapability.ToolCalling);
        entry.ContextWindowTokens.Should().Be(200_000);
        entry.CostTier.Should().Be(ModelCostTier.Premium);
    }

    [Fact]
    public void RecordEquality()
    {
        var a = new ModelCapabilityEntry("m", "M", "P", ModelCapability.None, 4096, ModelCostTier.Free);
        var b = new ModelCapabilityEntry("m", "M", "P", ModelCapability.None, 4096, ModelCostTier.Free);
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality_DifferentCostTier()
    {
        var a = new ModelCapabilityEntry("m", "M", "P", ModelCapability.None, 4096, ModelCostTier.Free);
        var b = new ModelCapabilityEntry("m", "M", "P", ModelCapability.None, 4096, ModelCostTier.Premium);
        a.Should().NotBe(b);
    }

    // ── ModelCapability enum ──────────────────────────────────────────────

    [Theory]
    [InlineData(ModelCapability.None, 0)]
    [InlineData(ModelCapability.ChatCompletion, 1)]
    [InlineData(ModelCapability.Streaming, 2)]
    [InlineData(ModelCapability.ToolCalling, 4)]
    [InlineData(ModelCapability.JsonMode, 8)]
    [InlineData(ModelCapability.Vision, 16)]
    [InlineData(ModelCapability.Embeddings, 32)]
    [InlineData(ModelCapability.AudioInput, 64)]
    [InlineData(ModelCapability.AudioOutput, 128)]
    public void ModelCapability_FlagValues(ModelCapability capability, int expected) =>
        ((int)capability).Should().Be(expected);

    // ── ModelCostTier enum ────────────────────────────────────────────────

    [Theory]
    [InlineData(ModelCostTier.Unknown, 0)]
    [InlineData(ModelCostTier.Free, 1)]
    [InlineData(ModelCostTier.Budget, 2)]
    [InlineData(ModelCostTier.Standard, 3)]
    [InlineData(ModelCostTier.Premium, 4)]
    public void ModelCostTier_Values(ModelCostTier tier, int expected) =>
        ((int)tier).Should().Be(expected);
}
