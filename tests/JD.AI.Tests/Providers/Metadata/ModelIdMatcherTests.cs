using FluentAssertions;
using JD.AI.Core.Providers.Metadata;

namespace JD.AI.Tests.Providers.Metadata;

public sealed class ModelIdMatcherTests
{
    private static readonly Dictionary<string, ModelMetadataEntry> Entries = new(StringComparer.Ordinal)
    {
        ["gpt-4o"] = MakeEntry("gpt-4o"),
        ["anthropic/claude-opus-4-20250514"] = MakeEntry("anthropic/claude-opus-4-20250514"),
        ["bedrock/anthropic.claude-sonnet-4-20250514-v1:0"] = MakeEntry("bedrock/anthropic.claude-sonnet-4-20250514-v1:0"),
        ["openai/gpt-4.1"] = MakeEntry("openai/gpt-4.1"),
        ["gemini/gemini-2.5-pro"] = MakeEntry("gemini/gemini-2.5-pro"),
        ["mistral/mistral-large-latest"] = MakeEntry("mistral/mistral-large-latest"),
    };

    [Fact]
    public void ExactMatch_ReturnsEntry()
    {
        var result = ModelIdMatcher.FindBestMatch("gpt-4o", "OpenAI", Entries);
        result.Should().NotBeNull();
        result!.Key.Should().Be("gpt-4o");
    }

    [Fact]
    public void ProviderPrefixedMatch_ReturnsEntry()
    {
        var result = ModelIdMatcher.FindBestMatch("claude-opus-4-20250514", "Anthropic", Entries);
        result.Should().NotBeNull();
        result!.Key.Should().Be("anthropic/claude-opus-4-20250514");
    }

    [Fact]
    public void BareSuffixMatch_ReturnsEntry()
    {
        var result = ModelIdMatcher.FindBestMatch(
            "anthropic.claude-sonnet-4-20250514-v1:0", "AWS Bedrock", Entries);
        result.Should().NotBeNull();
        result!.Key.Should().Be("bedrock/anthropic.claude-sonnet-4-20250514-v1:0");
    }

    [Fact]
    public void NormalizedStemMatch_HandlesDateSuffixes()
    {
        // "gpt-4.1" should match "openai/gpt-4.1" via bare suffix
        var result = ModelIdMatcher.FindBestMatch("gpt-4.1", "OpenAI", Entries);
        result.Should().NotBeNull();
        result!.Key.Should().Be("openai/gpt-4.1");
    }

    [Fact]
    public void UnknownModel_ReturnsNull()
    {
        var result = ModelIdMatcher.FindBestMatch("nonexistent-model-xyz", "Unknown", Entries);
        result.Should().BeNull();
    }

    [Fact]
    public void CaseInsensitive_MatchesCorrectly()
    {
        var result = ModelIdMatcher.FindBestMatch("GPT-4O", "OpenAI", Entries);
        // Exact match is case-sensitive in dictionary, so it should fall through to suffix/stem match
        // The bare suffix check is case-insensitive so it should still find it
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("claude-opus-4-20250514", "claude-opus-4")]
    [InlineData("gpt-4.1", "gpt-4.1")]
    [InlineData("model-v1:0", "model")]
    [InlineData("model-v2", "model")]
    public void NormalizeStem_StripsExpectedSuffixes(string input, string expected)
    {
        ModelIdMatcher.NormalizeStem(input).Should().Be(expected);
    }

    private static ModelMetadataEntry MakeEntry(string key) => new()
    {
        Key = key,
        Mode = "chat",
        MaxInputTokens = 128_000,
        MaxOutputTokens = 4_096,
        InputCostPerToken = 0.00001m,
        OutputCostPerToken = 0.00003m,
    };
}
