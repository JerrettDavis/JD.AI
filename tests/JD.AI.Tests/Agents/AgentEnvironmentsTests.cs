using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class AgentEnvironmentsTests
{
    // ── Constants ───────────────────────────────────────────────────────────

    [Fact]
    public void Dev_Constant() => AgentEnvironments.Dev.Should().Be("dev");

    [Fact]
    public void Staging_Constant() => AgentEnvironments.Staging.Should().Be("staging");

    [Fact]
    public void Prod_Constant() => AgentEnvironments.Prod.Should().Be("prod");

    // ── All ─────────────────────────────────────────────────────────────────

    [Fact]
    public void All_ContainsThreeEnvironments()
    {
        AgentEnvironments.All.Should().HaveCount(3);
    }

    [Fact]
    public void All_OrderedLowestToHighest()
    {
        AgentEnvironments.All.Should().ContainInOrder("dev", "staging", "prod");
    }

    [Theory]
    [InlineData("dev")]
    [InlineData("DEV")]
    [InlineData("Staging")]
    [InlineData("prod")]
    public void IsKnown_RecognizesSupportedValues(string input) =>
        AgentEnvironments.IsKnown(input).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("qa")]
    [InlineData("..\\prod")]
    public void IsKnown_RejectsUnsupportedValues(string? input) =>
        AgentEnvironments.IsKnown(input).Should().BeFalse();

    [Theory]
    [InlineData("dev", "dev")]
    [InlineData("DEV", "dev")]
    [InlineData("Staging", "staging")]
    public void Normalize_ReturnsCanonicalEnvironment(string input, string expected) =>
        AgentEnvironments.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_Unknown_Throws() =>
        FluentActions.Invoking(() => AgentEnvironments.Normalize("..\\prod"))
            .Should()
            .Throw<ArgumentException>()
            .WithMessage("Environment must be one of:*");

    // ── NextAfter ───────────────────────────────────────────────────────────

    [Fact]
    public void NextAfter_Dev_ReturnsStaging() =>
        AgentEnvironments.NextAfter("dev").Should().Be("staging");

    [Fact]
    public void NextAfter_Staging_ReturnsProd() =>
        AgentEnvironments.NextAfter("staging").Should().Be("prod");

    [Fact]
    public void NextAfter_Prod_ReturnsNull() =>
        AgentEnvironments.NextAfter("prod").Should().BeNull();

    [Fact]
    public void NextAfter_Unknown_ReturnsNull() =>
        AgentEnvironments.NextAfter("unknown").Should().BeNull();

    [Theory]
    [InlineData("DEV", "staging")]
    [InlineData("Dev", "staging")]
    [InlineData("STAGING", "prod")]
    [InlineData("Staging", "prod")]
    [InlineData("PROD", null)]
    public void NextAfter_CaseInsensitive(string input, string? expected) =>
        AgentEnvironments.NextAfter(input).Should().Be(expected);
}
