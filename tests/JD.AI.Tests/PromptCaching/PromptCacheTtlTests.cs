using FluentAssertions;
using JD.AI.Core.PromptCaching;

namespace JD.AI.Tests.PromptCaching;

public sealed class PromptCacheTtlTests
{
    [Theory]
    [InlineData(PromptCacheTtl.FiveMinutes, 0)]
    [InlineData(PromptCacheTtl.OneHour, 1)]
    public void PromptCacheTtl_Values(PromptCacheTtl ttl, int expected) =>
        ((int)ttl).Should().Be(expected);

    // ── PromptCachePolicy internal helpers ────────────────────────────────

    [Theory]
    [InlineData("Anthropic", null, true)]
    [InlineData("Claude Code", null, true)]
    [InlineData("anthropic", null, true)]
    [InlineData("OpenAI", null, false)]
    [InlineData(null, "claude-3-5-sonnet", true)]
    [InlineData(null, "gpt-4o", false)]
    [InlineData(null, null, false)]
    [InlineData("", "", false)]
    public void IsSupportedProvider(string? provider, string? modelId, bool expected) =>
        PromptCachePolicy.IsSupportedProvider(provider, modelId).Should().Be(expected);

    [Theory]
    [InlineData("claude-haiku-3-5", 2048)]
    [InlineData("claude-sonnet-3-5", 1024)]
    [InlineData("claude-opus-4", 1024)]
    [InlineData(null, 1024)]
    [InlineData("gpt-4o", 1024)]
    public void GetMinimumPromptTokens(string? modelId, int expected) =>
        PromptCachePolicy.GetMinimumPromptTokens(modelId).Should().Be(expected);

    [Theory]
    [InlineData(PromptCacheTtl.OneHour, "1h")]
    [InlineData(PromptCacheTtl.FiveMinutes, "5m")]
    public void ToToken(PromptCacheTtl ttl, string expected) =>
        PromptCachePolicy.ToToken(ttl).Should().Be(expected);

    [Theory]
    [InlineData("1h", true, PromptCacheTtl.OneHour)]
    [InlineData("one_hour", true, PromptCacheTtl.OneHour)]
    [InlineData("onehour", true, PromptCacheTtl.OneHour)]
    [InlineData("hour", true, PromptCacheTtl.OneHour)]
    [InlineData("5m", true, PromptCacheTtl.FiveMinutes)]
    [InlineData("five_minutes", true, PromptCacheTtl.FiveMinutes)]
    [InlineData("fiveminutes", true, PromptCacheTtl.FiveMinutes)]
    [InlineData("default", true, PromptCacheTtl.FiveMinutes)]
    [InlineData("garbage", false, PromptCacheTtl.FiveMinutes)]
    [InlineData(null, false, PromptCacheTtl.FiveMinutes)]
    [InlineData("", false, PromptCacheTtl.FiveMinutes)]
    public void TryParseTtl(string? input, bool expectedResult, PromptCacheTtl expectedTtl)
    {
        var result = PromptCachePolicy.TryParseTtl(input, out var ttl);
        result.Should().Be(expectedResult);
        ttl.Should().Be(expectedTtl);
    }
}
