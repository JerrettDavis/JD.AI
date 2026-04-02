using System.Net;
using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using JD.AI.Rendering;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

[Collection("EnvironmentVariables")]
public sealed class WelcomeServiceStatusProbeExtendedTests
{
    // ── ResolveGatewayHealthUri ──────────────────────────────────────────

    [Fact]
    public void ResolveGatewayHealthUri_DefaultPort()
    {
        WithGatewayUrlOverride(null, () =>
        {
            var opts = new CliOptions();
            var uri = WelcomeServiceStatusProbe.ResolveGatewayHealthUri(opts);

            uri.Should().Be(new Uri("http://localhost:5100/health"));
        });
    }

    [Fact]
    public void ResolveGatewayHealthUri_CustomPort()
    {
        WithGatewayUrlOverride(null, () =>
        {
            var opts = new CliOptions { GatewayPort = "8080" };
            var uri = WelcomeServiceStatusProbe.ResolveGatewayHealthUri(opts);

            uri.Should().Be(new Uri("http://localhost:8080/health"));
        });
    }

    [Fact]
    public void ResolveGatewayHealthUri_PreservesConfiguredHealthEndpoint()
    {
        WithGatewayUrlOverride("http://127.0.0.1:7777/health", () =>
        {
            var uri = WelcomeServiceStatusProbe.ResolveGatewayHealthUri(new CliOptions());

            uri.Should().Be(new Uri("http://127.0.0.1:7777/health"));
        });
    }

    [Fact]
    public void ResolveGatewayHealthUri_AppendsHealthToConfiguredBasePath()
    {
        WithGatewayUrlOverride("http://127.0.0.1:7777/api", () =>
        {
            var uri = WelcomeServiceStatusProbe.ResolveGatewayHealthUri(new CliOptions());

            uri.Should().Be(new Uri("http://127.0.0.1:7777/api/health"));
        });
    }

    [Fact]
    public void ResolveGatewayHealthUri_IgnoresInvalidConfiguredUrl()
    {
        WithGatewayUrlOverride("not a valid uri", () =>
        {
            var uri = WelcomeServiceStatusProbe.ResolveGatewayHealthUri(new CliOptions { GatewayPort = "9090" });

            uri.Should().Be(new Uri("http://localhost:9090/health"));
        });
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

    [Fact]
    public async Task ProbeAsync_UsesProvidedProbesAndPreservesIndicatorOrder()
    {
        var opts = new CliOptions();

        var indicators = await WelcomeServiceStatusProbe.ProbeAsync(
            opts,
            async ct =>
            {
                await Task.Delay(10, ct);
                return new WelcomeIndicator("Daemon", "running", IndicatorState.Healthy);
            },
            async (_, ct) =>
            {
                await Task.Delay(1, ct);
                return new WelcomeIndicator("Gateway", "online", IndicatorState.Healthy);
            });

        indicators.Should().ContainInOrder(
            new WelcomeIndicator("Daemon", "running", IndicatorState.Healthy),
            new WelcomeIndicator("Gateway", "online", IndicatorState.Healthy));
    }

    [Fact]
    public async Task ProbeSafeAsync_WhenProbeThrows_ReturnsEmpty()
    {
        var indicators = await WelcomeServiceStatusProbe.ProbeSafeAsync(
            new CliOptions(),
            (_, _) => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        indicators.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeSafeAsync_WhenProbeSucceeds_ReturnsProbeResults()
    {
        var expected = new[]
        {
            new WelcomeIndicator("Daemon", "running", IndicatorState.Healthy),
            new WelcomeIndicator("Gateway", "online", IndicatorState.Healthy),
        };

        var indicators = await WelcomeServiceStatusProbe.ProbeSafeAsync(
            new CliOptions(),
            (_, _) => Task.FromResult<IReadOnlyList<WelcomeIndicator>>(expected),
            CancellationToken.None);

        indicators.Should().Equal(expected);
    }

    [Fact]
    public async Task ProbeDaemonAsync_Windows_UsesScQueryAndParsesResult()
    {
        string? command = null;
        string? arguments = null;

        var indicator = await WelcomeServiceStatusProbe.ProbeDaemonAsync(
            isWindows: true,
            isLinux: false,
            (fileName, args, _, _) =>
            {
                command = fileName;
                arguments = args;
                return Task.FromResult((0, "STATE : 4  RUNNING", string.Empty, false));
            },
            CancellationToken.None);

        command.Should().Be("sc");
        arguments.Should().Be("query JDAIDaemon");
        indicator.Should().Be(new WelcomeIndicator("Daemon", "running", IndicatorState.Healthy));
    }

    [Fact]
    public async Task ProbeDaemonAsync_Linux_UsesSystemctlAndParsesResult()
    {
        string? command = null;
        string? arguments = null;

        var indicator = await WelcomeServiceStatusProbe.ProbeDaemonAsync(
            isWindows: false,
            isLinux: true,
            (fileName, args, _, _) =>
            {
                command = fileName;
                arguments = args;
                return Task.FromResult((3, "failed", string.Empty, false));
            },
            CancellationToken.None);

        command.Should().Be("systemctl");
        arguments.Should().Be("is-active jdai-daemon");
        indicator.Should().Be(new WelcomeIndicator("Daemon", "failed", IndicatorState.Error));
    }

    [Fact]
    public async Task ProbeDaemonAsync_UnsupportedOs_ReturnsNeutralWithoutRunningCommand()
    {
        var wasCalled = false;

        var indicator = await WelcomeServiceStatusProbe.ProbeDaemonAsync(
            isWindows: false,
            isLinux: false,
            (_, _, _, _) =>
            {
                wasCalled = true;
                return Task.FromResult((0, string.Empty, string.Empty, false));
            },
            CancellationToken.None);

        wasCalled.Should().BeFalse();
        indicator.Should().Be(new WelcomeIndicator("Daemon", "unsupported OS", IndicatorState.Neutral));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "online", IndicatorState.Healthy)]
    [InlineData(HttpStatusCode.Unauthorized, "online (auth)", IndicatorState.Healthy)]
    [InlineData(HttpStatusCode.Forbidden, "online (auth)", IndicatorState.Healthy)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "http 503", IndicatorState.Warning)]
    public async Task ProbeGatewayAsync_StatusCodes_MapToExpectedIndicator(
        HttpStatusCode statusCode,
        string expectedValue,
        IndicatorState expectedState)
    {
        await WithGatewayUrlOverrideAsync(null, async () =>
        {
            Uri? requestedUri = null;
            using var http = new HttpClient(new StubHttpMessageHandler((request, _) =>
            {
                requestedUri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(statusCode));
            }));

            var indicator = await WelcomeServiceStatusProbe.ProbeGatewayAsync(
                new CliOptions { GatewayPort = "7777" },
                http,
                CancellationToken.None);

            requestedUri.Should().Be(new Uri("http://localhost:7777/health"));
            indicator.Should().Be(new WelcomeIndicator("Gateway", expectedValue, expectedState));
        });
    }

