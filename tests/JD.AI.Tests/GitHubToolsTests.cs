using FluentAssertions;
using JD.AI.Core.Tools;
using Xunit;

namespace JD.AI.Tests;

/// <summary>
/// Tests for GitHubTools — focuses on argument construction and error handling.
/// Actual gh CLI calls are integration tests (require auth).
/// </summary>
public sealed class GitHubToolsTests
{
    // ── Issues ──────────────────────────────────────────────

    [Fact]
    public async Task ListIssues_NoGhCli_ReturnsError()
    {
        // If gh is not installed or auth fails, we get an error message
        var result = await GitHubTools.ListIssuesAsync("nonexistent-owner/nonexistent-repo");

        // Either returns data (if gh is installed) or an error
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetIssue_InvalidRepo_ReturnsError()
    {
        var result = await GitHubTools.GetIssueAsync(999999, "nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        // Should contain "Error" since repo doesn't exist
        result.Should().Contain("Error");
    }

    [Fact]
    public async Task CreateIssue_InvalidRepo_ReturnsError()
    {
        var result = await GitHubTools.CreateIssueAsync(
            "Test Issue", "Test body", "nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Error");
    }

    [Fact]
    public async Task CloseIssue_InvalidRepo_ReturnsError()
    {
        var result = await GitHubTools.CloseIssueAsync(999999, "nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Error");
    }

    // ── Pull Requests ───────────────────────────────────────

    [Fact]
    public async Task ListPrs_NoGhCli_ReturnsResult()
    {
        var result = await GitHubTools.ListPullRequestsAsync("nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetPr_InvalidRepo_ReturnsError()
    {
        var result = await GitHubTools.GetPullRequestAsync(999999, "nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Error");
    }

    [Fact]
    public async Task PrChecks_InvalidRepo_ReturnsError()
    {
        var result = await GitHubTools.GetPrChecksAsync(999999, "nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Error");
    }

    [Fact]
    public async Task MergePr_InvalidRepo_ReturnsError()
    {
        var result = await GitHubTools.MergePullRequestAsync(999999, "nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Error");
    }

    [Fact]
    public async Task ReviewPr_InvalidRepo_ReturnsError()
    {
        var result = await GitHubTools.ReviewPullRequestAsync(
            999999, "approve", "Looks good", "nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Error");
    }

    // ── Repository ──────────────────────────────────────────

    [Fact]
    public async Task RepoInfo_InvalidRepo_ReturnsError()
    {
        var result = await GitHubTools.GetRepoInfoAsync("nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Error");
    }

    [Fact]
    public async Task SearchIssues_ReturnsResult()
    {
        var result = await GitHubTools.SearchIssuesAsync("test", "nonexistent-owner/nonexistent-repo");

        // May return empty results or error — both valid
        result.Should().NotBeNullOrEmpty();
    }

    // ── Workflows ───────────────────────────────────────────

    [Fact]
    public async Task ListRuns_InvalidRepo_ReturnsResult()
    {
        var result = await GitHubTools.ListWorkflowRunsAsync("nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunDetails_InvalidId_ReturnsError()
    {
        var result = await GitHubTools.GetRunDetailsAsync(0, "nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Error");
    }

    // ── Releases ────────────────────────────────────────────

    [Fact]
    public async Task ListReleases_InvalidRepo_ReturnsResult()
    {
        var result = await GitHubTools.ListReleasesAsync("nonexistent-owner/nonexistent-repo");

        result.Should().NotBeNullOrEmpty();
    }

    // ── Auth ────────────────────────────────────────────────

    [Fact]
    public async Task AuthStatus_ReturnsResult()
    {
        var result = await GitHubTools.GetAuthStatusAsync();

        // Returns auth info or error if not authenticated
        result.Should().NotBeNullOrEmpty();
    }

    // ── Integration Tests (real repo — only when gh is authenticated) ───

    [Fact]
    public async Task ListIssues_RealRepo_ReturnsJson()
    {
        var result = await GitHubTools.ListIssuesAsync("JerrettDavis/JD.AI");

        // If gh is authenticated, we should get JSON array
        if (!result.StartsWith("Error", StringComparison.Ordinal))
        {
            result.Should().StartWith("[");
        }
    }

    [Fact]
    public async Task ListPrs_RealRepo_ReturnsJson()
    {
        var result = await GitHubTools.ListPullRequestsAsync("JerrettDavis/JD.AI");

        if (!result.StartsWith("Error", StringComparison.Ordinal))
        {
            result.Should().StartWith("[");
        }
    }

    [Fact]
    public async Task RepoInfo_RealRepo_ReturnsInfo()
    {
        var result = await GitHubTools.GetRepoInfoAsync("JerrettDavis/JD.AI");

        if (!result.StartsWith("Error", StringComparison.Ordinal))
        {
            result.Should().Contain("JD.AI");
        }
    }
}
