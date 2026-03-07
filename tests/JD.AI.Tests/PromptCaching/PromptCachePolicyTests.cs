using System.Linq;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Xunit;

namespace JD.AI.Tests.PromptCaching;

public sealed class PromptCachePolicyTests
{
    [Fact]
    public void Apply_UnsupportedProvider_DoesNotSetExtensionData()
    {
        var settings = new OpenAIPromptExecutionSettings { MaxTokens = 256 };
        var model = new ProviderModelInfo("gpt-4.1", "GPT-4.1", "OpenAI");
        var history = BuildHistoryWithApproxTokens(4000);

        PromptCachePolicy.Apply(
            settings,
            model,
            history,
            enabled: true,
            ttl: PromptCacheTtl.FiveMinutes);

        Assert.Null(settings.ExtensionData);
    }

    [Fact]
    public void Apply_AnthropicPromptBelowThreshold_DoesNotSetExtensionData()
    {
        var settings = new OpenAIPromptExecutionSettings { MaxTokens = 256 };
        var model = new ProviderModelInfo("claude-sonnet-4-20250514", "Claude Sonnet 4", "Anthropic");
        var history = BuildHistoryWithApproxTokens(128);

        PromptCachePolicy.Apply(
            settings,
            model,
            history,
            enabled: true,
            ttl: PromptCacheTtl.FiveMinutes);

        Assert.Null(settings.ExtensionData);
    }

    [Fact]
    public void Apply_AnthropicPromptAboveThreshold_SetsPromptCacheKeys()
    {
        var settings = new OpenAIPromptExecutionSettings { MaxTokens = 256 };
        var model = new ProviderModelInfo("claude-sonnet-4-20250514", "Claude Sonnet 4", "Anthropic");
        var history = BuildHistoryWithApproxTokens(5000);

        PromptCachePolicy.Apply(
            settings,
            model,
            history,
            enabled: true,
            ttl: PromptCacheTtl.OneHour);

        Assert.NotNull(settings.ExtensionData);
        Assert.True(settings.ExtensionData!.ContainsKey(PromptCachePolicy.EnabledExtensionKey));
        Assert.Equal(true, settings.ExtensionData[PromptCachePolicy.EnabledExtensionKey]);
        Assert.Equal("1h", settings.ExtensionData[PromptCachePolicy.TtlExtensionKey]);
    }

    [Theory]
    [InlineData("5m", PromptCacheTtl.FiveMinutes, true)]
    [InlineData("1h", PromptCacheTtl.OneHour, true)]
    [InlineData("one_hour", PromptCacheTtl.OneHour, true)]
    [InlineData("bad", PromptCacheTtl.FiveMinutes, false)]
    public void TryParseTtl_ParsesExpectedValues(string token, PromptCacheTtl expected, bool parsed)
    {
        var ok = PromptCachePolicy.TryParseTtl(token, out var ttl);
        Assert.Equal(parsed, ok);
        Assert.Equal(expected, ttl);
    }

    private static ChatHistory BuildHistoryWithApproxTokens(int tokenCount)
    {
        var history = new ChatHistory();
        var text = string.Join(' ', Enumerable.Repeat("token", tokenCount));
        history.AddSystemMessage(text);
        history.AddUserMessage(text);
        return history;
    }
}
