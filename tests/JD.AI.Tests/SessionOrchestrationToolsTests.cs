using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Tools;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tests;

public sealed class SessionOrchestrationToolsTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SessionStore _store;
    private readonly AgentSession _session;
    private readonly SessionOrchestrationTools _tools;
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();

    public SessionOrchestrationToolsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}.db");
        _store = new SessionStore(_dbPath);
        _store.InitializeAsync().GetAwaiter().GetResult();

        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");

        _session = new AgentSession(_registry, kernel, model)
        {
            Store = _store,
            SessionInfo = new SessionInfo
            {
                ProjectPath = "/test/project",
                ProjectHash = "abc12345",
                Name = "test-session",
            },
        };

        _tools = new SessionOrchestrationTools(_session);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private AgentSession CreateEmptySession()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        return new AgentSession(_registry, kernel, model);
    }

    // ── session_status ───────────────────────────────────

    [Fact]
    public void GetSessionStatus_ReturnsFormattedInfo()
    {
        var result = _tools.GetSessionStatus();

        result.Should().Contain("Session Status");
        result.Should().Contain(_session.SessionInfo!.Id);
        result.Should().Contain("test-session");
        result.Should().Contain("/test/project");
        result.Should().Contain("ACTIVE");
    }

    [Fact]
    public void GetSessionStatus_NoSession_ReturnsMessage()
    {
        var tools = new SessionOrchestrationTools(CreateEmptySession());

        var result = tools.GetSessionStatus();

        result.Should().Be("No active session.");
    }

    // ── agents_list ──────────────────────────────────────

    [Fact]
    public void ListAgents_ReturnsAgentTypes()
    {
        var result = _tools.ListAgents();

        result.Should().Contain("explore");
        result.Should().Contain("task");
        result.Should().Contain("plan");
        result.Should().Contain("review");
        result.Should().Contain("general");
        result.Should().Contain("sequential");
        result.Should().Contain("fan-out");
        result.Should().Contain("supervisor");
        result.Should().Contain("debate");
    }

    // ── sessions_list ────────────────────────────────────

    [Fact]
    public async Task ListSessions_NoStore_ReturnsError()
    {
        var tools = new SessionOrchestrationTools(CreateEmptySession());

        var result = await tools.ListSessionsAsync();

        result.Should().Contain("persistence is not enabled");
    }

    [Fact]
    public async Task ListSessions_EmptyStore_ReturnsNoSessions()
    {
        var result = await _tools.ListSessionsAsync();

        result.Should().Contain("No sessions found");
    }

    [Fact]
    public async Task ListSessions_WithSessions_ReturnsFormatted()
    {
        await _store.CreateSessionAsync(_session.SessionInfo!);

        var result = await _tools.ListSessionsAsync();

        result.Should().Contain("Sessions (1)");
        result.Should().Contain(_session.SessionInfo!.Id);
        result.Should().Contain("current");
    }

    [Fact]
    public async Task ListSessions_FilterActive_OnlyActive()
    {
        var activeInfo = new SessionInfo
        {
            ProjectPath = "/test/project",
            ProjectHash = "abc12345",
            IsActive = true,
            Name = "active-one",
        };
        var closedInfo = new SessionInfo
        {
            ProjectPath = "/test/project",
            ProjectHash = "abc12345",
            IsActive = false,
            Name = "closed-one",
        };
        await _store.CreateSessionAsync(activeInfo);
        await _store.CreateSessionAsync(closedInfo);
        await _store.CloseSessionAsync(closedInfo.Id);

        var result = await _tools.ListSessionsAsync(filter: "active");

        result.Should().Contain("active-one");
    }

    // ── sessions_history ─────────────────────────────────

    [Fact]
    public async Task GetHistory_NoStore_ReturnsError()
    {
        var tools = new SessionOrchestrationTools(CreateEmptySession());

        var result = await tools.GetSessionHistoryAsync();

        result.Should().Contain("persistence is not enabled");
    }

    [Fact]
    public async Task GetHistory_SessionNotFound_ReturnsError()
    {
        var result = await _tools.GetSessionHistoryAsync("nonexistent");

        result.Should().Contain("not found");
    }

    [Fact]
    public async Task GetHistory_CrossProjectAccess_Denied()
    {
        var otherSession = new SessionInfo
        {
            ProjectPath = "/other/project",
            ProjectHash = "xyz99999",
            Name = "other-project",
        };
        await _store.CreateSessionAsync(otherSession);

        var result = await _tools.GetSessionHistoryAsync(otherSession.Id);

        result.Should().Contain("Access denied");
    }

    // ── sessions_spawn ───────────────────────────────────

    [Fact]
    public async Task SpawnSession_New_CreatesSession()
    {
        var result = await _tools.SpawnSessionAsync("new", "spawned-session");

        result.Should().Contain("New session created");
        result.Should().Contain("spawned-session");
    }

    [Fact]
    public async Task SpawnSession_NoStore_ReturnsError()
    {
        var tools = new SessionOrchestrationTools(CreateEmptySession());

        var result = await tools.SpawnSessionAsync("new");

        result.Should().Contain("persistence is not enabled");
    }

    // ── sessions_send ────────────────────────────────────

    [Fact]
    public async Task SendMessage_EmptyMessage_ReturnsError()
    {
        var result = await _tools.SendMessageAsync("   ");

        result.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task SendMessage_NoStore_ReturnsError()
    {
        var tools = new SessionOrchestrationTools(CreateEmptySession());

        var result = await tools.SendMessageAsync("hello");

        result.Should().Contain("persistence is not enabled");
    }
}
