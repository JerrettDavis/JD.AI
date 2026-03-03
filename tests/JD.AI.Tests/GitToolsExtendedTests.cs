using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class GitToolsExtendedTests
{
    [Fact]
    public async Task GitBranch_ListBranches_ReturnsOutput()
    {
        // List branches in the current repo (should work if we're in a git repo)
        var result = await GitTools.GitBranchAsync();

        // Should contain at least one branch or an error
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public async Task GitStash_List_ReturnsOutput()
    {
        var result = await GitTools.GitStashAsync(action: "list");

        // Should return something (even if empty stash list returns "(no output)")
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public async Task GitPush_InvalidRemote_ReturnsError()
    {
        // Push to a nonexistent remote should produce an error
        var result = await GitTools.GitPushAsync(remote: "nonexistent-remote-xyz");

        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GitCheckout_InvalidBranch_ReturnsError()
    {
        var result = await GitTools.GitCheckoutAsync("nonexistent-branch-xyz-12345");

        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }
}
