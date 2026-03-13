using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class FooterStateProviderTests
{
    [Fact]
    public void BuildState_PopulatesRequiredFields_AfterUpdate()
    {
        var provider = new FooterStateProvider("/home/user/projects/myapp");

        provider.Update(
            provider: "Anthropic",
            model: "claude-sonnet-4-6",
            tokensUsed: 12_000,
            contextWindow: 200_000,
            turnCount: 5,
            mode: PermissionMode.Normal,
            warnThresholdPercent: 15.0);

        var state = provider.CurrentState;

        state.WorkingDirectory.Should().Be("/home/user/projects/myapp");
        state.Provider.Should().Be("Anthropic");
        state.Model.Should().Be("claude-sonnet-4-6");
        state.ContextTokensUsed.Should().Be(12_000);
        state.ContextWindowSize.Should().Be(200_000);
        state.TurnCount.Should().Be(5);
        state.Mode.Should().Be(PermissionMode.Normal);
        state.WarnThresholdPercent.Should().Be(15.0);
    }

    [Fact]
    public void SetGitInfo_UpdatesBranchAndPr()
    {
        var provider = new FooterStateProvider("/home/user/projects/myapp");

        provider.SetGitInfo("feature/my-branch", "https://github.com/example/repo/pull/42");

        var state = provider.CurrentState;

        state.GitBranch.Should().Be("feature/my-branch");
        state.PrLink.Should().Be("https://github.com/example/repo/pull/42");
    }

    [Fact]
    public void SetGitInfo_WithNullValues_ClearsBranch()
    {
        var provider = new FooterStateProvider("/home/user/projects/myapp");

        provider.SetGitInfo("main", "https://github.com/example/repo/pull/1");
        provider.SetGitInfo(null, null);

        var state = provider.CurrentState;

        state.GitBranch.Should().BeNull();
        state.PrLink.Should().BeNull();
    }

    [Fact]
    public void AddPluginSegment_AppearsInState()
    {
        var provider = new FooterStateProvider("/home/user/projects/myapp");

        provider.AddPluginSegment("my-plugin", "hello world", priority: 1);

        var state = provider.CurrentState;

        state.PluginSegments.Should().ContainSingle(s =>
            s.Key == "my-plugin" &&
            s.Value == "hello world" &&
            s.Priority == 1);
    }
}
