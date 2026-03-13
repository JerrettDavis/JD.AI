using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class ToolPermissionProfileTests
{
    [Fact]
    public void GlobalAllow_MatchesExactName()
    {
        var profile = new ToolPermissionProfile();
        profile.AddAllowed("run_command", projectScope: false);

        profile.IsExplicitlyAllowed("run_command").Should().BeTrue();
    }

    [Fact]
    public void ProjectDeny_WinsForMatchingRule()
    {
        var profile = new ToolPermissionProfile();
        profile.AddAllowed("*", projectScope: false);
        profile.AddDenied("git_*", projectScope: true);

        profile.IsExplicitlyDenied("git_push").Should().BeTrue();
    }

    [Fact]
    public void GlobRules_SupportWildcardMatching()
    {
        ToolPermissionProfile.MatchesRule("mcp.server.call", "mcp.*").Should().BeTrue();
        ToolPermissionProfile.MatchesRule("run_command", "read_*").Should().BeFalse();
    }
}
