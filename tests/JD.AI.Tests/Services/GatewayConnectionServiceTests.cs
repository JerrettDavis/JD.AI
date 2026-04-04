using JD.AI.Gateway.Client.Models;
using JD.AI.Services;

namespace JD.AI.Tests.Services;

[Collection("Console")]
public sealed class GatewayConnectionServiceTests
{
    [Fact]
    public async Task ConnectAsync_WhenGatewayIsUnhealthy_ReturnsFalseWithoutConnectingSignalR()
    {
        var (sut, http, signalR) = CreateSut();
        http.Healthy = false;

        var connected = await sut.ConnectAsync();

        Assert.False(connected);
        Assert.Equal(0, signalR.ConnectCalls);
    }

    [Fact]
    public async Task ConnectAsync_WhenGatewayIsHealthy_ReturnsSignalRConnectionState()
    {
        var (sut, http, signalR) = CreateSut();
        http.Healthy = true;
        signalR.IsConnected = true;

        var connected = await sut.ConnectAsync();

        Assert.True(connected);
        Assert.Equal(1, signalR.ConnectCalls);
    }

    [Fact]
    public async Task EnsureAgentAsync_WhenExistingAgentPresent_ReusesFirstAgent()
    {
        var (sut, http, _) = CreateSut();
        http.Agents =
        [
            new AgentInfo("agent-1", "openai", "gpt-5", 0, DateTimeOffset.UtcNow),
        ];

        var agentId = await sut.EnsureAgentAsync();

        Assert.Equal("agent-1", agentId);
        Assert.Equal("agent-1", sut.ActiveAgentId);
        Assert.Equal(0, http.SpawnAgentCalls);
    }

    [Fact]
    public async Task EnsureAgentAsync_WhenMatchingAgentExists_ReusesMatchingAgent()
    {
        var (sut, http, _) = CreateSut();
        http.Agents =
        [
            new AgentInfo("agent-1", "anthropic", "claude-sonnet-4-20250514", 0, DateTimeOffset.UtcNow),
            new AgentInfo("agent-2", "openai", "gpt-5.4", 0, DateTimeOffset.UtcNow),
        ];

        var agentId = await sut.EnsureAgentAsync("openai", "gpt-5.4");

        Assert.Equal("agent-2", agentId);
        Assert.Equal("agent-2", sut.ActiveAgentId);
        Assert.Equal(0, http.SpawnAgentCalls);
    }

    [Fact]
    public async Task EnsureAgentAsync_WhenNoAgentExists_SpawnsDefaultAgentDefinition()
    {
        var (sut, http, _) = CreateSut();
        http.Providers =
        [
            new ProviderInfo("openai", true, null, [new ProviderModelInfo("gpt-5.4", "GPT-5.4", "openai")]),
            new ProviderInfo("anthropic", false, "offline", [new ProviderModelInfo("claude-sonnet-4-20250514", "Claude Sonnet 4", "anthropic")]),
        ];
        http.SpawnedAgentId = "spawned-1";

        var agentId = await sut.EnsureAgentAsync();

        Assert.Equal("spawned-1", agentId);
        Assert.Equal("spawned-1", sut.ActiveAgentId);
        Assert.NotNull(http.LastSpawnDefinition);
        Assert.StartsWith("tui-", http.LastSpawnDefinition!.Id);
        Assert.Equal("openai", http.LastSpawnDefinition.Provider);
        Assert.Equal("gpt-5.4", http.LastSpawnDefinition.Model);
    }

    [Fact]
    public async Task EnsureAgentAsync_WhenProviderAndModelSpecified_UsesProvidedValues()
    {
        var (sut, http, _) = CreateSut();
        http.Providers =
        [
            new ProviderInfo("OpenAI", true, null, [new ProviderModelInfo("gpt-5.4", "GPT-5.4", "OpenAI")]),
        ];
        http.SpawnedAgentId = "spawned-2";

        var agentId = await sut.EnsureAgentAsync("openai", "GPT-5.4");

        Assert.Equal("spawned-2", agentId);
        Assert.NotNull(http.LastSpawnDefinition);
        Assert.Equal("OpenAI", http.LastSpawnDefinition!.Provider);
        Assert.Equal("gpt-5.4", http.LastSpawnDefinition.Model);
    }

    [Fact]
    public async Task EnsureAgentAsync_WhenProviderSpecifiedWithoutModel_UsesProviderFirstModel()
    {
        var (sut, http, _) = CreateSut();
        http.Providers =
        [
            new ProviderInfo("anthropic", true, null, [new ProviderModelInfo("claude-sonnet-4-20250514", "Claude Sonnet 4", "anthropic")]),
            new ProviderInfo("openai", true, null, [new ProviderModelInfo("gpt-5.4", "GPT-5.4", "openai")]),
        ];
        http.SpawnedAgentId = "spawned-3";

        var agentId = await sut.EnsureAgentAsync("openai");

        Assert.Equal("spawned-3", agentId);
        Assert.NotNull(http.LastSpawnDefinition);
        Assert.Equal("openai", http.LastSpawnDefinition!.Provider);
        Assert.Equal("gpt-5.4", http.LastSpawnDefinition.Model);
    }

