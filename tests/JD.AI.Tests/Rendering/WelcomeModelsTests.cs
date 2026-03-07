using FluentAssertions;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class WelcomeModelsTests
{
    // ── IndicatorState enum ─────────────────────────────────────────────────

    [Theory]
    [InlineData(IndicatorState.Healthy, 0)]
    [InlineData(IndicatorState.Warning, 1)]
    [InlineData(IndicatorState.Error, 2)]
    [InlineData(IndicatorState.Neutral, 3)]
    public void IndicatorState_Values(IndicatorState state, int expected) =>
        ((int)state).Should().Be(expected);

    // ── WelcomeIndicator record ─────────────────────────────────────────────

    [Fact]
    public void WelcomeIndicator_Construction()
    {
        var indicator = new WelcomeIndicator("Model", "claude-3", IndicatorState.Healthy);
        indicator.Name.Should().Be("Model");
        indicator.Value.Should().Be("claude-3");
        indicator.State.Should().Be(IndicatorState.Healthy);
    }

    [Fact]
    public void WelcomeIndicator_RecordEquality()
    {
        var a = new WelcomeIndicator("X", "Y", IndicatorState.Warning);
        var b = new WelcomeIndicator("X", "Y", IndicatorState.Warning);
        a.Should().Be(b);
    }

    [Fact]
    public void WelcomeIndicator_RecordInequality()
    {
        var a = new WelcomeIndicator("X", "Y", IndicatorState.Healthy);
        var b = new WelcomeIndicator("X", "Y", IndicatorState.Error);
        a.Should().NotBe(b);
    }

    // ── WelcomeBannerDetails record ─────────────────────────────────────────

    [Fact]
    public void WelcomeBannerDetails_DefaultsNull()
    {
        var details = new WelcomeBannerDetails();
        details.WorkingDirectory.Should().BeNull();
        details.Version.Should().BeNull();
        details.Motd.Should().BeNull();
    }

    [Fact]
    public void WelcomeBannerDetails_CustomValues()
    {
        var details = new WelcomeBannerDetails(
            WorkingDirectory: "/home/user/project",
            Version: "1.2.3",
            Motd: "Welcome!");

        details.WorkingDirectory.Should().Be("/home/user/project");
        details.Version.Should().Be("1.2.3");
        details.Motd.Should().Be("Welcome!");
    }

    [Fact]
    public void WelcomeBannerDetails_RecordEquality()
    {
        var a = new WelcomeBannerDetails("/dir", "1.0", "hi");
        var b = new WelcomeBannerDetails("/dir", "1.0", "hi");
        a.Should().Be(b);
    }
}
