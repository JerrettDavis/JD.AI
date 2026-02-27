using System.Diagnostics;

namespace JD.AI.Tui.Agent.Checkpointing;

/// <summary>
/// Checkpoint strategy using git commits with a special prefix.
/// Creates checkpoint commits that can be reverted via git reset.
/// </summary>
public sealed class CommitCheckpointStrategy : ICheckpointStrategy
{
    private const string CommitPrefix = "[jdai-checkpoint]";
    private readonly string _workingDir;

    public CommitCheckpointStrategy(string? workingDir = null)
    {
        _workingDir = workingDir ?? Directory.GetCurrentDirectory();
    }

    public async Task<string?> CreateAsync(string label, CancellationToken ct = default)
    {
        var status = await RunGitAsync("status --porcelain", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(status))
            return null;

        await RunGitAsync("add -A", ct).ConfigureAwait(false);
        var message = $"{CommitPrefix} {label}";
        await RunGitAsync($"commit -m \"{message}\" --no-verify", ct).ConfigureAwait(false);

        var sha = (await RunGitAsync("rev-parse --short HEAD", ct).ConfigureAwait(false)).Trim();
        return sha;
    }

    public async Task<IReadOnlyList<CheckpointInfo>> ListAsync(CancellationToken ct = default)
    {
        var output = await RunGitAsync(
            $"log --oneline --grep=\"{CommitPrefix}\" -20", ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(output))
            return [];

        var results = new List<CheckpointInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(' ', 2);
            if (parts.Length < 2) continue;
            results.Add(new CheckpointInfo(parts[0], parts[1].Trim(), DateTime.UtcNow));
        }

        return results;
    }

    public async Task<bool> RestoreAsync(string checkpointId, CancellationToken ct = default)
    {
        var result = await RunGitAsync($"reset --hard {checkpointId}", ct).ConfigureAwait(false);
        return !result.Contains("fatal", StringComparison.OrdinalIgnoreCase);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var checkpoints = await ListAsync(ct).ConfigureAwait(false);
        if (checkpoints.Count == 0) return;
        var lastCp = checkpoints[^1].Id;
        await RunGitAsync($"reset --soft {lastCp}~1", ct).ConfigureAwait(false);
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
