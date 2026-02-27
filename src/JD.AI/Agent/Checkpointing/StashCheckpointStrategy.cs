using System.Diagnostics;

namespace JD.AI.Tui.Agent.Checkpointing;

/// <summary>
/// Checkpoint strategy using git stash. Lightweight, doesn't pollute branch history.
/// Stashes are named "jdai-cp-{label}" and can be listed/restored.
/// </summary>
public sealed class StashCheckpointStrategy : ICheckpointStrategy
{
    private readonly string _workingDir;

    public StashCheckpointStrategy(string? workingDir = null)
    {
        _workingDir = workingDir ?? Directory.GetCurrentDirectory();
    }

    public async Task<string?> CreateAsync(string label, CancellationToken ct = default)
    {
        var status = await RunGitAsync("status --porcelain", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(status))
            return null; // Nothing to checkpoint

        var stashName = $"jdai-cp-{label}";
        var result = await RunGitAsync($"stash push -m \"{stashName}\" --include-untracked", ct).ConfigureAwait(false);

        if (result.Contains("Saved working directory", StringComparison.OrdinalIgnoreCase))
        {
            // Pop immediately so working tree stays intact — we just want the stash ref
            await RunGitAsync("stash pop --quiet", ct).ConfigureAwait(false);
            return stashName;
        }

        return null;
    }

    public async Task<IReadOnlyList<CheckpointInfo>> ListAsync(CancellationToken ct = default)
    {
        var output = await RunGitAsync("stash list", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(output))
            return [];

        var results = new List<CheckpointInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("jdai-cp-", StringComparison.Ordinal))
                continue;

            var stashRef = line.Split(':')[0].Trim();
            var msgStart = line.IndexOf("jdai-cp-", StringComparison.Ordinal);
            var label = msgStart >= 0 ? line[msgStart..].Trim() : stashRef;

            results.Add(new CheckpointInfo(stashRef, label, DateTime.UtcNow));
        }

        return results;
    }

    public async Task<bool> RestoreAsync(string checkpointId, CancellationToken ct = default)
    {
        var result = await RunGitAsync($"stash apply {checkpointId}", ct).ConfigureAwait(false);
        return !result.Contains("error", StringComparison.OrdinalIgnoreCase);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var stashes = await ListAsync(ct).ConfigureAwait(false);
        for (var i = stashes.Count - 1; i >= 0; i--)
        {
            await RunGitAsync($"stash drop {stashes[i].Id}", ct).ConfigureAwait(false);
        }
    }

    private async Task<string> RunGitAsync(string arguments, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return output;
    }
}
