using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// Git tools for the AI agent.
/// </summary>
public sealed class GitTools
{
    [KernelFunction("git_status")]
    [Description("Show the working tree status (modified, staged, untracked files).")]
    public static async Task<string> GitStatusAsync(
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        return await RunGitAsync("status --porcelain", path).ConfigureAwait(false);
    }

    [KernelFunction("git_diff")]
    [Description("Show differences. Use target to compare branches (e.g. 'main').")]
    public static async Task<string> GitDiffAsync(
        [Description("Diff target (e.g. 'main', '--staged', or empty for unstaged)")] string? target = null,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        var args = string.IsNullOrWhiteSpace(target) ? "diff" : $"diff {target}";
        return await RunGitAsync(args, path).ConfigureAwait(false);
    }

    [KernelFunction("git_log")]
    [Description("Show recent commit history.")]
    public static async Task<string> GitLogAsync(
        [Description("Number of commits to show (default 10)")] int count = 10,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        return await RunGitAsync(
            $"log --oneline --no-decorate -n {count}", path).ConfigureAwait(false);
    }

    [KernelFunction("git_commit")]
    [Description("Stage all changes and commit with the given message.")]
    public static async Task<string> GitCommitAsync(
        [Description("Commit message")] string message,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        var escaped = message.Replace("\"", "\\\"");
        await RunGitAsync("add -A", path).ConfigureAwait(false);
        return await RunGitAsync($"commit -m \"{escaped}\"", path).ConfigureAwait(false);
    }

    private static async Task<string> RunGitAsync(string args, string? path)
    {
        var workDir = path ?? Directory.GetCurrentDirectory();
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"--no-pager {args}",
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var sb = new StringBuilder();

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            sb.Append(stdout);
        }

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            sb.AppendLine($"Error (exit {process.ExitCode}): {stderr}");
        }

        var result = sb.ToString();
        return string.IsNullOrWhiteSpace(result) ? "(no output)" : result;
    }
}
