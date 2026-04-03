using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Services;

public sealed class AgentRouterTests
{
    [Fact]
    public void MapChannel_ReturnsSnapshotAndCaseInsensitiveLookup()
    {
        var (router, _, _, _) = CreateSut();

        router.MapChannel("discord", "agent-1");
        var snapshot = router.GetMappings();
        router.MapChannel("slack", "agent-2");

        router.GetAgentForChannel("DISCORD").Should().Be("agent-1");
        snapshot.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("discord", "agent-1"));
        router.GetMappings().Should().HaveCount(2);
    }

    [Fact]
    public async Task RouteAsync_WhenNoAgentMapped_PublishesUnroutedEvent()
    {
        var (router, _, _, events) = CreateSut();
        GatewayEvent? published = null;
        using var subscription = events.Subscribe("message.unrouted", evt =>
        {
            published = evt;
            return Task.CompletedTask;
        });

        var response = await router.RouteAsync(CreateMessage("discord"));

        response.Should().BeNull();
        published.Should().NotBeNull();
        published!.EventType.Should().Be("message.unrouted");
        published.SourceId.Should().Be("discord");
        published.Payload.Should().Be($"No agent for channel discord");
    }

    [Fact]
    public async Task RouteAsync_WhenSourceChannelProvided_SendsResponseBackToSourceChannel()
    {
        var (router, pool, _, _) = CreateSut(("fake", "model-a", "source-response"));
        var agentId = await pool.SpawnAgentAsync("fake", "model-a", null, CancellationToken.None);
        router.MapChannel("discord", agentId);
        var sourceChannel = new TestChannel("slack");

        var response = await router.RouteAsync(CreateMessage("discord"), sourceChannel);

        response.Should().Be("source-response");
        sourceChannel.LastConversationId.Should().Be("discord");
        sourceChannel.LastSentContent.Should().Be("source-response");
    }

    [Fact]
    public async Task RouteAsync_WhenRouteKeyIsPresent_UsesRouteMappingWhenChannelIdIsUnmapped()
    {
        var (router, pool, _, _) = CreateSut(
            ("channel-provider", "model-a", "channel-response"),
            ("route-provider", "model-b", "route-response"));
        var channelAgentId = await pool.SpawnAgentAsync("channel-provider", "model-a", null, CancellationToken.None);
        var routeAgentId = await pool.SpawnAgentAsync("route-provider", "model-b", null, CancellationToken.None);
        router.MapChannel("discord", channelAgentId);
        router.MapChannel("vip-route", routeAgentId);

        var response = await router.RouteAsync(CreateMessage(
            "support",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AgentRouter.RouteKeyMetadataKey] = "vip-route",
            }));

        response.Should().Be("route-response");
    }

    [Fact]
    public async Task RouteAsync_WhenChannelTypeMetadataPresent_UsesRegisteredChannelForResponse()
    {
        var (router, pool, channels, _) = CreateSut(("fake", "model-a", "metadata-response"));
        var agentId = await pool.SpawnAgentAsync("fake", "model-a", null, CancellationToken.None);
        router.MapChannel("support", agentId);
        var responseChannel = new TestChannel("discord");
        channels.Register(responseChannel);

        var response = await router.RouteAsync(CreateMessage(
            "support",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AgentRouter.ChannelTypeMetadataKey] = "discord",
            }));

        response.Should().Be("metadata-response");
        responseChannel.LastConversationId.Should().Be("support");
        responseChannel.LastSentContent.Should().Be("metadata-response");
    }

    [Fact]
    public async Task RouteAsync_WhenChannelTypeMetadataIsWhitespace_FallsBackToChannelIdChannel()
    {
        var (router, pool, channels, _) = CreateSut(("fake", "model-a", "fallback-response"));
        var agentId = await pool.SpawnAgentAsync("fake", "model-a", null, CancellationToken.None);
        router.MapChannel("discord", agentId);
        var responseChannel = new TestChannel("discord");
        channels.Register(responseChannel);

        var response = await router.RouteAsync(CreateMessage(
            "discord",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AgentRouter.ChannelTypeMetadataKey] = "   ",
            }));

        response.Should().Be("fallback-response");
        responseChannel.LastConversationId.Should().Be("discord");
        responseChannel.LastSentContent.Should().Be("fallback-response");
    }

    private static ChannelMessage CreateMessage(
        string channelId,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            ChannelId = channelId,
            SenderId = "user-1",
            SenderDisplayName = "User",
            Content = "hello",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Attachments = [],
        };

    private static (AgentRouter Router, AgentPoolService Pool, ChannelRegistry Channels, InProcessEventBus Events) CreateSut(
        params (string Provider, string Model, string Response)[] providers)
    {
        providers = providers.Length == 0 ? [("fake", "model-a", "default-response")] : providers;

        var registry = Substitute.For<IProviderRegistry>();
        var providerInfos = providers
            .Select(p => new ProviderInfo(
                p.Provider,
                true,
                null,
                [new ProviderModelInfo(p.Model, p.Model, p.Provider)]))
            .ToArray();
        var detectors = providers.ToDictionary(
            p => p.Provider,
            p =>
            {
                var detector = Substitute.For<IProviderDetector>();
                var kernel = CreateKernel(p.Response);
                detector.BuildKernel(Arg.Any<ProviderModelInfo>()).Returns(kernel);
                return detector;
            },
            StringComparer.OrdinalIgnoreCase);

        registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(providerInfos));
        registry.DetectProvidersAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(providerInfos));
        registry.GetDetector(Arg.Any<string>())
            .Returns(call =>
            {
                var providerName = call.ArgAt<string>(0);
                return detectors.TryGetValue(providerName, out var detector) ? detector : null;
            });

        var channels = new ChannelRegistry();
        var events = new InProcessEventBus();
        var pool = new AgentPoolService(registry, channels, events, NullLogger<AgentPoolService>.Instance);
        var router = new AgentRouter(pool, channels, events, NullLogger<AgentRouter>.Instance);
        return (router, pool, channels, events);
    }

    private static Kernel CreateKernel(string response)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(new FakeChatCompletionService(response));
        return builder.Build();
    }

    private sealed class TestChannel : IChannel
    {
        public TestChannel(string channelType)
        {
            ChannelType = channelType;
            DisplayName = channelType;
        }

        public string ChannelType { get; }
        public string DisplayName { get; }
        public bool IsConnected => true;
        public string? LastConversationId { get; private set; }
        public string? LastSentContent { get; private set; }

#pragma warning disable CS0067
        public event Func<ChannelMessage, Task>? MessageReceived;
#pragma warning restore CS0067

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
        {
            LastConversationId = conversationId;
            LastSentContent = content;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeChatCompletionService(string response) : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; } =
            new Dictionary<string, object?>(StringComparer.Ordinal);

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatMessageContent>>(
                [new ChatMessageContent(AuthorRole.Assistant, response)]);

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, response);
            await Task.CompletedTask;
        }
    }
}
