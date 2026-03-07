using System.Collections.Concurrent;

namespace JD.AI.Core.Sessions;

/// <summary>
/// In-memory session repository for testing and lightweight deployments.
/// Not suitable for production multi-node use.
/// </summary>
public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();

    /// <summary>Number of stored sessions.</summary>
    public int Count => _sessions.Count;

    public Task CreateSessionAsync(SessionRecord session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task UpdateSessionAsync(SessionRecord session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!_sessions.ContainsKey(session.Id))
            throw new KeyNotFoundException($"Session '{session.Id}' not found");

        session.UpdatedAt = DateTimeOffset.UtcNow;
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<SessionRecord?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<SessionRecord>> ListSessionsAsync(
        string? projectPath = null, int limit = 50, int offset = 0,
        CancellationToken ct = default)
    {
        var query = _sessions.Values.AsEnumerable();

        if (projectPath is not null)
            query = query.Where(s =>
                string.Equals(s.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));

        var result = query
            .OrderByDescending(s => s.UpdatedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<SessionRecord>>(result);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task<long> CountAsync(string? projectPath = null, CancellationToken ct = default)
    {
        var count = projectPath is null
            ? _sessions.Count
            : _sessions.Values.Count(s =>
                string.Equals(s.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult((long)count);
    }
}
