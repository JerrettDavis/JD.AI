using FluentAssertions;
using JD.AI.Core.Providers.Metadata;

namespace JD.AI.Tests.Providers.Metadata;

public sealed class ModelMetadataEntryTests
{
    [Fact]
    public void Construction_RequiredKey()
    {
        var entry = new ModelMetadataEntry { Key = "anthropic/claude-3-opus" };
        entry.Key.Should().Be("anthropic/claude-3-opus");
    }

    [Fact]
    public void OptionalProperties_DefaultNull()
    {
        var entry = new ModelMetadataEntry { Key = "test" };
        entry.LitellmProvider.Should().BeNull();
        entry.Mode.Should().BeNull();
        entry.MaxInputTokens.Should().BeNull();
        entry.MaxOutputTokens.Should().BeNull();
        entry.InputCostPerToken.Should().BeNull();
        entry.OutputCostPerToken.Should().BeNull();
        entry.SupportsVision.Should().BeNull();
        entry.SupportsFunctionCalling.Should().BeNull();
        entry.SupportsReasoning.Should().BeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var entry = new ModelMetadataEntry
        {
            Key = "openai/gpt-4o",
            LitellmProvider = "openai",
            Mode = "chat",
            MaxInputTokens = 128_000,
            MaxOutputTokens = 16_384,
            InputCostPerToken = 0.0000025m,
            OutputCostPerToken = 0.00001m,
            SupportsVision = true,
            SupportsFunctionCalling = true,
            SupportsReasoning = false,
        };

        entry.Key.Should().Be("openai/gpt-4o");
        entry.LitellmProvider.Should().Be("openai");
        entry.Mode.Should().Be("chat");
        entry.MaxInputTokens.Should().Be(128_000);
        entry.MaxOutputTokens.Should().Be(16_384);
        entry.InputCostPerToken.Should().Be(0.0000025m);
        entry.OutputCostPerToken.Should().Be(0.00001m);
        entry.SupportsVision.Should().BeTrue();
        entry.SupportsFunctionCalling.Should().BeTrue();
        entry.SupportsReasoning.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality()
    {
        var a = new ModelMetadataEntry { Key = "x", Mode = "chat" };
        var b = new ModelMetadataEntry { Key = "x", Mode = "chat" };
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality()
    {
        var a = new ModelMetadataEntry { Key = "x" };
        var b = new ModelMetadataEntry { Key = "y" };
        a.Should().NotBe(b);
    }
}
