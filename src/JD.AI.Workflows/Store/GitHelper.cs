using JD.AI.Core.Infrastructure;

namespace JD.AI.Workflows.Store;

/// <summary>
/// Runs git CLI commands via <see cref="ProcessExecutor"/>.
/// This avoids the heavy native dependencies of LibGit2Sharp.
/// </summary>
internal static class GitHelper
{
    /// <summary>Runs a git command in the given working directory and returns (exitCode, stdout, stderr).</summary>
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string workingDirectory,
        string arguments,
        CancellationToken ct = default)
    {
        var result = await ProcessExecutor.RunAsync(
            "git", arguments, workingDirectory: workingDirectory, cancellationToken: ct)
            .ConfigureAwait(false);

        return (result.ExitCode, result.StandardOutput, result.StandardError);
    }

    /// <summary>Throws if git is not available on the PATH.</summary>
    public static async Task EnsureGitAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await ProcessExecutor.RunAsync(
                "git", "--version", timeout: TimeSpan.FromSeconds(5), cancellationToken: ct)
                .ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException("git not found on PATH.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "git is not available on PATH. Install git to use the GitWorkflowStore.", ex);
        }
    }
}
