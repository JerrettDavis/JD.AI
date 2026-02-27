using JD.AI.Tui.Persistence;

namespace JD.AI.Tui.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SessionStore _store;

    public SessionStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"jdai_test_{Guid.NewGuid():N}.db");
        _store = new SessionStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task InitializeAsync_CreatesDatabase()
    {
        await _store.InitializeAsync();
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public async Task CreateSession_RoundTrips()
    {
        await _store.InitializeAsync();

        var session = new SessionInfo
        {
            Id = "test1234abcdef01",
            Name = "Test Session",
            ProjectPath = "/tmp/test",
            ProjectHash = "abc12345",
            ModelId = "gpt-4",
            ProviderName = "openai",
        };

        await _store.CreateSessionAsync(session);
        var loaded = await _store.GetSessionAsync("test1234abcdef01");

        Assert.NotNull(loaded);
        Assert.Equal("Test Session", loaded.Name);
        Assert.Equal("/tmp/test", loaded.ProjectPath);
        Assert.Equal("abc12345", loaded.ProjectHash);
        Assert.Equal("gpt-4", loaded.ModelId);
        Assert.True(loaded.IsActive);
    }

    [Fact]
    public async Task SaveTurn_WithToolCallsAndFiles()
    {
        await _store.InitializeAsync();

        var session = new SessionInfo
        {
            Id = "sess123456789012",
            ProjectPath = "/tmp/test",
            ProjectHash = "abc12345",
        };
        await _store.CreateSessionAsync(session);

        var turn = new TurnRecord
        {
            Id = "turn123456789012",
            SessionId = "sess123456789012",
            TurnIndex = 0,
            Role = "assistant",
            Content = "Hello!",
            ThinkingText = "Let me think...",
            ModelId = "gpt-4",
            TokensIn = 100,
            TokensOut = 50,
            DurationMs = 500,
        };
        turn.ToolCalls.Add(new ToolCallRecord
        {
            Id = "tc01234567890123",
            TurnId = "turn123456789012",
            ToolName = "file_read",
            Arguments = """{"path":"test.cs"}""",
            Result = "file contents",
            DurationMs = 10,
        });
        turn.FilesTouched.Add(new FileTouchRecord
        {
            Id = "ft01234567890123",
            TurnId = "turn123456789012",
            FilePath = "test.cs",
            Operation = "read",
        });

        await _store.SaveTurnAsync(turn);

        var loaded = await _store.GetSessionAsync("sess123456789012");
        Assert.NotNull(loaded);
        Assert.Single(loaded.Turns);
        Assert.Equal("Hello!", loaded.Turns[0].Content);
        Assert.Equal("Let me think...", loaded.Turns[0].ThinkingText);
        Assert.Single(loaded.Turns[0].ToolCalls);
        Assert.Equal("file_read", loaded.Turns[0].ToolCalls[0].ToolName);
        Assert.Single(loaded.Turns[0].FilesTouched);
        Assert.Equal("test.cs", loaded.Turns[0].FilesTouched[0].FilePath);
    }

    [Fact]
    public async Task ListSessions_FiltersByProjectHash()
    {
        await _store.InitializeAsync();

        await _store.CreateSessionAsync(new SessionInfo
        {
            Id = "aaaa123456789012",
            ProjectPath = "/a",
            ProjectHash = "hashAAAA",
        });
        await _store.CreateSessionAsync(new SessionInfo
        {
            Id = "bbbb123456789012",
            ProjectPath = "/b",
            ProjectHash = "hashBBBB",
        });

        var all = await _store.ListSessionsAsync();
        Assert.Equal(2, all.Count);

        var filtered = await _store.ListSessionsAsync("hashAAAA");
        Assert.Single(filtered);
        Assert.Equal("aaaa123456789012", filtered[0].Id);
    }

    [Fact]
    public async Task DeleteTurnsAfter_RemovesTurnsAndChildren()
    {
        await _store.InitializeAsync();

        await _store.CreateSessionAsync(new SessionInfo
        {
            Id = "del0123456789012",
            ProjectPath = "/tmp/del",
            ProjectHash = "delhash1",
        });

        for (var i = 0; i < 5; i++)
        {
            await _store.SaveTurnAsync(new TurnRecord
            {
                Id = $"turn{i:D16}",
                SessionId = "del0123456789012",
                TurnIndex = i,
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = $"Turn {i}",
            });
        }

        // Delete turns after index 2
        await _store.DeleteTurnsAfterAsync("del0123456789012", 2);

        var loaded = await _store.GetSessionAsync("del0123456789012");
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.Turns.Count); // indices 0, 1, 2
    }

    [Fact]
    public async Task CloseSession_SetsInactive()
    {
        await _store.InitializeAsync();

        await _store.CreateSessionAsync(new SessionInfo
        {
            Id = "close12345678901",
            ProjectPath = "/tmp/close",
            ProjectHash = "closhash",
        });

        await _store.CloseSessionAsync("close12345678901");

        var loaded = await _store.GetSessionAsync("close12345678901");
        Assert.NotNull(loaded);
        Assert.False(loaded.IsActive);
    }

    [Fact]
    public async Task UpdateSession_PersistsChanges()
    {
        await _store.InitializeAsync();

        var session = new SessionInfo
        {
            Id = "updt123456789012",
            ProjectPath = "/tmp/updt",
            ProjectHash = "updthash",
        };
        await _store.CreateSessionAsync(session);

        session.Name = "Updated Name";
        session.MessageCount = 42;
        session.TotalTokens = 9001;
        await _store.UpdateSessionAsync(session);

        var loaded = await _store.GetSessionAsync("updt123456789012");
        Assert.NotNull(loaded);
        Assert.Equal("Updated Name", loaded.Name);
        Assert.Equal(42, loaded.MessageCount);
        Assert.Equal(9001, loaded.TotalTokens);
    }
}
