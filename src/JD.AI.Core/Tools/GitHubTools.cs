using System.ComponentModel;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Native GitHub tools for issue, PR, and repository workflows.
/// Uses the gh CLI for authentication and API access.
/// </summary>
[ToolPlugin("github")]
public sealed class GitHubTools
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // ── Issues ──────────────────────────────────────────────

    [KernelFunction("github_list_issues")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List issues in a GitHub repository. Returns issue number, title, state, labels, and assignees.")]
    public static async Task<string> ListIssuesAsync(
        [Description("Repository in owner/repo format (e.g. 'JerrettDavis/JD.AI'). Omit to use current repo.")] string? repo = null,
        [Description("Filter by state: open, closed, all (default: open)")] string state = "open",
        [Description("Filter by labels (comma-separated)")] string? labels = null,
        [Description("Maximum number of results (default: 30)")] int limit = 30)
    {
        var args = new StringBuilder("issue list");
        args.Append($" --state {state}");
        args.Append($" --limit {limit}");
        if (!string.IsNullOrEmpty(labels)) args.Append($" --label \"{labels}\"");
        if (!string.IsNullOrEmpty(repo)) args.Append($" --repo {repo}");
        args.Append(" --json number,title,state,labels,assignees,createdAt,updatedAt");

        return await RunGhAsync(args.ToString());
    }

    [KernelFunction("github_get_issue")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get details of a specific GitHub issue including body, comments, and metadata.")]
    public static async Task<string> GetIssueAsync(
        [Description("Issue number")] int number,
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Include comments (default: false)")] bool includeComments = false)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        var result = await RunGhAsync(
            $"issue view {number}{repoArg} --json number,title,body,state,labels,assignees,comments,createdAt,updatedAt,milestone,author");

        if (includeComments)
        {
            var comments = await RunGhAsync(
                $"issue view {number}{repoArg} --json comments --jq '.comments[] | \"---\\n**\" + .author.login + \"** (\" + .createdAt + \"):\\n\" + .body'");
            if (!string.IsNullOrEmpty(comments) && !comments.StartsWith("Error", StringComparison.Ordinal))
                result += "\n\n## Comments\n" + comments;
        }

        return result;
    }

    [KernelFunction("github_create_issue")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Create a new GitHub issue with title, body, and optional labels/assignees.")]
    public static async Task<string> CreateIssueAsync(
        [Description("Issue title")] string title,
        [Description("Issue body (markdown)")] string body,
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Labels (comma-separated)")] string? labels = null,
        [Description("Assignees (comma-separated GitHub usernames)")] string? assignees = null)
    {
        var args = new StringBuilder($"issue create --title \"{EscapeQuotes(title)}\" --body \"{EscapeQuotes(body)}\"");
        if (!string.IsNullOrEmpty(labels)) args.Append($" --label \"{labels}\"");
        if (!string.IsNullOrEmpty(assignees)) args.Append($" --assignee \"{assignees}\"");
        if (!string.IsNullOrEmpty(repo)) args.Append($" --repo {repo}");

        return await RunGhAsync(args.ToString());
    }

    [KernelFunction("github_close_issue")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Close a GitHub issue with an optional comment.")]
    public static async Task<string> CloseIssueAsync(
        [Description("Issue number")] int number,
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Optional closing comment")] string? comment = null)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        if (!string.IsNullOrEmpty(comment))
            await RunGhAsync($"issue comment {number}{repoArg} --body \"{EscapeQuotes(comment)}\"");

        return await RunGhAsync($"issue close {number}{repoArg}");
    }

    // ── Pull Requests ───────────────────────────────────────

    [KernelFunction("github_list_prs")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List pull requests in a GitHub repository.")]
    public static async Task<string> ListPullRequestsAsync(
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Filter by state: open, closed, merged, all (default: open)")] string state = "open",
        [Description("Maximum number of results (default: 30)")] int limit = 30)
    {
        var args = new StringBuilder("pr list");
        args.Append($" --state {state}");
        args.Append($" --limit {limit}");
        if (!string.IsNullOrEmpty(repo)) args.Append($" --repo {repo}");
        args.Append(" --json number,title,state,author,baseRefName,headRefName,labels,reviewDecision,createdAt,updatedAt");

        return await RunGhAsync(args.ToString());
    }

    [KernelFunction("github_get_pr")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get details of a specific pull request including diff stats, review status, and checks.")]
    public static async Task<string> GetPullRequestAsync(
        [Description("Pull request number")] int number,
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Include diff (default: false — can be large)")] bool includeDiff = false)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";

        var sb = new StringBuilder();
        var detail = await RunGhAsync(
            $"pr view {number}{repoArg} --json number,title,body,state,author,baseRefName,headRefName,additions,deletions,changedFiles,labels,reviewDecision,reviews,statusCheckRollup,createdAt,updatedAt,mergedAt,mergedBy");
        sb.Append(detail);

        if (includeDiff)
        {
            var diff = await RunGhAsync($"pr diff {number}{repoArg} --name-only");
            if (!string.IsNullOrEmpty(diff) && !diff.StartsWith("Error", StringComparison.Ordinal))
                sb.Append("\n\n## Changed Files\n").Append(diff);
        }

        return sb.ToString();
    }

    [KernelFunction("github_create_pr")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Create a new pull request from the current branch.")]
    public static async Task<string> CreatePullRequestAsync(
        [Description("PR title")] string title,
        [Description("PR body (markdown)")] string body,
        [Description("Base branch to merge into (default: main)")] string baseBranch = "main",
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Labels (comma-separated)")] string? labels = null,
        [Description("Mark as draft (default: false)")] bool draft = false)
    {
        var args = new StringBuilder($"pr create --title \"{EscapeQuotes(title)}\" --body \"{EscapeQuotes(body)}\" --base {baseBranch}");
        if (!string.IsNullOrEmpty(labels)) args.Append($" --label \"{labels}\"");
        if (draft) args.Append(" --draft");
        if (!string.IsNullOrEmpty(repo)) args.Append($" --repo {repo}");

        return await RunGhAsync(args.ToString());
    }

    [KernelFunction("github_pr_checks")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get CI check status for a pull request.")]
    public static async Task<string> GetPrChecksAsync(
        [Description("Pull request number")] int number,
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        return await RunGhAsync($"pr checks {number}{repoArg}");
    }

    [KernelFunction("github_merge_pr")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Merge a pull request. Requires all checks to pass and review approval.")]
    public static async Task<string> MergePullRequestAsync(
        [Description("Pull request number")] int number,
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Merge method: merge, squash, rebase (default: squash)")] string method = "squash",
        [Description("Delete branch after merge (default: true)")] bool deleteBranch = true)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        var deleteArg = deleteBranch ? " --delete-branch" : "";
        return await RunGhAsync($"pr merge {number}{repoArg} --{method}{deleteArg} --auto");
    }

    [KernelFunction("github_pr_review")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Submit a review on a pull request (approve, request changes, or comment).")]
    public static async Task<string> ReviewPullRequestAsync(
        [Description("Pull request number")] int number,
        [Description("Review action: approve, request-changes, comment")] string action,
        [Description("Review body/comment")] string body,
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        return await RunGhAsync($"pr review {number}{repoArg} --{action} --body \"{EscapeQuotes(body)}\"");
    }

    // ── Repository ──────────────────────────────────────────

    [KernelFunction("github_repo_info")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get information about a GitHub repository (description, stars, language, topics).")]
    public static async Task<string> GetRepoInfoAsync(
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        return await RunGhAsync(
            $"repo view{repoArg} --json name,description,defaultBranchRef,stargazerCount,forkCount,primaryLanguage,repositoryTopics,isPrivate,url,createdAt,pushedAt");
    }

    [KernelFunction("github_search_issues")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Search GitHub issues and PRs across repositories using query syntax.")]
    public static async Task<string> SearchIssuesAsync(
        [Description("Search query (GitHub search syntax, e.g. 'bug label:critical is:open')")] string query,
        [Description("Repository in owner/repo format. Omit to search all accessible repos.")] string? repo = null,
        [Description("Maximum results (default: 20)")] int limit = 20)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        return await RunGhAsync(
            $"search issues \"{EscapeQuotes(query)}\"{repoArg} --limit {limit} --json repository,number,title,state,author,labels,createdAt,updatedAt");
    }

    // ── Workflows / Actions ─────────────────────────────────

    [KernelFunction("github_list_runs")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List recent GitHub Actions workflow runs for a repository.")]
    public static async Task<string> ListWorkflowRunsAsync(
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Filter by branch name")] string? branch = null,
        [Description("Filter by workflow name or filename")] string? workflow = null,
        [Description("Maximum results (default: 10)")] int limit = 10)
    {
        var args = new StringBuilder("run list");
        args.Append($" --limit {limit}");
        if (!string.IsNullOrEmpty(branch)) args.Append($" --branch {branch}");
        if (!string.IsNullOrEmpty(workflow)) args.Append($" --workflow \"{workflow}\"");
        if (!string.IsNullOrEmpty(repo)) args.Append($" --repo {repo}");
        args.Append(" --json databaseId,displayTitle,status,conclusion,headBranch,event,createdAt,updatedAt,url");

        return await RunGhAsync(args.ToString());
    }

    [KernelFunction("github_run_details")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get details and job logs for a specific GitHub Actions run.")]
    public static async Task<string> GetRunDetailsAsync(
        [Description("Workflow run ID")] long runId,
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Show failed job logs (default: true)")] bool showLogs = true)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        var sb = new StringBuilder();

        var detail = await RunGhAsync($"run view {runId}{repoArg} --json databaseId,displayTitle,status,conclusion,jobs,headBranch,event,createdAt,updatedAt,url");
        sb.Append(detail);

        if (showLogs)
        {
            var logs = await RunGhAsync($"run view {runId}{repoArg} --log-failed");
            if (!string.IsNullOrEmpty(logs) && !logs.StartsWith("Error", StringComparison.Ordinal))
            {
                // Truncate logs to avoid overwhelming the context
                const int maxLogLength = 8000;
                if (logs.Length > maxLogLength)
                    logs = logs[..maxLogLength] + $"\n\n... (truncated, {logs.Length - maxLogLength} chars omitted)";
                sb.Append("\n\n## Failed Job Logs\n```\n").Append(logs).Append("\n```");
            }
        }

        return sb.ToString();
    }

    // ── Releases ────────────────────────────────────────────

    [KernelFunction("github_list_releases")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List releases for a GitHub repository.")]
    public static async Task<string> ListReleasesAsync(
        [Description("Repository in owner/repo format. Omit to use current repo.")] string? repo = null,
        [Description("Maximum results (default: 10)")] int limit = 10)
    {
        var repoArg = !string.IsNullOrEmpty(repo) ? $" --repo {repo}" : "";
        return await RunGhAsync($"release list{repoArg} --limit {limit}");
    }

    // ── Auth Status ─────────────────────────────────────────

    [KernelFunction("github_auth_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Check GitHub CLI authentication status and available scopes.")]
    public static async Task<string> GetAuthStatusAsync()
    {
        return await RunGhAsync("auth status");
    }

    // ── Internal helpers ────────────────────────────────────

    private static async Task<string> RunGhAsync(string arguments)
    {
        try
        {
            var result = await ProcessExecutor.RunAsync(
                "gh", arguments, timeout: DefaultTimeout);

            if (!result.Success)
            {
                return !string.IsNullOrEmpty(result.StandardError)
                    ? $"Error (exit {result.ExitCode}): {result.StandardError}"
                    : $"Error (exit {result.ExitCode}): {result.StandardOutput}";
            }

            return string.IsNullOrWhiteSpace(result.StandardOutput) ? "(no output)" : result.StandardOutput;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string EscapeQuotes(string input)
        => input.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