    [Fact]
    public async Task ProbeGatewayAsync_HttpRequestException_ReturnsOfflineWarning()
    {
        await WithGatewayUrlOverrideAsync(null, async () =>
        {
            using var http = new HttpClient(new StubHttpMessageHandler((_, _) =>
                throw new HttpRequestException("connection refused")));

            var indicator = await WelcomeServiceStatusProbe.ProbeGatewayAsync(
                new CliOptions(),
                http,
                CancellationToken.None);

            indicator.Should().Be(new WelcomeIndicator("Gateway", "offline", IndicatorState.Warning));
        });
    }

    [Fact]
    public async Task ProbeGatewayAsync_TaskCanceledException_ReturnsTimeoutWarning()
    {
        await WithGatewayUrlOverrideAsync(null, async () =>
        {
            using var http = new HttpClient(new StubHttpMessageHandler((_, _) =>
                throw new TaskCanceledException("timed out")));

            var indicator = await WelcomeServiceStatusProbe.ProbeGatewayAsync(
                new CliOptions(),
                http,
                CancellationToken.None);

            indicator.Should().Be(new WelcomeIndicator("Gateway", "timeout", IndicatorState.Warning));
        });
    }

    [Fact]
    public async Task ProbeGatewayAsync_OperationCanceledWithoutCallerCancellation_ReturnsTimeoutWarning()
    {
        await WithGatewayUrlOverrideAsync(null, async () =>
        {
            using var http = new HttpClient(new StubHttpMessageHandler((_, _) =>
                throw new OperationCanceledException("timed out")));

            var indicator = await WelcomeServiceStatusProbe.ProbeGatewayAsync(
                new CliOptions(),
                http,
                CancellationToken.None);

            indicator.Should().Be(new WelcomeIndicator("Gateway", "timeout", IndicatorState.Warning));
        });
    }

    [Fact]
    public async Task ProbeGatewayAsync_CallerCancellation_Propagates()
    {
        await WithGatewayUrlOverrideAsync(null, async () =>
        {
            using var http = new HttpClient(new StubHttpMessageHandler((_, cancellationToken) =>
                Task.FromCanceled<HttpResponseMessage>(cancellationToken)));
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => WelcomeServiceStatusProbe.ProbeGatewayAsync(new CliOptions(), http, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        });
    }

    [Fact]
    public async Task RunCommandAsync_WhenCommandSucceeds_CapturesExitCodeOutputAndError()
    {
        var result = await InvokeRunCommandAsync(
            "pwsh",
            "-NoLogo -NoProfile -Command \"Write-Output 'hello'; [Console]::Error.WriteLine('oops')\"",
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("hello");
        result.Error.Should().Contain("oops");
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task RunCommandAsync_WhenCommandTimesOut_ReturnsTimedOut()
    {
        var result = await InvokeRunCommandAsync(
            "pwsh",
            "-NoLogo -NoProfile -Command \"Start-Sleep -Milliseconds 300\"",
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.TimedOut.Should().BeTrue();
    }

    [Fact]
    public async Task RunCommandAsync_WhenExecutableIsMissing_ReturnsWin32Error()
    {
        var result = await InvokeRunCommandAsync(
            "jdai-definitely-missing-executable",
            string.Empty,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.Output.Should().BeEmpty();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.TimedOut.Should().BeFalse();
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    private static void WithGatewayUrlOverride(string? value, Action assertion)
    {
        var previous = Environment.GetEnvironmentVariable("JDAI_GATEWAY_URL");
        try
        {
            Environment.SetEnvironmentVariable("JDAI_GATEWAY_URL", value);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable("JDAI_GATEWAY_URL", previous);
        }
    }

    private static async Task WithGatewayUrlOverrideAsync(string? value, Func<Task> assertion)
    {
        var previous = Environment.GetEnvironmentVariable("JDAI_GATEWAY_URL");
        try
        {
            Environment.SetEnvironmentVariable("JDAI_GATEWAY_URL", value);
            await assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable("JDAI_GATEWAY_URL", previous);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error, bool TimedOut)> InvokeRunCommandAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var method = typeof(WelcomeServiceStatusProbe).GetMethod(
            "RunCommandAsync",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var task = (Task<(int ExitCode, string Output, string Error, bool TimedOut)>)method!.Invoke(
            null,
            [fileName, arguments, timeout, cancellationToken])!;

        return await task;
    }
}
