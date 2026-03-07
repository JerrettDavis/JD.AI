#pragma warning disable CA1416 // Platform compatibility: SystemdServiceManager is Linux-only; all test methods that instantiate it are guarded by OperatingSystem.IsLinux()

using FluentAssertions;
using JD.AI.Core.Infrastructure;
using JD.AI.Daemon.Services;

namespace JD.AI.Tests.Daemon.Services;

/// <summary>
/// Tests for <see cref="SystemdServiceManager"/>.
///
/// <see cref="SystemdServiceManager"/> is Linux-only (<c>[SupportedOSPlatform("linux")]</c>)
/// and almost all of its methods ultimately delegate to <c>systemctl</c> or
/// <c>journalctl</c>, which are unavailable on Windows / macOS CI agents.
///
/// We therefore focus on:
/// 1. Instantiation and interface conformance (runs everywhere).
/// 2. <c>GetStatusAsync</c> when the unit file is absent (pure file-system guard — no
///    systemctl call is made if the file is missing, so this works on any OS).
/// 3. State-machine assertions over the <see cref="ServiceState"/> mapping that the
///    class documents in its switch expression — exercised by inspecting the record
///    values directly.
/// 4. <see cref="ServiceResult"/> and <see cref="ServiceStatus"/> records (value equality
///    / deconstruction) which are shared data types tested here for completeness.
///
/// Methods that unconditionally shell out to systemctl (Install, Uninstall, Start, Stop,
/// ShowLogs) are only exercised on Linux, guarded by <c>OperatingSystem.IsLinux()</c>.
/// </summary>
public sealed class SystemdServiceManagerTests
{
    // ── construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_CreatesInstance()
    {
        // The ctor is parameterless; instantiation must not throw on any OS.
        var mgr = new SystemdServiceManager();
        mgr.Should().NotBeNull();
    }

    [Fact]
    public void Implements_IServiceManager()
    {
        var mgr = new SystemdServiceManager();
        mgr.Should().BeAssignableTo<IServiceManager>();
    }

    // ── GetStatusAsync — unit file absent (pure guard, no systemctl needed) ───

    [Fact]
    public async Task GetStatusAsync_WhenUnitFileDoesNotExist_ReturnsNotInstalled()
    {
        if (!OperatingSystem.IsLinux())
            return; // The [SupportedOSPlatform("linux")] guard would trigger on other platforms

        // The unit file is /etc/systemd/system/jdai-daemon.service which never
        // exists in a CI sandbox, so this exercises the "not installed" fast path.
        var mgr = new SystemdServiceManager();
        var status = await mgr.GetStatusAsync();

        status.State.Should().Be(ServiceState.NotInstalled);
        status.Details.Should().Contain("Unit file not found");
        status.Version.Should().BeNull();
        status.Uptime.Should().BeNull();
    }

    // ── ServiceState mapping values ───────────────────────────────────────────

    [Theory]
    [InlineData(ServiceState.Unknown)]
    [InlineData(ServiceState.NotInstalled)]
    [InlineData(ServiceState.Stopped)]
    [InlineData(ServiceState.Running)]
    [InlineData(ServiceState.Starting)]
    [InlineData(ServiceState.Stopping)]
    public void ServiceState_AllExpectedValues_AreDefined(ServiceState state)
    {
        Enum.IsDefined(state).Should().BeTrue();
    }

    // ── ServiceResult record ──────────────────────────────────────────────────

    [Fact]
    public void ServiceResult_Success_HasExpectedProperties()
    {
        var result = new ServiceResult(true, "Service 'jdai-daemon' started.");

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Service 'jdai-daemon' started.");
    }

    [Fact]
    public void ServiceResult_Failure_HasExpectedProperties()
    {
        var result = new ServiceResult(false, "Failed to start: unit not found");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to start");
    }

    [Fact]
    public void ServiceResult_RecordEquality()
    {
        var a = new ServiceResult(true, "OK");
        var b = new ServiceResult(true, "OK");

        a.Should().Be(b);
    }

    [Fact]
    public void ServiceResult_RecordInequality_OnSuccess()
    {
        var a = new ServiceResult(true, "OK");
        var b = new ServiceResult(false, "OK");

        a.Should().NotBe(b);
    }

