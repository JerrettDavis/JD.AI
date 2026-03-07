using System.Linq;
using Anthropic.SDK.Messaging;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using Microsoft.Extensions.AI;
using Xunit;

namespace JD.AI.Tests.Providers;

public sealed class AnthropicPromptCachingChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_WithPromptCacheDirective_InjectsRawMessageParameters()
    {
        var inner = new RecordingChatClient();
        var client = new AnthropicPromptCachingChatClient(inner);

        var options = new ChatOptions
        {
            ModelId = "claude-sonnet-4-20250514",
            Temperature = 0.2f,
            MaxOutputTokens = 1024,
        };
        options.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [PromptCachePolicy.EnabledExtensionKey] = true,
            [PromptCachePolicy.TtlExtensionKey] = "1h",
        };

        var input = new[]
        {
            new ChatMessage(ChatRole.System, [new Microsoft.Extensions.AI.TextContent("system prompt")]),
            new ChatMessage(ChatRole.User, [new Microsoft.Extensions.AI.TextContent("hello")]),
        };

        _ = await client.GetResponseAsync(input, options);

        Assert.NotNull(inner.LastMessages);
        Assert.Empty(inner.LastMessages!);
        Assert.NotNull(inner.LastOptions);
        Assert.NotNull(inner.LastOptions!.RawRepresentationFactory);
        Assert.Equal(options.Temperature, inner.LastOptions.Temperature);
        Assert.Equal(options.MaxOutputTokens, inner.LastOptions.MaxOutputTokens);
        Assert.NotSame(options, inner.LastOptions);

        var raw = inner.LastOptions.RawRepresentationFactory(inner);
        var parameters = Assert.IsType<MessageParameters>(raw);
        Assert.Equal(PromptCacheType.AutomaticToolsAndSystem, parameters.PromptCaching);
        Assert.NotNull(parameters.System);
        Assert.NotEmpty(parameters.System!);
        Assert.Equal(CacheDuration.OneHour, parameters.System![^1].CacheControl?.TTL);
    }

    [Fact]
    public async Task GetResponseAsync_WithoutDirective_PassesThrough()
    {
        var inner = new RecordingChatClient();
        var client = new AnthropicPromptCachingChatClient(inner);
        var input = new[]
        {
            new ChatMessage(ChatRole.User, [new Microsoft.Extensions.AI.TextContent("hello")]),
        };

        _ = await client.GetResponseAsync(input, new ChatOptions());

        Assert.NotNull(inner.LastMessages);
        Assert.Single(inner.LastMessages!);
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<ChatMessage>? LastMessages { get; private set; }
        public ChatOptions? LastOptions { get; private set; }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            LastOptions = options;

            var response = new ChatResponse(
            [
                new ChatMessage(
                    ChatRole.Assistant,
                    [new Microsoft.Extensions.AI.TextContent("ok")]),
            ]);
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            LastOptions = options;
            await Task.CompletedTask;
            yield break;
        }

        public void Dispose()
        {
        }
    }
}
