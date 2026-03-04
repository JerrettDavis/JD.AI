using FluentAssertions;
using JD.AI.Core.Sessions;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class SessionPersistenceSteps : IDisposable
{
    private readonly ScenarioContext _context;
    private SessionStore? _store;
    private string? _tempDbPath;

    public SessionPersistenceSteps(ScenarioContext context) => _context = context;

    [Given(@"a session store backed by a temporary database")]
    public async Task GivenASessionStoreBackedByATemporaryDatabase()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"jdai-session-test-{Guid.NewGuid():N}.db");
        _store = new SessionStore(_tempDbPath);
        await _store.InitializeAsync();
        _context.Set(_store);
    }

    [Given(@"a session exists for project ""(.*)""")]
    [When(@"a new session is created for project ""(.*)""")]
    public async Task GivenASessionExistsForProject(string projectPath)
    {
        var store = _context.Get<SessionStore>();
        var session = new SessionInfo
        {
            ProjectPath = projectPath,
            ProjectHash = ProjectHasher.Hash(projectPath),
        };
        await store.CreateSessionAsync(session);

        // Store for later use; keep track of multiple sessions
        if (!_context.TryGetValue<List<SessionInfo>>("sessions", out var sessions))
        {
            sessions = [];
            _context.Set(sessions, "sessions");
        }
        sessions.Add(session);
        _context.Set(session, "currentSession");
    }

    [Given(@"a user turn ""(.*)"" is saved to the session")]
    public async Task GivenAUserTurnIsSaved(string content)
    {
        var store = _context.Get<SessionStore>();
        var session = _context.Get<SessionInfo>("currentSession");
        var turn = new TurnRecord
        {
            SessionId = session.Id,
            TurnIndex = session.Turns.Count,
            Role = "user",
            Content = content,
        };
        session.Turns.Add(turn);
        session.MessageCount++;
        await store.SaveTurnAsync(turn);
        await store.UpdateSessionAsync(session);
    }

    [Given(@"an assistant turn ""(.*)"" is saved to the session")]
    public async Task GivenAnAssistantTurnIsSaved(string content)
    {
        var store = _context.Get<SessionStore>();
        var session = _context.Get<SessionInfo>("currentSession");
        var turn = new TurnRecord
        {
            SessionId = session.Id,
            TurnIndex = session.Turns.Count,
            Role = "assistant",
            Content = content,
        };
        session.Turns.Add(turn);
        session.MessageCount++;
        await store.SaveTurnAsync(turn);
        await store.UpdateSessionAsync(session);
    }

    [When(@"the session is loaded by its ID")]
    public async Task WhenTheSessionIsLoadedByItsId()
    {
        var store = _context.Get<SessionStore>();
        var session = _context.Get<SessionInfo>("currentSession");
        var loaded = await store.GetSessionAsync(session.Id);
        _context.Set(loaded, "loadedSession");
    }

    [When(@"sessions are listed")]
    public async Task WhenSessionsAreListed()
    {
        var store = _context.Get<SessionStore>();
        var list = await store.ListSessionsAsync();
        _context.Set(list, "sessionList");
    }

    [When(@"turns after index (\d+) are deleted")]
    public async Task WhenTurnsAfterIndexAreDeleted(int turnIndex)
    {
        var store = _context.Get<SessionStore>();
        var session = _context.Get<SessionInfo>("currentSession");
        await store.DeleteTurnsAfterAsync(session.Id, turnIndex);
    }

    [When(@"the session is closed")]
    public async Task WhenTheSessionIsClosed()
    {
        var store = _context.Get<SessionStore>();
        var session = _context.Get<SessionInfo>("currentSession");
        await store.CloseSessionAsync(session.Id);
    }

    [Then(@"the session should have a non-empty ID")]
    public void ThenTheSessionShouldHaveANonEmptyId()
    {
        var session = _context.Get<SessionInfo>("currentSession");
        session.Id.Should().NotBeNullOrEmpty();
    }

    [Then(@"the session should be marked as active")]
    public void ThenTheSessionShouldBeMarkedAsActive()
    {
        var session = _context.Get<SessionInfo>("currentSession");
        session.IsActive.Should().BeTrue();
    }

    [Then(@"the session should have project path ""(.*)""")]
    public void ThenTheSessionShouldHaveProjectPath(string path)
    {
        var session = _context.Get<SessionInfo>("currentSession");
        session.ProjectPath.Should().Be(path);
    }

    [Then(@"the loaded session should have the same ID")]
    public void ThenTheLoadedSessionShouldHaveTheSameId()
    {
        var original = _context.Get<SessionInfo>("currentSession");
        var loaded = _context.Get<SessionInfo?>("loadedSession");
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(original.Id);
    }

    [Then(@"the loaded session should be marked as active")]
    public void ThenTheLoadedSessionShouldBeMarkedAsActive()
    {
        var loaded = _context.Get<SessionInfo?>("loadedSession");
        loaded!.IsActive.Should().BeTrue();
    }

    [Then(@"the loaded session should be marked as inactive")]
    public void ThenTheLoadedSessionShouldBeMarkedAsInactive()
    {
        var loaded = _context.Get<SessionInfo?>("loadedSession");
        loaded!.IsActive.Should().BeFalse();
    }

    [Then(@"the loaded session should have (\d+) turns")]
    public void ThenTheLoadedSessionShouldHaveTurns(int count)
    {
        var loaded = _context.Get<SessionInfo?>("loadedSession");
        loaded!.Turns.Should().HaveCount(count);
    }

    [Then(@"the first turn should have role ""(.*)"" and content ""(.*)""")]
    public void ThenTheFirstTurnShouldHaveRoleAndContent(string role, string content)
    {
        var loaded = _context.Get<SessionInfo?>("loadedSession");
        loaded!.Turns[0].Role.Should().Be(role);
        loaded.Turns[0].Content.Should().Be(content);
    }

    [Then(@"the second turn should have role ""(.*)"" and content ""(.*)""")]
    public void ThenTheSecondTurnShouldHaveRoleAndContent(string role, string content)
    {
        var loaded = _context.Get<SessionInfo?>("loadedSession");
        loaded!.Turns[1].Role.Should().Be(role);
        loaded.Turns[1].Content.Should().Be(content);
    }

    [Then(@"the session list should contain (\d+) entries")]
    public void ThenTheSessionListShouldContainEntries(int count)
    {
        var list = _context.Get<System.Collections.ObjectModel.ReadOnlyCollection<SessionInfo>>("sessionList");
        list.Should().HaveCount(count);
    }

    public void Dispose()
    {
        _store?.Dispose();
        if (_tempDbPath != null && File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { /* best-effort */ }
        }
    }
}
