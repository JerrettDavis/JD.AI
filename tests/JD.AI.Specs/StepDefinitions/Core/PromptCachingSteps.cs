using FluentAssertions;
using JD.AI.Core.PromptCaching;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class PromptCachingSteps
{
    private readonly ScenarioContext _context;

    public PromptCachingSteps(ScenarioContext context) => _context = context;

    [Given(@"execution settings for model ""(.*)"" on provider ""(.*)""")]
    public void GivenExecutionSettingsForModelOnProvider(string modelId, string providerName)
    {
        var settings = new OpenAIPromptExecutionSettings();
        _context.Set(settings, "executionSettings");
        _context.Set(modelId, "cacheModelId");
        _context.Set(providerName, "cacheProviderName");
    }

    [Given(@"the chat history has sufficient tokens for caching")]
    public void GivenTheChatHistoryHasSufficientTokensForCaching()
    {
        var history = new ChatHistory();
        // Add enough content to exceed the minimum token threshold (1024 for most models)
        var longText = new string('x', 5000);
        history.AddSystemMessage(longText);
        history.AddUserMessage(longText);
        _context.Set(history, "cacheHistory");
    }

    [Given(@"the chat history has insufficient tokens for caching")]
    public void GivenTheChatHistoryHasInsufficientTokensForCaching()
    {
        var history = new ChatHistory();
        history.AddUserMessage("Hi"); // Very short - under minimum threshold
        _context.Set(history, "cacheHistory");
    }

    [When(@"the prompt cache policy is applied with caching enabled")]
    public void WhenThePromptCachePolicyIsAppliedWithCachingEnabled()
    {
        var settings = _context.Get<OpenAIPromptExecutionSettings>("executionSettings");
        var modelId = _context.Get<string>("cacheModelId");
        var providerName = _context.Get<string>("cacheProviderName");
        var history = _context.Get<ChatHistory>("cacheHistory");

        PromptCachePolicy.Apply(settings, providerName, modelId, history,
            enabled: true, PromptCacheTtl.FiveMinutes);
    }

    [When(@"the prompt cache policy is applied with caching disabled")]
    public void WhenThePromptCachePolicyIsAppliedWithCachingDisabled()
    {
        var settings = _context.Get<OpenAIPromptExecutionSettings>("executionSettings");
        var modelId = _context.Get<string>("cacheModelId");
        var providerName = _context.Get<string>("cacheProviderName");
        var history = _context.Get<ChatHistory>("cacheHistory");

        PromptCachePolicy.Apply(settings, providerName, modelId, history,
            enabled: false, PromptCacheTtl.FiveMinutes);
    }

    [When(@"the prompt cache policy is applied with caching enabled and TTL ""(.*)""")]
    public void WhenThePromptCachePolicyIsAppliedWithCachingEnabledAndTtl(string ttlStr)
    {
        var settings = _context.Get<OpenAIPromptExecutionSettings>("executionSettings");
        var modelId = _context.Get<string>("cacheModelId");
        var providerName = _context.Get<string>("cacheProviderName");
        var history = _context.Get<ChatHistory>("cacheHistory");
        var ttl = Enum.Parse<PromptCacheTtl>(ttlStr);

        PromptCachePolicy.Apply(settings, providerName, modelId, history,
            enabled: true, ttl);
    }

    [When(@"checking if provider ""(.*)"" with model ""(.*)"" supports caching")]
    public void WhenCheckingIfProviderWithModelSupportsCaching(string provider, string model)
    {
        var result = PromptCachePolicy.IsSupportedProvider(provider, model);
        _context.Set(result, "cacheSupportResult");
    }

    [When(@"checking the minimum tokens for model ""(.*)""")]
    public void WhenCheckingTheMinimumTokensForModel(string model)
    {
        var minTokens = PromptCachePolicy.GetMinimumPromptTokens(model);
        _context.Set(minTokens, "minTokens");
    }

    [Then(@"the execution settings should contain the cache enabled extension key")]
    public void ThenTheExecutionSettingsShouldContainTheCacheEnabledExtensionKey()
    {
        var settings = _context.Get<OpenAIPromptExecutionSettings>("executionSettings");
        settings.ExtensionData.Should().NotBeNull();
        settings.ExtensionData.Should().ContainKey(PromptCachePolicy.EnabledExtensionKey);
    }

    [Then(@"the execution settings should not contain the cache enabled extension key")]
    public void ThenTheExecutionSettingsShouldNotContainTheCacheEnabledExtensionKey()
    {
        var settings = _context.Get<OpenAIPromptExecutionSettings>("executionSettings");
        if (settings.ExtensionData != null)
        {
            settings.ExtensionData.Should().NotContainKey(PromptCachePolicy.EnabledExtensionKey);
        }
    }

    [Then(@"the execution settings should contain the TTL extension key with value ""(.*)""")]
    public void ThenTheExecutionSettingsShouldContainTheTtlExtensionKeyWithValue(string expected)
    {
        var settings = _context.Get<OpenAIPromptExecutionSettings>("executionSettings");
        settings.ExtensionData.Should().NotBeNull();
        settings.ExtensionData.Should().ContainKey(PromptCachePolicy.TtlExtensionKey);
        settings.ExtensionData![PromptCachePolicy.TtlExtensionKey].Should().Be(expected);
    }

    [Then(@"the result should be true")]
    public void ThenTheResultShouldBeTrue()
    {
        var result = _context.Get<bool>("cacheSupportResult");
        result.Should().BeTrue();
    }

    [Then(@"the result should be false")]
    public void ThenTheResultShouldBeFalse()
    {
        var result = _context.Get<bool>("cacheSupportResult");
        result.Should().BeFalse();
    }

    [Then(@"the minimum should be (\d+)")]
    public void ThenTheMinimumShouldBe(int expected)
    {
        var minTokens = _context.Get<int>("minTokens");
        minTokens.Should().Be(expected);
    }
}
