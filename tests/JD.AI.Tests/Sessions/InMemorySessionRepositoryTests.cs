using JD.AI.Core.Sessions;

namespace JD.AI.Tests.Sessions;

public sealed class InMemorySessionRepositoryTests
{
    private readonly InMemorySessionRepository _sut = new();

    private static SessionRecord MakeSession(string id = "s1", string project = "/test") =>
        new()
        {
            Id = id,
            ProjectPath = project,
            Name = $"Session {id}",
        };

    [Fact]
    public async Task CreateAndGet_ReturnsSession()
    {
        var session = MakeSession();
        await _sut.CreateSessionAsync(session);

        var result = await _sut.GetSessionAsync("s1");

        Assert.NotNull(result);
        Assert.Equal("s1", result.Id);
        Assert.Equal("/test", result.ProjectPath);
    }

    [Fact]
    public async Task GetSession_NotFound_ReturnsNull()
    {
        var result = await _sut.GetSessionAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSession_SetsUpdatedAt()
    {
        var session = MakeSession();
        await _sut.CreateSessionAsync(session);

        session.Name = "Updated";
        await _sut.UpdateSessionAsync(session);

        var result = await _sut.GetSessionAsync("s1");
        Assert.Equal("Updated", result!.Name);
    }

    [Fact]
    public async Task UpdateSession_NotFound_ThrowsKeyNotFound()
    {
        var session = MakeSession("missing");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.UpdateSessionAsync(session));
    }

    [Fact]
    public async Task CreateSession_NullArg_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateSessionAsync(null!));
    }

    [Fact]
    public async Task DeleteSession_RemovesRecord()
    {
        await _sut.CreateSessionAsync(MakeSession());
        await _sut.DeleteSessionAsync("s1");

        var result = await _sut.GetSessionAsync("s1");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteSession_Nonexistent_DoesNotThrow()
    {
        await _sut.DeleteSessionAsync("nope"); // Should not throw
    }

    [Fact]
    public async Task ListSessions_ReturnsByUpdatedAtDescending()
    {
        var s1 = MakeSession("s1");
        s1.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var s2 = MakeSession("s2");
        s2.UpdatedAt = DateTimeOffset.UtcNow;

        await _sut.CreateSessionAsync(s1);
        await _sut.CreateSessionAsync(s2);

        var list = await _sut.ListSessionsAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("s2", list[0].Id);
        Assert.Equal("s1", list[1].Id);
    }

    [Fact]
    public async Task ListSessions_FiltersByProjectPath()
    {
        await _sut.CreateSessionAsync(MakeSession("s1", "/project-a"));
        await _sut.CreateSessionAsync(MakeSession("s2", "/project-b"));

        var list = await _sut.ListSessionsAsync(projectPath: "/project-a");

        Assert.Single(list);
        Assert.Equal("s1", list[0].Id);
    }

    [Fact]
    public async Task ListSessions_RespectsLimitAndOffset()
    {
        for (var i = 0; i < 5; i++)
        {
            var s = MakeSession($"s{i}");
            s.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(i);
            await _sut.CreateSessionAsync(s);
        }

        var page = await _sut.ListSessionsAsync(limit: 2, offset: 1);

        Assert.Equal(2, page.Count);
    }

    [Fact]
    public async Task CountAsync_ReturnsTotal()
    {
        await _sut.CreateSessionAsync(MakeSession("s1"));
        await _sut.CreateSessionAsync(MakeSession("s2"));

        var count = await _sut.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CountAsync_FiltersByProject()
    {
        await _sut.CreateSessionAsync(MakeSession("s1", "/a"));
        await _sut.CreateSessionAsync(MakeSession("s2", "/b"));
        await _sut.CreateSessionAsync(MakeSession("s3", "/a"));

        var count = await _sut.CountAsync("/a");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Count_Property_MatchesStoredSessions()
    {
        Assert.Equal(0, _sut.Count);

        await _sut.CreateSessionAsync(MakeSession("s1"));
        Assert.Equal(1, _sut.Count);

        await _sut.DeleteSessionAsync("s1");
        Assert.Equal(0, _sut.Count);
    }
}
