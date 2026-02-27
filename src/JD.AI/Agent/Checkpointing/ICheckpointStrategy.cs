namespace JD.AI.Tui.Agent.Checkpointing;

/// <summary>
/// Abstraction for creating and managing project state checkpoints
/// before file-mutating tool invocations.
/// </summary>
public interface ICheckpointStrategy
{
    /// <summary>Create a checkpoint with the given label. Returns the checkpoint ID.</summary>
    Task<string?> CreateAsync(string label, CancellationToken ct = default);

    /// <summary>List all checkpoints for the current project.</summary>
    Task<IReadOnlyList<CheckpointInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>Restore to a specific checkpoint.</summary>
    Task<bool> RestoreAsync(string checkpointId, CancellationToken ct = default);

    /// <summary>Remove all checkpoints.</summary>
    Task ClearAsync(CancellationToken ct = default);
}

/// <summary>Metadata about a single checkpoint.</summary>
public sealed record CheckpointInfo(string Id, string Label, DateTime CreatedAt);
