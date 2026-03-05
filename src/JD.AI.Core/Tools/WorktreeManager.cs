using System.Diagnostics;

namespace JD.AI.Core.Tools;

/// <summary>
/// Manages git worktree lifecycle for isolated agent sessions.
/// Created via <c>--worktree</c> / <c>-w</c> CLI flag.
/// </summary>
public sealed class WorktreeManager : IAsyncDisposable
{
    private readonly string _repoRoot;
    private string? _worktreePath;
    private string? _branchName;
    private bool _disposed;

    public WorktreeManager(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    /// <summary>Path to the created worktree directory, or null if not yet created.</summary>
    public string? WorktreePath => _worktreePath;

    /// <summary>Branch name created for the worktree.</summary>
    public string? BranchName => _branchName;

    /// <summary>
    /// Creates a new git worktree with a unique branch name.
    /// Returns the worktree directory path.
    /// </summary>
    public async Task<string> CreateAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var id = Guid.NewGuid().ToString("N")[..8];
        _branchName = $"jdai/worktree-{id}";
        _worktreePath = Path.Combine(_repoRoot, ".git", "worktrees-jdai", id);

        // Ensure parent directory exists
        var parentDir = Path.GetDirectoryName(_worktreePath);
        if (!string.IsNullOrEmpty(parentDir))
            Directory.CreateDirectory(parentDir);

        var result = await RunGitAsync(
            $"worktree add \"{_worktreePath}\" -b \"{_branchName}\"",
            _repoRoot, ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to create git worktree: {result.Error}");
        }

        return _worktreePath;
    }

    /// <summary>Removes the worktree and deletes the branch.</summary>
    public async Task RemoveAsync(CancellationToken ct = default)
    {
        if (_worktreePath is null) return;

        await RunGitAsync(
            $"worktree remove \"{_worktreePath}\" --force",
            _repoRoot, ct).ConfigureAwait(false);

        if (_branchName is not null)
        {
            await RunGitAsync(
                $"branch -D \"{_branchName}\"",
                _repoRoot, ct).ConfigureAwait(false);
        }

        _worktreePath = null;
        _branchName = null;
    }

    /// <summary>
    /// Merges the worktree branch into the current branch and removes the worktree.
    /// </summary>
    public async Task MergeAndRemoveAsync(string targetBranch = "HEAD", CancellationToken ct = default)
    {
        if (_branchName is null) return;

        await RunGitAsync(
            $"merge \"{_branchName}\" --no-ff -m \"Merge jdai worktree session\"",
            _repoRoot, ct).ConfigureAwait(false);

        await RemoveAsync(ct).ConfigureAwait(false);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunGitAsync(
        string arguments, string workingDirectory, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return (process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Clean up worktree on dispose if still active
        if (_worktreePath is not null)
        {
            try
            {
                await RemoveAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
