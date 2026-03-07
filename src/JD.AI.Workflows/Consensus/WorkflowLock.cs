using System.Collections.Concurrent;

namespace JD.AI.Workflows.Consensus;

/// <summary>
/// Provides advisory workflow-level locking for exclusive edit access.
/// Locks are held by a specific user and expire after a configurable duration
/// to prevent stale locks from blocking other users indefinitely.
/// </summary>
public sealed class WorkflowLock
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _defaultExpiry;

    /// <summary>
    /// Creates a new workflow lock manager.
    /// </summary>
    /// <param name="defaultExpiry">
    /// Default lock duration before auto-expiry. Defaults to 30 minutes.
    /// </param>
    public WorkflowLock(TimeSpan? defaultExpiry = null)
    {
        _defaultExpiry = defaultExpiry ?? TimeSpan.FromMinutes(30);
    }

    /// <summary>Number of active (non-expired) locks.</summary>
    public int ActiveLockCount => _locks.Values.Count(l => !l.IsExpired);

    /// <summary>
    /// Attempts to acquire an exclusive lock on a workflow.
    /// Returns true if the lock was acquired, false if held by another user.
    /// </summary>
    public bool TryAcquire(string workflowName, string owner, TimeSpan? expiry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var entry = new LockEntry
        {
            WorkflowName = workflowName,
            Owner = owner,
            AcquiredAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow + (expiry ?? _defaultExpiry),
        };

        return _locks.AddOrUpdate(
            workflowName,
            entry,
            (_, existing) =>
            {
                // Allow re-acquisition if expired or same owner
                if (existing.IsExpired ||
                    string.Equals(existing.Owner, owner, StringComparison.OrdinalIgnoreCase))
                    return entry;

                return existing; // Keep existing lock
            }) == entry; // True if our entry was used
    }

    /// <summary>Releases a lock. Only the lock owner can release it.</summary>
    public bool Release(string workflowName, string owner)
    {
        if (!_locks.TryGetValue(workflowName, out var existing))
            return false;

        if (!string.Equals(existing.Owner, owner, StringComparison.OrdinalIgnoreCase))
            return false;

        return _locks.TryRemove(workflowName, out _);
    }

    /// <summary>Checks if a workflow is currently locked and by whom.</summary>
    public LockEntry? GetLock(string workflowName)
    {
        if (!_locks.TryGetValue(workflowName, out var entry))
            return null;

        if (entry.IsExpired)
        {
            _locks.TryRemove(workflowName, out _);
            return null;
        }

        return entry;
    }

    /// <summary>Checks if the given user holds the lock (or the workflow is unlocked).</summary>
    public bool IsLockedBy(string workflowName, string owner)
    {
        var entry = GetLock(workflowName);
        return entry is not null &&
               string.Equals(entry.Owner, owner, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Extends the expiry of an existing lock. Only the owner can extend.</summary>
    public bool Extend(string workflowName, string owner, TimeSpan extension)
    {
        if (!_locks.TryGetValue(workflowName, out var existing))
            return false;

        if (!string.Equals(existing.Owner, owner, StringComparison.OrdinalIgnoreCase))
            return false;

        if (existing.IsExpired)
            return false;

        existing.ExpiresAt = DateTimeOffset.UtcNow + extension;
        return true;
    }

    /// <summary>Force-releases a lock regardless of owner (admin operation).</summary>
    public bool ForceRelease(string workflowName) =>
        _locks.TryRemove(workflowName, out _);

    /// <summary>Lists all active (non-expired) locks.</summary>
    public IReadOnlyList<LockEntry> ListActiveLocks() =>
        _locks.Values.Where(l => !l.IsExpired).ToList();
}

/// <summary>An advisory lock held on a workflow.</summary>
public sealed class LockEntry
{
    public string WorkflowName { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public DateTimeOffset AcquiredAt { get; init; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
