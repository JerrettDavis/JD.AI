using System.Diagnostics;

namespace JD.AI.Workflows.Store;

/// <summary>
/// Runs git CLI commands via <see cref="Process.Start"/>.
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
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Use ArgumentList for safe argument passing (no shell quoting issues)
        foreach (var arg in SplitArguments(arguments))
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        return (process.ExitCode, output.Trim(), error.Trim());
    }

    /// <summary>Throws if git is not available on the PATH.</summary>
    public static async Task EnsureGitAvailableAsync(CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--version");

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("git not found on PATH.");
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidOperationException("git not found on PATH.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "git is not available on PATH. Install git to use the GitWorkflowStore.", ex);
        }
    }

    /// <summary>Splits a space-delimited argument string, respecting quoted segments.</summary>
    internal static IEnumerable<string> SplitArguments(string arguments)
    {
        var inQuote = false;
        var current = new System.Text.StringBuilder();

        foreach (var ch in arguments)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                continue;
            }

            if (ch == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }
}
