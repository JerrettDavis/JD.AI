using FluentAssertions;
using JD.AI.Core.Providers.Metadata;

namespace JD.AI.Tests.Providers.Metadata;

/// <summary>
/// Extended edge-case tests for <see cref="ModelIdMatcher.NormalizeStem"/>.
/// Core matching strategies are covered in <see cref="ModelIdMatcherTests"/>.
/// </summary>
public sealed class ModelIdMatcherExtendedTests
{
    // ── NormalizeStem edge cases ──────────────────────────────────────────

    [Theory]
    [InlineData("  model  ", "model")]                        // whitespace trimming
    [InlineData("model:10", "model")]                         // multi-digit version colon
    [InlineData("model:abc", "model:abc")]                    // non-digit after colon — NOT stripped
    [InlineData("model-20241231", "model")]                   // different valid date suffix
    [InlineData("model-20250514extra", "model-20250514extra")]// date not at end — NOT stripped
    [InlineData("model-v10", "model-v10")]                    // multi-digit after v — NOT stripped
    [InlineData("ab", "ab")]                                  // too short for any suffix rule
    [InlineData("", "")]                                      // empty input
    [InlineData("x:5", "x")]                                  // single-char id with version colon
    [InlineData("model:", "model:")]                          // colon at end, nothing after — NOT stripped
    public void NormalizeStem_EdgeCases(string input, string expected)
    {
        ModelIdMatcher.NormalizeStem(input).Should().Be(expected);
    }

    // ── FindBestMatch additional strategies ───────────────────────────────

    [Fact]
    public void FindBestMatch_EmptyEntries_ReturnsNull()
    {
        var empty = new Dictionary<string, ModelMetadataEntry>(StringComparer.Ordinal);
        ModelIdMatcher.FindBestMatch("gpt-4o", "OpenAI", empty).Should().BeNull();
    }

    [Fact]
    public void FindBestMatch_NormalizedStemFallback_MatchesDespiteDateDifference()
    {
        var entries = new Dictionary<string, ModelMetadataEntry>(StringComparer.Ordinal)
        {
            ["anthropic/claude-opus-4-20250514"] = MakeEntry("anthropic/claude-opus-4-20250514"),
        };

        // "claude-opus-4-20250601" should normalize to "claude-opus-4" which matches
        // "anthropic/claude-opus-4-20250514" → "claude-opus-4"
        var result = ModelIdMatcher.FindBestMatch("claude-opus-4-20250601", "Unknown", entries);
        result.Should().NotBeNull();
    }

    [Fact]
    public void FindBestMatch_UnknownProvider_SkipsPrefixStrategy()
    {
        var entries = new Dictionary<string, ModelMetadataEntry>(StringComparer.Ordinal)
        {
            ["openai/gpt-4o"] = MakeEntry("openai/gpt-4o"),
        };

        // Unknown provider → no prefixes → falls through to bare suffix
        var result = ModelIdMatcher.FindBestMatch("gpt-4o", "SomeUnknownProvider", entries);
        result.Should().NotBeNull();
        result!.Key.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public void FindBestMatch_MultipleProviderPrefixes_TriesBoth()
    {
        var entries = new Dictionary<string, ModelMetadataEntry>(StringComparer.Ordinal)
        {
            ["bedrock_converse/model-x"] = MakeEntry("bedrock_converse/model-x"),
        };

        // "AWS Bedrock" has prefixes ["bedrock/", "bedrock_converse/"]
        var result = ModelIdMatcher.FindBestMatch("model-x", "AWS Bedrock", entries);
        result.Should().NotBeNull();
        result!.Key.Should().Be("bedrock_converse/model-x");
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
