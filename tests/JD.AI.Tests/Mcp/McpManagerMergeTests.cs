using JD.AI.Core.Mcp;

namespace JD.AI.Tests.Mcp;

public sealed class McpManagerMergeTests
{
    [Fact]
    public void Merge_ProjectOverridesUser()
    {
        var user = new McpServerDefinition
        {
            Name = "server1", DisplayName = "User",
            Scope = McpScope.User, SourceProvider = "JD.AI",
            Command = "user-cmd",
        };
        var project = new McpServerDefinition
        {
            Name = "server1", DisplayName = "Project",
            Scope = McpScope.Project, SourceProvider = "ClaudeCode",
            Command = "project-cmd",
        };

        var merged = McpManager.Merge([user, project]);

        Assert.Single(merged);
        Assert.Equal("project-cmd", merged[0].Command);
        Assert.Equal(McpScope.Project, merged[0].Scope);
    }

    [Fact]
    public void Merge_UserOverridesBuiltIn()
    {
        var builtin = new McpServerDefinition
        {
            Name = "server1",
            Scope = McpScope.BuiltIn, SourceProvider = "JD.AI",
            Command = "builtin-cmd",
        };
        var user = new McpServerDefinition
        {
            Name = "server1",
            Scope = McpScope.User, SourceProvider = "ClaudeCode",
            Command = "user-cmd",
        };

        var merged = McpManager.Merge([builtin, user]);

        Assert.Single(merged);
        Assert.Equal("user-cmd", merged[0].Command);
        Assert.Equal(McpScope.User, merged[0].Scope);
    }

    [Fact]
    public void Merge_DistinctNamesArePreserved()
    {
        var a = new McpServerDefinition { Name = "alpha", Scope = McpScope.User };
        var b = new McpServerDefinition { Name = "beta",  Scope = McpScope.Project };
        var c = new McpServerDefinition { Name = "gamma", Scope = McpScope.BuiltIn };

        var merged = McpManager.Merge([a, b, c]);

        Assert.Equal(3, merged.Count);
    }

    [Fact]
    public void Merge_EmptyInputReturnsEmpty()
    {
        var merged = McpManager.Merge([]);
        Assert.Empty(merged);
    }

    [Fact]
    public void Merge_OrderedAlphabetically()
    {
        var c = new McpServerDefinition { Name = "charlie", Scope = McpScope.User };
        var a = new McpServerDefinition { Name = "alpha",   Scope = McpScope.User };
        var b = new McpServerDefinition { Name = "beta",    Scope = McpScope.User };

        var merged = McpManager.Merge([c, a, b]);

        Assert.Equal(["alpha", "beta", "charlie"], merged.Select(m => m.Name).ToList());
    }

    [Fact]
    public void Merge_ProjectDoesNotOverrideHigherScopeProjectFromDifferentProvider()
    {
        // Two project-scope entries for the same server: last one wins (as per merge rules)
        var p1 = new McpServerDefinition
        {
            Name = "server1", Scope = McpScope.Project,
            SourceProvider = "Provider1", Command = "cmd1",
        };
        var p2 = new McpServerDefinition
        {
            Name = "server1", Scope = McpScope.Project,
            SourceProvider = "Provider2", Command = "cmd2",
        };

        var merged = McpManager.Merge([p1, p2]);

        // p2 is processed after p1 and same scope priority → p2 wins (>= check)
        Assert.Single(merged);
        Assert.Equal("cmd2", merged[0].Command);
    }

    [Fact]
    public void GetStatus_UnknownServerReturnsDefault()
    {
        var manager = new McpManager([], null);
        var status = manager.GetStatus("nonexistent");
        Assert.Equal(McpConnectionState.Unknown, status.State);
    }

    [Fact]
    public void SetStatus_RoundTrips()
    {
        var manager = new McpManager([], null);
        var status = new McpServerStatus
        {
            State = McpConnectionState.Connected,
            LastErrorSummary = null,
        };

        manager.SetStatus("myserver", status);
        var retrieved = manager.GetStatus("myserver");

        Assert.Equal(McpConnectionState.Connected, retrieved.State);
    }
}
