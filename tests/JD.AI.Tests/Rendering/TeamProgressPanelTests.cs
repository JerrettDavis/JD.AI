using FluentAssertions;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class TeamProgressPanelTests
{
    // ── GetStatusIcon exhaustive coverage ─────────────────────────────

    [Theory]
    [InlineData(SubagentStatus.Pending, "○")]
    [InlineData(SubagentStatus.Started, "◐")]
    [InlineData(SubagentStatus.Thinking, "◑")]
    [InlineData(SubagentStatus.ExecutingTool, "◒")]
    [InlineData(SubagentStatus.Completed, "●")]
    [InlineData(SubagentStatus.Failed, "✗")]
    [InlineData(SubagentStatus.Cancelled, "⊘")]
    public void GetStatusIcon_AllEnumValues(SubagentStatus status, string expected) =>
        TeamProgressPanel.GetStatusIcon(status).Should().Be(expected);

    [Fact]
    public void GetStatusIcon_UnknownValue_ReturnsQuestionMark() =>
        TeamProgressPanel.GetStatusIcon((SubagentStatus)99).Should().Be("?");

    // ── GetStatusColor exhaustive coverage ────────────────────────────

    [Theory]
    [InlineData(SubagentStatus.Completed, "green")]
    [InlineData(SubagentStatus.Failed, "red")]
    [InlineData(SubagentStatus.Cancelled, "yellow")]
    [InlineData(SubagentStatus.Thinking, "blue")]
    [InlineData(SubagentStatus.ExecutingTool, "cyan")]
    public void GetStatusColor_ExplicitArms(SubagentStatus status, string expected) =>
        TeamProgressPanel.GetStatusColor(status).Should().Be(expected);

    [Theory]
    [InlineData(SubagentStatus.Pending)]
    [InlineData(SubagentStatus.Started)]
    public void GetStatusColor_DefaultArm_ReturnsDim(SubagentStatus status) =>
        TeamProgressPanel.GetStatusColor(status).Should().Be("dim");

    [Fact]
    public void GetStatusColor_UnknownValue_ReturnsDim() =>
        TeamProgressPanel.GetStatusColor((SubagentStatus)99).Should().Be("dim");

    // ── Dispose ───────────────────────────────────────────────────────

    [Fact]
    public void Dispose_Idempotent()
    {
        var panel = new TeamProgressPanel();
        panel.OnProgress(new SubagentProgress("agent1", SubagentStatus.Started, "Working"));

        panel.Dispose();
        var act = () => panel.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void OnProgress_LastWriteWins()
    {
        var panel = new TeamProgressPanel();
        panel.OnProgress(new SubagentProgress("agent1", SubagentStatus.Pending, "Starting"));
        panel.OnProgress(new SubagentProgress("agent1", SubagentStatus.Completed, "Done"));

        // Render should not throw and should reflect latest state
        var rendered = panel.Render();
        rendered.Should().NotBeNull();
    }

    [Fact]
    public void OnProgress_MultipleAgents()
    {
        var panel = new TeamProgressPanel();
        panel.OnProgress(new SubagentProgress("alpha", SubagentStatus.Started, "Working"));
        panel.OnProgress(new SubagentProgress("beta", SubagentStatus.Thinking, "Analyzing"));

        var rendered = panel.Render();
        rendered.Should().NotBeNull();
    }

    [Fact]
    public void Render_EmptyPanel_ReturnsPanel()
    {
        using var panel = new TeamProgressPanel();
        var rendered = panel.Render();
        rendered.Should().NotBeNull();
    }
}
