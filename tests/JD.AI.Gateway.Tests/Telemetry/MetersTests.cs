using FluentAssertions;
using JD.AI.Telemetry;

namespace JD.AI.Gateway.Tests.Telemetry;

public sealed class MetersTests
{
    [Fact]
    public void AllMeterNames_ContainsAgentMeter()
    {
        var names = Meters.AllMeterNames;
        names.Should().HaveCount(1);
        names.Should().Contain(Meters.AgentMeterName);
        Meters.AgentMeterName.Should().Be("JD.AI.Agent");
    }

    [Fact]
    public void TurnCount_HasExpectedName()
    {
        Meters.TurnCount.Name.Should().Be("jdai.agent.turns");
    }

    [Fact]
    public void TurnDuration_HasExpectedName()
    {
        Meters.TurnDuration.Name.Should().Be("jdai.agent.turn_duration");
    }

    [Fact]
    public void TokensUsed_HasExpectedName()
    {
        Meters.TokensUsed.Name.Should().Be("jdai.tokens.total");
    }

    [Fact]
    public void ToolCalls_HasExpectedName()
    {
        Meters.ToolCalls.Name.Should().Be("jdai.tools.invocations");
    }

    [Fact]
    public void ProviderErrors_HasExpectedName()
    {
        Meters.ProviderErrors.Name.Should().Be("jdai.providers.errors");
    }

    [Fact]
    public void ProviderLatency_HasExpectedName()
    {
        Meters.ProviderLatency.Name.Should().Be("jdai.providers.latency");
    }
}
