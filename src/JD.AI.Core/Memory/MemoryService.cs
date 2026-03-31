using System.Diagnostics;
using JD.AI.Core.Config;
using JD.AI.Core.Tracing;

namespace JD.AI.Core.Memory;

/// <summary>
/// Provides access to the JD.AI per-project memory directory at <c>~/.jdai/memory/{projectId}/</c>.
/// Each project has a <c>MEMORY.md</c> file for long-term context and a <c>memory/YYYY/MM/</c>
/// subtree for daily turn logs.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// The root memory directory (<c>~/.jdai/memory/</c>).
    /// </summary>
    string MemoryRoot { get; }

    /// <summary>
    /// Reads the long-term memory file for a project.
    /// Returns an empty string if the file does not exist.
    /// </summary>
    Task<string> GetMemoryContentAsync(string projectId, CancellationToken ct = default);

    /// <summary>
    /// Appends a single entry to the daily log for the current UTC date.
    /// Entries are written to <c>memory/{projectId}/memory/YYYY/MM/yyyy-MM-dd.md</c>.
    /// </summary>
    Task AppendToDailyLogAsync(string projectId, string entry, CancellationToken ct = default);

    /// <summary>
    /// Reads the daily log for a specific date.
    /// Returns <c>null</c> if the file does not exist.
    /// </summary>
    Task<string?> GetDailyLogAsync(string projectId, DateTimeOffset logDate, CancellationToken ct = default);

    /// <summary>
    /// Updates the long-term <c>MEMORY.md</c> file for a project.
    /// </summary>
    Task WriteMemoryContentAsync(string projectId, string content, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class MemoryService : IMemoryService
{
    private readonly string _root;

    public MemoryService()
    {
        _root = DataDirectories.MemoryRoot;
    }

    public string MemoryRoot => _root;

    public async Task<string> GetMemoryContentAsync(string projectId, CancellationToken ct = default)
    {
        var path = Path.Combine(_root, projectId, "MEMORY.md");
        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, ct).ConfigureAwait(false)
            : "";
    }

    public async Task WriteMemoryContentAsync(string projectId, string content, CancellationToken ct = default)
    {
        var dir = Path.Combine(_root, projectId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "MEMORY.md");
        try
        {
            await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugLogger.Log(DebugCategory.Memory,
                "Failed to write MEMORY.md for project {0}: {1}", projectId, ex.Message);
        }
    }

    public async Task AppendToDailyLogAsync(string projectId, string entry, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var dir = Path.Combine(_root, projectId, "memory", now.ToString("yyyy/MM"));
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            DebugLogger.Log(DebugCategory.Memory,
                "Failed to create memory directory {0}: {1}", dir, ex.Message);
            return;
        }

        var file = Path.Combine(dir, $"{now:yyyy-MM-dd}.md");
        var line = $"{now:O} | {entry}";

        try
        {
            await File.AppendAllTextAsync(file, line + Environment.NewLine, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugLogger.Log(DebugCategory.Memory,
                "Failed to append to daily log {0}: {1}", file, ex.Message);
        }
    }

    public async Task<string?> GetDailyLogAsync(string projectId, DateTimeOffset logDate, CancellationToken ct = default)
    {
        var path = Path.Combine(_root, projectId, "memory", logDate.ToString("yyyy/MM"), $"{logDate:yyyy-MM-dd}.md");
        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, ct).ConfigureAwait(false)
            : null;
    }
}
