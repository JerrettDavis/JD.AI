namespace JD.AI.Core.Sessions;

/// <summary>
/// Abstraction for session persistence. The existing <see cref="SessionStore"/>
/// (SQLite) implements this interface. Distributed backends (PostgreSQL, Redis)
/// can provide alternative implementations for multi-node deployments.
/// </summary>
public interface ISessionRepository
{
    /// <summary>Creates a new session record.</summary>
    Task CreateSessionAsync(SessionRecord session, CancellationToken ct = default);

    /// <summary>Updates an existing session record.</summary>
    Task UpdateSessionAsync(SessionRecord session, CancellationToken ct = default);

    /// <summary>Gets a session by ID.</summary>
    Task<SessionRecord?> GetSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Lists sessions with optional filtering.</summary>
    Task<IReadOnlyList<SessionRecord>> ListSessionsAsync(
        string? projectPath = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default);

    /// <summary>Deletes a session and all its related data.</summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Returns the total number of sessions.</summary>
    Task<long> CountAsync(string? projectPath = null, CancellationToken ct = default);
}

/// <summary>
/// Normalized session record for cross-backend portability.
/// </summary>
public sealed class SessionRecord
{
    public required string Id { get; init; }
    public string? Name { get; set; }
    public required string ProjectPath { get; init; }
    public string? ProjectHash { get; init; }
    public string? ModelId { get; set; }
    public string? ProviderName { get; set; }
    public string? TenantId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long TotalTokens { get; set; }
    public int MessageCount { get; set; }
    public bool IsActive { get; set; } = true;
}