    [Fact]
    public async Task EnsureAgentAsync_WhenModelSpecifiedWithoutProvider_UsesMatchingProvider()
    {
        var (sut, http, _) = CreateSut();
        http.Providers =
        [
            new ProviderInfo("anthropic", true, null, [new ProviderModelInfo("claude-sonnet-4-20250514", "Claude Sonnet 4", "anthropic")]),
            new ProviderInfo("openai", true, null, [new ProviderModelInfo("gpt-5.4", "GPT-5.4", "openai")]),
        ];
        http.SpawnedAgentId = "spawned-4";

        var agentId = await sut.EnsureAgentAsync(model: "gpt-5.4");

        Assert.Equal("spawned-4", agentId);
        Assert.NotNull(http.LastSpawnDefinition);
        Assert.Equal("openai", http.LastSpawnDefinition!.Provider);
        Assert.Equal("gpt-5.4", http.LastSpawnDefinition.Model);
    }

    [Fact]
    public async Task EnsureAgentAsync_WhenRequestedModelIsMissing_ReturnsNull()
    {
        var (sut, http, _) = CreateSut();
        http.Providers =
        [
            new ProviderInfo("openai", true, null, [new ProviderModelInfo("gpt-5.4", "GPT-5.4", "openai")]),
        ];

        var agentId = await sut.EnsureAgentAsync("openai", "does-not-exist");

        Assert.Null(agentId);
        Assert.Equal(0, http.SpawnAgentCalls);
    }

    [Fact]
    public async Task SendMessageStreamingAsync_WhenNoActiveAgent_YieldsNothing()
    {
        var (sut, _, signalR) = CreateSut();

        var chunks = await CollectAsync(sut.SendMessageStreamingAsync("hello"));

        Assert.Empty(chunks);
        Assert.Equal(0, signalR.StreamCalls);
    }

    [Fact]
    public async Task SendMessageStreamingAsync_WhenActiveAgentSet_FiltersNullChunks()
    {
        var (sut, http, signalR) = CreateSut();
        http.Agents =
        [
            new AgentInfo("agent-1", "openai", "gpt-5", 0, DateTimeOffset.UtcNow),
        ];
        signalR.StreamFactory = (_, _, _) => StreamChunks(
            new AgentStreamChunk("delta", "agent-1", null),
            new AgentStreamChunk("delta", "agent-1", "hi"),
            new AgentStreamChunk("delta", "agent-1", null),
            new AgentStreamChunk("delta", "agent-1", " there"));

        await sut.EnsureAgentAsync();
        var chunks = await CollectAsync(sut.SendMessageStreamingAsync("hello"));

        Assert.Equal(["hi", " there"], chunks);
        Assert.Equal("agent-1", signalR.LastAgentId);
        Assert.Equal("hello", signalR.LastMessage);
    }

    [Fact]
    public async Task SendMessageAsync_WhenNoActiveAgent_ReturnsNull()
    {
        var (sut, http, _) = CreateSut();

        var response = await sut.SendMessageAsync("hello");

        Assert.Null(response);
        Assert.Equal(0, http.SendMessageCalls);
    }

    [Fact]
    public async Task SendMessageAsync_WhenActiveAgentSet_UsesGatewayHttpClient()
    {
        var (sut, http, _) = CreateSut();
        http.Agents =
        [
            new AgentInfo("agent-1", "openai", "gpt-5", 0, DateTimeOffset.UtcNow),
        ];
        http.SendMessageResponse = "gateway-response";

        await sut.EnsureAgentAsync();
        var response = await sut.SendMessageAsync("hello");

        Assert.Equal("gateway-response", response);
        Assert.Equal("agent-1", http.LastSendAgentId);
        Assert.Equal("hello", http.LastSendMessage);
    }

    [Fact]
    public async Task GetLatestSessionAsync_WhenSessionExists_ReusesMostRecentSession()
    {
        var (sut, http, _) = CreateSut();
        http.Sessions =
        [
            new SessionInfo { Id = "session-1" },
        ];

        var sessionId = await sut.GetLatestSessionAsync();

        Assert.Equal("session-1", sessionId);
        Assert.Equal(0, http.GetAgentsCalls);
    }

    [Fact]
    public async Task GetLatestSessionAsync_WhenNoPersistedSessionExists_ReturnsNullWithoutSpawningAgent()
    {
        var (sut, http, _) = CreateSut();

        var sessionId = await sut.GetLatestSessionAsync();

        Assert.Null(sessionId);
        Assert.Equal(0, http.SpawnAgentCalls);
    }

