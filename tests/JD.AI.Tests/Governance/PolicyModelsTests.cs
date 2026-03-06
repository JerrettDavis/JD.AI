using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class PolicyModelsTests
{
    // ── PolicyMetadata ──────────────────────────────────────────────────

    [Fact]
    public void PolicyMetadata_Defaults()
    {
        var meta = new PolicyMetadata();
        meta.Name.Should().BeEmpty();
        meta.Scope.Should().Be(PolicyScope.User);
        meta.Priority.Should().Be(0);
        meta.Version.Should().BeNull();
        meta.RequireSignature.Should().BeFalse();
    }

    [Fact]
    public void PolicyMetadata_AllProperties()
    {
        var meta = new PolicyMetadata
        {
            Name = "security-policy",
            Scope = PolicyScope.Organization,
            Priority = 10,
            Version = "2.0.0",
            RequireSignature = true,
        };
        meta.Name.Should().Be("security-policy");
        meta.Scope.Should().Be(PolicyScope.Organization);
        meta.Priority.Should().Be(10);
        meta.Version.Should().Be("2.0.0");
        meta.RequireSignature.Should().BeTrue();
    }

    // ── PolicyScope enum ────────────────────────────────────────────────

    [Theory]
    [InlineData(PolicyScope.Global, 0)]
    [InlineData(PolicyScope.Organization, 1)]
    [InlineData(PolicyScope.Team, 2)]
    [InlineData(PolicyScope.Project, 3)]
    [InlineData(PolicyScope.User, 4)]
    public void PolicyScope_Values(PolicyScope scope, int expected) =>
        ((int)scope).Should().Be(expected);

    // ── CircuitBreakerPolicy ────────────────────────────────────────────

    [Fact]
    public void CircuitBreakerPolicy_Defaults()
    {
        var policy = new CircuitBreakerPolicy();
        policy.RepetitionWarningThreshold.Should().Be(3);
        policy.RepetitionHardStopThreshold.Should().Be(5);
        policy.PingPongThreshold.Should().Be(4);
        policy.WindowSize.Should().Be(50);
        policy.CooldownSeconds.Should().Be(30);
        policy.Hardened.Should().BeFalse();
    }

    [Fact]
    public void CircuitBreakerPolicy_CustomValues()
    {
        var policy = new CircuitBreakerPolicy
        {
            RepetitionWarningThreshold = 2,
            RepetitionHardStopThreshold = 3,
            PingPongThreshold = 6,
            WindowSize = 100,
            CooldownSeconds = 60,
            Hardened = true,
        };
        policy.RepetitionWarningThreshold.Should().Be(2);
        policy.RepetitionHardStopThreshold.Should().Be(3);
        policy.PingPongThreshold.Should().Be(6);
        policy.WindowSize.Should().Be(100);
        policy.CooldownSeconds.Should().Be(60);
        policy.Hardened.Should().BeTrue();
    }
}