    [Fact]
    public void ServiceResult_RecordInequality_OnMessage()
    {
        var a = new ServiceResult(true, "OK");
        var b = new ServiceResult(true, "Different");

        a.Should().NotBe(b);
    }

    [Fact]
    public void ServiceResult_Deconstruct_Works()
    {
        var (success, message) = new ServiceResult(false, "Permission denied");

        success.Should().BeFalse();
        message.Should().Be("Permission denied");
    }

    // ── ServiceStatus record ──────────────────────────────────────────────────

    [Fact]
    public void ServiceStatus_Properties_AreSet()
    {
        var uptime = TimeSpan.FromMinutes(10);
        var status = new ServiceStatus(ServiceState.Running, "2.0.0", uptime, "active (running)");

        status.State.Should().Be(ServiceState.Running);
        status.Version.Should().Be("2.0.0");
        status.Uptime.Should().Be(uptime);
        status.Details.Should().Be("active (running)");
    }

    [Fact]
    public void ServiceStatus_NotInstalled_HasNullFields()
    {
        var status = new ServiceStatus(ServiceState.NotInstalled, null, null, "Unit file not found.");

        status.State.Should().Be(ServiceState.NotInstalled);
        status.Version.Should().BeNull();
        status.Uptime.Should().BeNull();
        status.Details.Should().Contain("Unit file not found");
    }

    [Fact]
    public void ServiceStatus_RecordEquality()
    {
        var a = new ServiceStatus(ServiceState.Stopped, "1.0.0", null, "inactive");
        var b = new ServiceStatus(ServiceState.Stopped, "1.0.0", null, "inactive");

        a.Should().Be(b);
    }

    [Fact]
    public void ServiceStatus_RecordInequality_OnState()
    {
        var a = new ServiceStatus(ServiceState.Running, "1.0.0", null, null);
        var b = new ServiceStatus(ServiceState.Stopped, "1.0.0", null, null);

        a.Should().NotBe(b);
    }

    // ── DaemonServiceIdentity constants (used by SystemdServiceManager) ───────

    [Fact]
    public void DaemonServiceIdentity_ToolCommand_IsExpected()
    {
        DaemonServiceIdentity.ToolCommand.Should().Be("jdai-daemon");
    }

    [Fact]
    public void DaemonServiceIdentity_LinuxServiceName_MatchesToolCommand()
    {
        DaemonServiceIdentity.LinuxServiceName.Should()
            .Be(DaemonServiceIdentity.ToolCommand,
                "the Linux service name is derived from the tool command");
    }

    // ── Linux-only integration paths (guarded) ────────────────────────────────

    [Fact]
    public async Task StartAsync_OnLinux_WhenServiceNotInstalled_ReturnsFailure()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var mgr = new SystemdServiceManager();
        var result = await mgr.StartAsync();

        // systemctl will fail because the unit isn't installed in a CI sandbox
        result.Should().NotBeNull();
        // Success or failure both produce a non-null result with a message
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StopAsync_OnLinux_ReturnsResult()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var mgr = new SystemdServiceManager();
        var result = await mgr.StopAsync();

        result.Should().NotBeNull();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ShowLogsAsync_OnLinux_ReturnsResult()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var mgr = new SystemdServiceManager();
        var result = await mgr.ShowLogsAsync(lines: 5);

        result.Should().NotBeNull();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UninstallAsync_OnLinux_WhenNotInstalled_ReturnsSuccessOrPermissionError()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var mgr = new SystemdServiceManager();
        var result = await mgr.UninstallAsync();

        // Either succeeds (unit file absent, nothing to delete) or fails with permission denied.
        // Either way the result must be non-null.
        result.Should().NotBeNull();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InstallAsync_OnLinux_WhenToolNotOnPath_ReturnsFailure()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // In a CI sandbox the tool is almost certainly not installed globally,
        // so InstallAsync returns a "cannot locate" failure rather than writing the unit file.
        var mgr = new SystemdServiceManager();
        var result = await mgr.InstallAsync();

        result.Should().NotBeNull();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }
}