    [Fact]
    public async Task GettersAndDisposeAsync_PassThroughToUnderlyingClients()
    {
        var (sut, http, signalR) = CreateSut();
        var status = new GatewayStatus { Status = "running" };
        var providers = new[] { new ProviderInfo("OpenAI", true, null, []) };
        var sessions = new[] { new SessionInfo { Id = "session-1" } };
        http.Status = status;
        http.Providers = providers;
        http.Sessions = sessions;

        Assert.Same(status, await sut.GetStatusAsync());
        Assert.Same(providers, await sut.GetProvidersAsync());
        Assert.Same(sessions, await sut.GetSessionsAsync(5));

        await sut.DisposeAsync();

        Assert.Equal(1, signalR.DisposeCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task GetSessionsAsync_WhenLimitIsOutOfRange_Throws(int limit)
    {
        var (sut, _, _) = CreateSut();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.GetSessionsAsync(limit));
    }

    [Fact]
    public async Task PrintStatus_WhenConnectedWithActiveAgent_PrintsConnectionAndAgentInfo()
    {
        var (sut, http, signalR) = CreateSut();
        http.Agents =
        [
            new AgentInfo("agent-1", "openai", "gpt-5", 0, DateTimeOffset.UtcNow),
        ];
        signalR.IsConnected = true;

        await sut.EnsureAgentAsync();
        SuppressConsoleOutput(sut.PrintStatus);

        Assert.Equal("agent-1", sut.ActiveAgentId);
        Assert.True(sut.IsConnected);
    }

    [Fact]
    public void PrintStatus_WhenDisconnectedWithError_PrintsError()
    {
        var (sut, _, signalR) = CreateSut();
        signalR.ConnectionError = "gateway unavailable";

        SuppressConsoleOutput(sut.PrintStatus);

        Assert.False(sut.IsConnected);
        Assert.Equal("http://localhost:5000", sut.GatewayUrl);
    }

    private static (GatewayConnectionService Sut, FakeGatewayHttpClient Http, FakeGatewaySignalRClient SignalR) CreateSut()
    {
        var http = new FakeGatewayHttpClient();
        var signalR = new FakeGatewaySignalRClient();
        return (new GatewayConnectionService("http://localhost:5000/", http, signalR), http, signalR);
    }

    private static void SuppressConsoleOutput(Action action)
    {
        var original = Console.Out;
        Console.SetOut(TextWriter.Null);

        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static async Task<List<string>> CollectAsync(IAsyncEnumerable<string> source)
    {
        var destination = new List<string>();
        await foreach (var item in source)
        {
            destination.Add(item);
        }

        return destination;
    }

    private static async IAsyncEnumerable<AgentStreamChunk> StreamChunks(params AgentStreamChunk[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return chunk;
            await Task.Yield();
        }
    }

    private sealed class FakeGatewayHttpClient : GatewayConnectionService.IGatewayHttpClient
    {
        public bool Healthy { get; set; }
        public AgentInfo[] Agents { get; set; } = [];
        public string? SpawnedAgentId { get; set; }
        public string? SendMessageResponse { get; set; }
        public SessionInfo[] Sessions { get; set; } = [];
        public GatewayStatus? Status { get; set; }
        public ProviderInfo[] Providers { get; set; } = [];
        public Queue<SessionInfo[]> SessionResponses { get; } = new();

        public int GetAgentsCalls { get; private set; }
        public int SpawnAgentCalls { get; private set; }
        public int SendMessageCalls { get; private set; }
        public AgentDefinition? LastSpawnDefinition { get; private set; }
        public string? LastSendAgentId { get; private set; }
        public string? LastSendMessage { get; private set; }

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(Healthy);

        public Task<AgentInfo[]> GetAgentsAsync(CancellationToken ct = default)
        {
            GetAgentsCalls++;
            return Task.FromResult(Agents);
        }

        public Task<string?> SpawnAgentAsync(AgentDefinition definition, CancellationToken ct = default)
        {
            SpawnAgentCalls++;
            LastSpawnDefinition = definition;
            return Task.FromResult(SpawnedAgentId);
        }

        public Task<string?> SendMessageAsync(string agentId, string message, CancellationToken ct = default)
        {
            SendMessageCalls++;
            LastSendAgentId = agentId;
            LastSendMessage = message;
            return Task.FromResult(SendMessageResponse);
        }

        public Task<SessionInfo[]> GetSessionsAsync(int limit = 50, CancellationToken ct = default)
        {
            if (SessionResponses.Count > 0)
                return Task.FromResult(SessionResponses.Dequeue());

            return Task.FromResult(Sessions);
        }

        public Task<GatewayStatus?> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(Status);

        public Task<ProviderInfo[]> GetProvidersAsync(CancellationToken ct = default)
            => Task.FromResult(Providers);
    }

    private sealed class FakeGatewaySignalRClient : GatewayConnectionService.IGatewaySignalRClient
    {
        public bool IsConnected { get; set; }
        public string? ConnectionError { get; set; }
        public int ConnectCalls { get; private set; }
        public int StreamCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public string? LastAgentId { get; private set; }
        public string? LastMessage { get; private set; }
        public Func<string, string, CancellationToken, IAsyncEnumerable<AgentStreamChunk>>? StreamFactory { get; set; }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalls++;
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(string agentId, string message, CancellationToken ct = default)
        {
            StreamCalls++;
            LastAgentId = agentId;
            LastMessage = message;
            return StreamFactory?.Invoke(agentId, message, ct) ?? EmptyStreamChunks();
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }

        private static async IAsyncEnumerable<AgentStreamChunk> EmptyStreamChunks()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
