using FluentAssertions;
using JD.AI.Rendering;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class WelcomeServiceStatusProbeExtendedTests
{
    // ── ResolveGatewayHealthUri ──────────────────────────────────────────

    [Fact]
    public void ResolveGatewayHealthUri_DefaultPort()
    {
        var opts = new CliOptions();
        var uri = WelcomeServiceStatusProbe.ResolveGatewayHealthUri(opts);

        uri.Should().Be(new Uri("http://localhost:5100/health"));
    }

    [Fact]
    public void ResolveGatewayHealthUri_CustomPort()
    {
        var opts = new CliOptions { GatewayPort = "8080" };
        var uri = WelcomeServiceStatusProbe.ResolveGatewayHealthUri(opts);

        uri.Should().Be(new Uri("http://localhost:8080/health"));
    }

    // ── WelcomeIndicator record ─────────────────────────────────────────

    [Fact]
    public void WelcomeIndicator_Construction()
    {
        var indicator = new WelcomeIndicator("Service", "status", IndicatorState.Healthy);
        indicator.Name.Should().Be("Service");
        indicator.Value.Should().Be("status");
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

    // ── IndicatorState enum ──────────────────────────────────────────────

    [Fact]
    public void IndicatorState_HasExpectedValues()
    {
        Enum.GetValues<IndicatorState>().Should().Contain(IndicatorState.Healthy);
        Enum.GetValues<IndicatorState>().Should().Contain(IndicatorState.Warning);
        Enum.GetValues<IndicatorState>().Should().Contain(IndicatorState.Error);
        Enum.GetValues<IndicatorState>().Should().Contain(IndicatorState.Neutral);
    }

    // ── ParseWindowsDaemonProbe edge cases ───────────────────────────────

    [Fact]
    public void ParseWindows_CaseInsensitiveRunning()
    {
        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            0, "STATE : 4  RUNNING", timedOut: false);
        result.Value.Should().Be("running");
    }

    [Fact]
    public void ParseWindows_EmptyOutput_ExitZero_ReturnsInstalled()
    {
        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            0, "", timedOut: false);
        result.Value.Should().Be("installed");
    }

    // ── ParseSystemdDaemonProbe edge cases ───────────────────────────────

    [Fact]
    public void ParseSystemd_EmptyOutput_ExitZero_ReturnsInstalled()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            0, "", timedOut: false);
        result.Value.Should().Be("installed");
        result.State.Should().Be(IndicatorState.Neutral);
    }

    [Fact]
    public void ParseSystemd_WhitespaceOutput_ExitNonZero_ReturnsUnknown()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            4, "   \n  ", timedOut: false);
        result.Value.Should().Be("unknown");
    }

    [Fact]
    public void ParseSystemd_NotFound_AlternateWording()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            4, "Unit jdai-daemon.service not found.", timedOut: false);
        result.Value.Should().Be("not installed");
    }
}
