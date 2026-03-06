using FluentAssertions;
using JD.AI.Rendering;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class WelcomeServiceStatusProbeTests
{
    // ── ParseWindowsDaemonProbe ──────────────────────────────

    [Fact]
    public void ParseWindows_Timeout_ReturnsWarning()
    {
        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            1, "", timedOut: true);

        result.Name.Should().Be("Daemon");
        result.Value.Should().Be("timeout");
        result.State.Should().Be(IndicatorState.Warning);
    }

    [Fact]
    public void ParseWindows_StateRunning_ReturnsHealthy()
    {
        var output = """
            SERVICE_NAME: JDAIDaemon
                    TYPE : 10  WIN32_OWN_PROCESS
                    STATE : 4  RUNNING
            """;

        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            0, output, timedOut: false);

        result.Value.Should().Be("running");
        result.State.Should().Be(IndicatorState.Healthy);
    }

    [Fact]
    public void ParseWindows_Stopped_ReturnsWarning()
    {
        var output = "STATE : 1  STOPPED";

        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            0, output, timedOut: false);

        result.Value.Should().Be("stopped");
        result.State.Should().Be(IndicatorState.Warning);
    }

    [Fact]
    public void ParseWindows_Error1060_ReturnsNotInstalled()
    {
        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            1, "FAILED 1060: The specified service does not exist",
            timedOut: false);

        result.Value.Should().Be("not installed");
        result.State.Should().Be(IndicatorState.Warning);
    }

    [Fact]
    public void ParseWindows_DoesNotExist_ReturnsNotInstalled()
    {
        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            1, "The specified service does not exist as an installed service.",
            timedOut: false);

        result.Value.Should().Be("not installed");
    }

    [Fact]
    public void ParseWindows_ExitZeroUnknown_ReturnsInstalled()
    {
        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            0, "some unrecognized output", timedOut: false);

        result.Value.Should().Be("installed");
        result.State.Should().Be(IndicatorState.Neutral);
    }

    [Fact]
    public void ParseWindows_NonZeroUnknown_ReturnsUnknown()
    {
        var result = WelcomeServiceStatusProbe.ParseWindowsDaemonProbe(
            1, "some error", timedOut: false);

        result.Value.Should().Be("unknown");
        result.State.Should().Be(IndicatorState.Neutral);
    }

    // ── ParseSystemdDaemonProbe ─────────────────────────────

    [Fact]
    public void ParseSystemd_Timeout_ReturnsWarning()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            1, "", timedOut: true);

        result.Value.Should().Be("timeout");
        result.State.Should().Be(IndicatorState.Warning);
    }

    [Fact]
    public void ParseSystemd_Active_ReturnsHealthy()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            0, "active", timedOut: false);

        result.Value.Should().Be("running");
        result.State.Should().Be(IndicatorState.Healthy);
    }

    [Fact]
    public void ParseSystemd_Inactive_ReturnsStopped()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            3, "inactive", timedOut: false);

        result.Value.Should().Be("stopped");
        result.State.Should().Be(IndicatorState.Warning);
    }

    [Fact]
    public void ParseSystemd_Dead_ReturnsStopped()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            3, "dead", timedOut: false);

        result.Value.Should().Be("stopped");
    }

    [Fact]
    public void ParseSystemd_Failed_ReturnsError()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            3, "failed", timedOut: false);

        result.Value.Should().Be("failed");
        result.State.Should().Be(IndicatorState.Error);
    }

    [Fact]
    public void ParseSystemd_NotFound_ReturnsNotInstalled()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            4, "Unit jdai-daemon.service could not be found.",
            timedOut: false);

        result.Value.Should().Be("not installed");
    }

    [Fact]
    public void ParseSystemd_ExitZeroUnknown_ReturnsNeutral()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            0, "activating", timedOut: false);

        result.State.Should().Be(IndicatorState.Neutral);
    }

    [Fact]
    public void ParseSystemd_Unknown_Status_ReturnsStopped()
    {
        var result = WelcomeServiceStatusProbe.ParseSystemdDaemonProbe(
            3, "unknown", timedOut: false);

        result.Value.Should().Be("stopped");
        result.State.Should().Be(IndicatorState.Warning);
    }
}
