using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Metadata;

namespace JD.AI.Tests.Providers.Metadata;

public sealed class ModelMetadataProviderTests
{
    private const string SampleJson = """
        {
            "sample_spec": { "max_tokens": 100 },
            "gpt-4o": {
                "max_tokens": 16384,
                "max_input_tokens": 128000,
                "max_output_tokens": 16384,
                "input_cost_per_token": 0.0000025,
                "output_cost_per_token": 0.00001,
                "litellm_provider": "openai",
                "mode": "chat",
                "supports_function_calling": true,
                "supports_vision": true,
                "supports_reasoning": false
            },
            "dall-e-3": {
                "mode": "image_generation",
                "litellm_provider": "openai"
            },
            "text-embedding-3-large": {
                "mode": "embedding",
                "litellm_provider": "openai"
            },
            "anthropic/claude-sonnet-4-20250514": {
                "max_tokens": 16384,
                "max_input_tokens": 200000,
                "max_output_tokens": 16384,
                "input_cost_per_token": 0.000003,
                "output_cost_per_token": 0.000015,
                "litellm_provider": "anthropic",
                "mode": "chat",
                "supports_function_calling": true,
                "supports_vision": true,
                "supports_reasoning": false
            }
        }
        """;

    [Fact]
    public void ParseLiteLlmJson_ExtractsChatModels()
    {
        var entries = ModelMetadataProvider.ParseLiteLlmJson(SampleJson);

        entries.Should().ContainKey("gpt-4o");
        entries.Should().ContainKey("anthropic/claude-sonnet-4-20250514");
        entries.Should().NotContainKey("dall-e-3");
        entries.Should().NotContainKey("text-embedding-3-large");
    }

    [Fact]
    public void ParseLiteLlmJson_SkipsSampleSpec()
    {
        var entries = ModelMetadataProvider.ParseLiteLlmJson(SampleJson);
        entries.Should().NotContainKey("sample_spec");
    }

    [Fact]
    public void ParseLiteLlmJson_ParsesAllFields()
    {
        var entries = ModelMetadataProvider.ParseLiteLlmJson(SampleJson);
        var gpt4o = entries["gpt-4o"];

        gpt4o.MaxInputTokens.Should().Be(128_000);
        gpt4o.MaxOutputTokens.Should().Be(16_384);
        gpt4o.InputCostPerToken.Should().Be(0.0000025m);
        gpt4o.OutputCostPerToken.Should().Be(0.00001m);
        gpt4o.LitellmProvider.Should().Be("openai");
        gpt4o.Mode.Should().Be("chat");
        gpt4o.SupportsVision.Should().BeTrue();
        gpt4o.SupportsFunctionCalling.Should().BeTrue();
        gpt4o.SupportsReasoning.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WithFakeSource_PopulatesEntries()
    {
        var source = new FakeMetadataSource(SampleJson);
        var provider = new ModelMetadataProvider(source);

        await provider.LoadAsync();

        provider.IsLoaded.Should().BeTrue();
        provider.EntryCount.Should().Be(2);
    }

    [Fact]
    public async Task LoadAsync_SourceReturnsNull_DoesNotThrow()
    {
        var source = new FakeMetadataSource(null);
        var provider = new ModelMetadataProvider(source);

        // Should not throw even when source returns null
        var act = () => provider.LoadAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Enrich_MatchedModel_SetsHasMetadataTrue()
    {
        var source = new FakeMetadataSource(SampleJson);
        var provider = new ModelMetadataProvider(source);
        await provider.LoadAsync();

        var models = new List<ProviderModelInfo>
        {
            new("gpt-4o", "GPT-4o", "OpenAI"),
        };

        var enriched = provider.Enrich(models);

        enriched.Should().HaveCount(1);
        enriched[0].HasMetadata.Should().BeTrue();
        enriched[0].ContextWindowTokens.Should().Be(128_000);
        enriched[0].MaxOutputTokens.Should().Be(16_384);
        enriched[0].InputCostPerToken.Should().Be(0.0000025m);
        enriched[0].OutputCostPerToken.Should().Be(0.00001m);
        enriched[0].Capabilities.Should().HaveFlag(ModelCapabilities.ToolCalling);
        enriched[0].Capabilities.Should().HaveFlag(ModelCapabilities.Vision);
    }

    [Fact]
    public async Task Enrich_UnmatchedModel_PreservesDefaults()
    {
        var source = new FakeMetadataSource(SampleJson);
        var provider = new ModelMetadataProvider(source);
        await provider.LoadAsync();

        var models = new List<ProviderModelInfo>
        {
            new("nonexistent-model", "Unknown Model", "Unknown"),
        };

        var enriched = provider.Enrich(models);

        enriched.Should().HaveCount(1);
        enriched[0].HasMetadata.Should().BeFalse();
        enriched[0].ContextWindowTokens.Should().Be(128_000); // default
        enriched[0].MaxOutputTokens.Should().Be(16_384); // default
    }

    [Fact]
    public async Task Enrich_MatchedModel_UsesMetadataToDisableUnsupportedCapabilities()
    {
        const string noToolsJson = """
            {
                "chat-basic": {
                    "mode": "chat",
                    "litellm_provider": "openai",
                    "supports_function_calling": false,
                    "supports_vision": false
                }
            }
            """;

        var source = new FakeMetadataSource(noToolsJson);
        var provider = new ModelMetadataProvider(source);
        await provider.LoadAsync();

        var models = new List<ProviderModelInfo>
        {
            new(
                "chat-basic",
                "Chat Basic",
                "OpenAI",
                Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling | ModelCapabilities.Vision),
        };

        var enriched = provider.Enrich(models);

        enriched.Should().HaveCount(1);
        enriched[0].Capabilities.Should().Be(ModelCapabilities.Chat);
    }

    private sealed class FakeMetadataSource(string? json) : IModelMetadataSource
    {
        public Task<string?> FetchAsync(CancellationToken ct = default) =>
            Task.FromResult(json);
    }
}
