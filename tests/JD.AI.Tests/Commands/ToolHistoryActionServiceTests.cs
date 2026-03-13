using JD.AI.Commands;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tests.Commands;

public sealed class ToolHistoryActionServiceTests
{
    [Fact]
    public async Task ViewDetails_ReturnsExpectedSummary()
    {
        var session = CreateSession();
        var service = new ToolHistoryActionService(session, configStore: null, checkpointStrategy: null);
        var entry = CreateEntry();

        var result = await service.ApplyAsync(entry, ToolHistoryAction.ViewDetails);

        Assert.Contains("Tool: run_command", result);
        Assert.Contains("Status: ok", result);
        Assert.Contains("Args: command=ls", result);
    }

    [Fact]
    public async Task AllowGlobal_UpdatesSessionProfile_AndPersistsConfig()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-global-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var session = CreateSession(tempDirectory);
            using var store = new AtomicConfigStore(Path.Combine(tempDirectory, "config.json"));
            var service = new ToolHistoryActionService(session, store, checkpointStrategy: null);
            var entry = CreateEntry(toolName: "bash");

            var result = await service.ApplyAsync(entry, ToolHistoryAction.AllowGlobal);
            var profile = await store.GetToolPermissionProfileAsync(tempDirectory);

            Assert.Contains("Allowed", result);
            Assert.Contains("run_command", profile.GlobalAllowed);
            Assert.True(session.ToolPermissionProfile.IsExplicitlyAllowed("run_command"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AllowProject_UpdatesSessionProfile_AndPersistsConfig()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-project-allow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var session = CreateSession(tempDirectory);
            using var store = new AtomicConfigStore(Path.Combine(tempDirectory, "config.json"));
            var service = new ToolHistoryActionService(session, store, checkpointStrategy: null);
            var entry = CreateEntry(toolName: "read_file");

            var result = await service.ApplyAsync(entry, ToolHistoryAction.AllowProject);
            var profile = await store.GetToolPermissionProfileAsync(tempDirectory);

            Assert.Contains("Allowed", result);
            Assert.Contains("read_file", profile.ProjectAllowed);
            Assert.True(session.ToolPermissionProfile.IsExplicitlyAllowed("read_file"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DenyGlobal_UpdatesSessionProfile_AndPersistsConfig()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-global-deny-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var session = CreateSession(tempDirectory);
            using var store = new AtomicConfigStore(Path.Combine(tempDirectory, "config.json"));
            var service = new ToolHistoryActionService(session, store, checkpointStrategy: null);
            var entry = CreateEntry(toolName: "run_command");

            var result = await service.ApplyAsync(entry, ToolHistoryAction.DenyGlobal);
            var profile = await store.GetToolPermissionProfileAsync(tempDirectory);

            Assert.Contains("Denied", result);
            Assert.Contains("run_command", profile.GlobalDenied);
            Assert.True(session.ToolPermissionProfile.IsExplicitlyDenied("run_command"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DenyProject_UpdatesSessionProfile_AndPersistsConfig()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-project-deny-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var session = CreateSession(tempDirectory);
            using var store = new AtomicConfigStore(Path.Combine(tempDirectory, "config.json"));
            var service = new ToolHistoryActionService(session, store, checkpointStrategy: null);
            var entry = CreateEntry(toolName: "git_push");

            var result = await service.ApplyAsync(entry, ToolHistoryAction.DenyProject);
            var profile = await store.GetToolPermissionProfileAsync(tempDirectory);

            Assert.Contains("Denied", result);
            Assert.Contains("git_push", profile.ProjectDenied);
            Assert.True(session.ToolPermissionProfile.IsExplicitlyDenied("git_push"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RewindBefore_RemovesTurnsAfterPreviousTurn()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-rewind-before-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await using var fixture = await CreateSessionStoreFixtureAsync(tempDirectory, turnCount: 4);
            var service = new ToolHistoryActionService(fixture.Session, configStore: null, checkpointStrategy: null);
            var entry = CreateEntry(sessionId: fixture.Session.SessionInfo!.Id, turnIndex: 2);

            var result = await service.ApplyAsync(entry, ToolHistoryAction.RewindBefore);

            Assert.Contains("turn 1", result);
            Assert.Equal(2, fixture.Session.SessionInfo!.Turns.Count);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RewindAfter_RemovesTurnsAfterSelectedTurn()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-rewind-after-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await using var fixture = await CreateSessionStoreFixtureAsync(tempDirectory, turnCount: 5);
            var service = new ToolHistoryActionService(fixture.Session, configStore: null, checkpointStrategy: null);
            var entry = CreateEntry(sessionId: fixture.Session.SessionInfo!.Id, turnIndex: 2);

            var result = await service.ApplyAsync(entry, ToolHistoryAction.RewindAfter);

            Assert.Contains("turn 2", result);
            Assert.Equal(3, fixture.Session.SessionInfo!.Turns.Count);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Rewind_RejectsCrossSessionRequests()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-rewind-cross-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await using var fixture = await CreateSessionStoreFixtureAsync(tempDirectory, turnCount: 3);
            var service = new ToolHistoryActionService(fixture.Session, configStore: null, checkpointStrategy: null);
            var entry = CreateEntry(sessionId: "different-session", turnIndex: 1);

            var result = await service.ApplyAsync(entry, ToolHistoryAction.RewindBefore);

            Assert.Contains("only supported for the current active session", result);
            Assert.Equal(3, fixture.Session.SessionInfo!.Turns.Count);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RewindWithCheckpointRestore_Success_ReturnsRestoredMessage()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-rewind-restore-ok-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await using var fixture = await CreateSessionStoreFixtureAsync(tempDirectory, turnCount: 4);
            var checkpoint = new FakeCheckpointStrategy(
                checkpoints: [new CheckpointInfo("cp-123", "checkpoint", DateTime.UtcNow)],
                restoreResult: true);
            var service = new ToolHistoryActionService(fixture.Session, configStore: null, checkpoint);
            var entry = CreateEntry(sessionId: fixture.Session.SessionInfo!.Id, turnIndex: 2);

            var result = await service.ApplyAsync(
                entry,
                ToolHistoryAction.RewindAfter,
                new ToolHistoryActionRequest(RestoreLatestCheckpoint: true));

            Assert.Contains("restored checkpoint cp-123", result);
            Assert.Equal("cp-123", checkpoint.LastRestoreId);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RewindWithCheckpointRestore_Failure_ReturnsFailureMessage()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"jdai-tool-history-rewind-restore-fail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await using var fixture = await CreateSessionStoreFixtureAsync(tempDirectory, turnCount: 4);
            var checkpoint = new FakeCheckpointStrategy(
                checkpoints: [new CheckpointInfo("cp-999", "checkpoint", DateTime.UtcNow)],
                restoreResult: false);
            var service = new ToolHistoryActionService(fixture.Session, configStore: null, checkpoint);
            var entry = CreateEntry(sessionId: fixture.Session.SessionInfo!.Id, turnIndex: 2);

            var result = await service.ApplyAsync(
                entry,
                ToolHistoryAction.RewindAfter,
                new ToolHistoryActionRequest(RestoreLatestCheckpoint: true));

            Assert.Contains("Checkpoint restore failed", result);
            Assert.Equal("cp-999", checkpoint.LastRestoreId);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static AgentSession CreateSession(string? projectPath = null)
    {
        var registry = Substitute.For<IProviderRegistry>();
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test-model", "Test Model", "TestProvider");
        var session = new AgentSession(registry, kernel, model);
        session.SessionInfo = new JD.AI.Core.Sessions.SessionInfo
        {
            ProjectPath = projectPath ?? Directory.GetCurrentDirectory(),
            ProjectHash = "testhash",
        };
        return session;
    }

    private static ToolHistoryEntry CreateEntry(
        string toolName = "run_command",
        string arguments = "command=ls",
        string result = "line1\nline2\nline3",
        string sessionId = "session-1",
        int turnIndex = 3)
    {
        return new ToolHistoryEntry(
            SessionId: sessionId,
            SessionName: "Session 1",
            TurnIndex: turnIndex,
            CreatedAt: DateTime.UtcNow,
            ToolName: toolName,
            Arguments: arguments,
            Result: result,
            Status: "ok",
            DurationMs: 123,
            Label: "label");
    }

    private static async Task<SessionStoreFixture> CreateSessionStoreFixtureAsync(
        string tempDirectory,
        int turnCount)
    {
        var session = CreateSession(tempDirectory);
        var store = new SessionStore(Path.Combine(tempDirectory, "sessions.db"));
        await store.InitializeAsync();
        session.Store = store;
        session.SessionInfo = new SessionInfo
        {
            Id = Guid.NewGuid().ToString("N")[..16],
            ProjectPath = tempDirectory,
            ProjectHash = "hash",
        };
        await store.CreateSessionAsync(session.SessionInfo);

        session.CaptureOriginalSystemPromptIfUnset("System prompt");
        session.History.AddSystemMessage("System prompt");

        for (var i = 0; i < turnCount; i++)
        {
            var role = i % 2 == 0 ? "user" : "assistant";
            var turn = new TurnRecord
            {
                SessionId = session.SessionInfo.Id,
                TurnIndex = i,
                Role = role,
                Content = $"turn-{i}",
            };
            session.SessionInfo.Turns.Add(turn);
            await store.SaveTurnAsync(turn);

            if (string.Equals(role, "user", StringComparison.Ordinal))
                session.History.AddUserMessage(turn.Content);
            else
                session.History.AddAssistantMessage(turn.Content);
        }

        return new SessionStoreFixture(session, store);
    }

    private sealed class SessionStoreFixture : IAsyncDisposable
    {
        public SessionStoreFixture(AgentSession session, SessionStore store)
        {
            Session = session;
            Store = store;
        }

        public AgentSession Session { get; }
        public SessionStore Store { get; }

        public ValueTask DisposeAsync()
        {
            Store.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeCheckpointStrategy : ICheckpointStrategy
    {
        private readonly IReadOnlyList<CheckpointInfo> _checkpoints;
        private readonly bool _restoreResult;

        public FakeCheckpointStrategy(
            IReadOnlyList<CheckpointInfo> checkpoints,
            bool restoreResult)
        {
            _checkpoints = checkpoints;
            _restoreResult = restoreResult;
        }

        public string? LastRestoreId { get; private set; }

        public Task<string?> CreateAsync(string label, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<CheckpointInfo>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult(_checkpoints);

        public Task<bool> RestoreAsync(string checkpointId, CancellationToken ct = default)
        {
            LastRestoreId = checkpointId;
            return Task.FromResult(_restoreResult);
        }

        public Task ClearAsync(CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
