using System.ComponentModel;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Git tools for the AI agent.
/// </summary>
[ToolPlugin("git")]
public sealed class GitTools
{
    [KernelFunction("git_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Show the working tree status (modified, staged, untracked files).")]
    public static async Task<string> GitStatusAsync(
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        return await RunGitAsync("status --porcelain", path).ConfigureAwait(false);
    }

    [KernelFunction("git_diff")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Show differences. Use target to compare branches (e.g. 'main').")]
    public static async Task<string> GitDiffAsync(
        [Description("Diff target (e.g. 'main', '--staged', or empty for unstaged)")] string? target = null,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        var args = string.IsNullOrWhiteSpace(target) ? "diff" : $"diff {target}";
        return await RunGitAsync(args, path).ConfigureAwait(false);
    }

    [KernelFunction("git_log")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Show recent commit history.")]
    public static async Task<string> GitLogAsync(
        [Description("Number of commits to show (default 10)")] int count = 10,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        return await RunGitAsync(
            $"log --oneline --no-decorate -n {count}", path).ConfigureAwait(false);
    }

    [KernelFunction("git_commit")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Stage all changes and commit with the given message.")]
    public static async Task<string> GitCommitAsync(
        [Description("Commit message")] string message,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        var escaped = message.Replace("\"", "\\\"");
        await RunGitAsync("add -A", path).ConfigureAwait(false);
        return await RunGitAsync($"commit -m \"{escaped}\"", path).ConfigureAwait(false);
    }

    [KernelFunction("git_push")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Push commits to the remote repository.")]
    public static async Task<string> GitPushAsync(
        [Description("Remote name (default 'origin')")] string remote = "origin",
        [Description("Branch name (default: current branch)")] string? branch = null,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        var args = string.IsNullOrWhiteSpace(branch) ? $"push {remote}" : $"push {remote} {branch}";
        return await RunGitAsync(args, path).ConfigureAwait(false);
    }

    [KernelFunction("git_pull")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Pull changes from the remote repository.")]
    public static async Task<string> GitPullAsync(
        [Description("Remote name (default 'origin')")] string remote = "origin",
        [Description("Branch name (default: current branch)")] string? branch = null,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        var args = string.IsNullOrWhiteSpace(branch) ? $"pull {remote}" : $"pull {remote} {branch}";
        return await RunGitAsync(args, path).ConfigureAwait(false);
    }

    [KernelFunction("git_branch")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List, create, or delete branches.")]
    public static async Task<string> GitBranchAsync(
        [Description("Branch name to create (omit to list branches)")] string? name = null,
        [Description("Delete the branch instead of creating it")] bool delete = false,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return await RunGitAsync("branch -a", path).ConfigureAwait(false);
        }

        var args = delete ? $"branch -d {name}" : $"branch {name}";
        return await RunGitAsync(args, path).ConfigureAwait(false);
    }

    [KernelFunction("git_checkout")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Switch branches or restore working tree files.")]
    public static async Task<string> GitCheckoutAsync(
        [Description("Branch name, commit SHA, or file path to checkout")] string target,
        [Description("Create a new branch (-b flag)")] bool createNew = false,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        var args = createNew ? $"checkout -b {target}" : $"checkout {target}";
        return await RunGitAsync(args, path).ConfigureAwait(false);
    }

    [KernelFunction("git_stash")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Stash or restore uncommitted changes.")]
    public static async Task<string> GitStashAsync(
        [Description("Stash action: 'push' (default), 'pop', 'list', 'drop'")] string action = "push",
        [Description("Optional message for stash push")] string? message = null,
        [Description("Repository path (defaults to cwd)")] string? path = null)
    {
        var args = action.ToUpperInvariant() switch
        {
            "POP" => "stash pop",
            "LIST" => "stash list",
            "DROP" => "stash drop",
            _ => string.IsNullOrWhiteSpace(message) ? "stash push" : $"stash push -m \"{message}\"",
        };
        return await RunGitAsync(args, path).ConfigureAwait(false);
    }

    private static async Task<string> RunGitAsync(string args, string? path)
    {
        var workDir = path ?? Directory.GetCurrentDirectory();
        var result = await ProcessExecutor.RunAsync("git", $"--no-pager {args}", workDir).ConfigureAwait(false);

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            sb.Append(result.StandardOutput);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.StandardError))
            sb.AppendLine($"Error (exit {result.ExitCode}): {result.StandardError}");

        var output = sb.ToString();
        return string.IsNullOrWhiteSpace(output) ? "(no output)" : output;
    }
}
