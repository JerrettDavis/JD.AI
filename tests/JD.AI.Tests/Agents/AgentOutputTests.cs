using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class AgentOutputTests
{
    // ── TurnMetrics record ──────────────────────────────────────────────

    [Fact]
    public void TurnMetrics_RequiredProperties()
    {
        var metrics = new TurnMetrics(1500, 200, 4096);
        metrics.ElapsedMs.Should().Be(1500);
        metrics.TokensOut.Should().Be(200);
        metrics.BytesReceived.Should().Be(4096);
        metrics.TimeToFirstTokenMs.Should().BeNull();
        metrics.ModelName.Should().BeNull();
    }

    [Fact]
    public void TurnMetrics_AllProperties()
    {
        var metrics = new TurnMetrics(2000, 500, 8192,
            TimeToFirstTokenMs: 350, ModelName: "claude-3.5-sonnet");
        metrics.TimeToFirstTokenMs.Should().Be(350);
        metrics.ModelName.Should().Be("claude-3.5-sonnet");
    }

    [Fact]
    public void TurnMetrics_RecordEquality()
    {
        var a = new TurnMetrics(100, 50, 1024);
        var b = new TurnMetrics(100, 50, 1024);
        a.Should().Be(b);
    }

    // ── NullAgentOutput ─────────────────────────────────────────────────

    [Fact]
    public void NullAgentOutput_Singleton()
    {
        NullAgentOutput.Instance.Should().NotBeNull();
        NullAgentOutput.Instance.Should().BeSameAs(NullAgentOutput.Instance);
    }

    [Fact]
    public void NullAgentOutput_AllMethods_DoNotThrow()
    {
        var output = NullAgentOutput.Instance;

        var act = () =>
        {
            output.RenderInfo("info");
            output.RenderWarning("warn");
            output.RenderError("error");
            output.BeginThinking();
            output.WriteThinkingChunk("chunk");
            output.EndThinking();
            output.BeginStreaming();
            output.WriteStreamingChunk("data");
            output.EndStreaming();
            output.BeginTurn();
            output.EndTurn(new TurnMetrics(0, 0, 0));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void NullAgentOutput_ImplementsIAgentOutput()
    {
        NullAgentOutput.Instance.Should().BeAssignableTo<IAgentOutput>();
    }

    // ── AgentOutput static accessor ─────────────────────────────────────

    [Fact]
    public void AgentOutput_Default_IsNullAgentOutput()
    {
        // Reset to default first
        AgentOutput.Current = NullAgentOutput.Instance;
        AgentOutput.Current.Should().BeSameAs(NullAgentOutput.Instance);
    }
}
