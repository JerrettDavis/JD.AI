using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using JD.AI.Rendering;

namespace JD.AI.Startup;

/// <summary>
/// Probes local daemon and gateway availability for the welcome banner.
/// </summary>
internal static class WelcomeServiceStatusProbe
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan GatewayTimeout = TimeSpan.FromMilliseconds(700);

    public static async Task<IReadOnlyList<WelcomeIndicator>> ProbeSafeAsync(
        CliOptions opts,
        CancellationToken ct = default)
    {
        try
        {
            return await ProbeAsync(opts, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // welcome indicators should not break startup
        catch
#pragma warning restore CA1031
        {
            return [];
        }
    }

    internal static async Task<IReadOnlyList<WelcomeIndicator>> ProbeAsync(
        CliOptions opts,
        CancellationToken ct = default)
    {
        var daemonTask = ProbeDaemonAsync(ct);
        var gatewayTask = ProbeGatewayAsync(opts, ct);
        await Task.WhenAll(daemonTask, gatewayTask).ConfigureAwait(false);

        return [await daemonTask.ConfigureAwait(false), await gatewayTask.ConfigureAwait(false)];
    }

    internal static Uri ResolveGatewayHealthUri(CliOptions opts)
    {
        var configured = Environment.GetEnvironmentVariable("JDAI_GATEWAY_URL");
        if (!string.IsNullOrWhiteSpace(configured)
            && Uri.TryCreate(configured, UriKind.Absolute, out var configuredUri))
        {
            return BuildHealthUri(configuredUri);
        }

        var port = string.IsNullOrWhiteSpace(opts.GatewayPort) ? "5100" : opts.GatewayPort;
        return new Uri($"http://localhost:{port}/health");
    }

    private static async Task<WelcomeIndicator> ProbeDaemonAsync(CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            var result = await RunCommandAsync("sc", "query JDAIDaemon", CommandTimeout, ct).ConfigureAwait(false);
            return ParseWindowsDaemonProbe(result.ExitCode, $"{result.Output}\n{result.Error}", result.TimedOut);
        }

        if (OperatingSystem.IsLinux())
        {
            var result = await RunCommandAsync("systemctl", "is-active jdai-daemon", CommandTimeout, ct).ConfigureAwait(false);
            return ParseSystemdDaemonProbe(result.ExitCode, $"{result.Output}\n{result.Error}", result.TimedOut);
        }

        return new WelcomeIndicator("Daemon", "unsupported OS", IndicatorState.Neutral);
    }

    private static async Task<WelcomeIndicator> ProbeGatewayAsync(
        CliOptions opts,
        CancellationToken ct)
    {
        var healthUri = ResolveGatewayHealthUri(opts);
        using var http = new HttpClient { Timeout = GatewayTimeout };

        try
        {
            using var response = await http.GetAsync(healthUri, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return new WelcomeIndicator("Gateway", "online", IndicatorState.Healthy);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new WelcomeIndicator("Gateway", "online (auth)", IndicatorState.Healthy);

            return new WelcomeIndicator("Gateway", $"http {(int)response.StatusCode}", IndicatorState.Warning);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new WelcomeIndicator("Gateway", "timeout", IndicatorState.Warning);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new WelcomeIndicator("Gateway", "timeout", IndicatorState.Warning);
        }
        catch (HttpRequestException)
        {
            return new WelcomeIndicator("Gateway", "offline", IndicatorState.Warning);
        }
    }

    internal static WelcomeIndicator ParseWindowsDaemonProbe(
        int exitCode,
        string output,
        bool timedOut)
    {
        if (timedOut)
            return new WelcomeIndicator("Daemon", "timeout", IndicatorState.Warning);

        var normalized = output.ToLowerInvariant();
        if (normalized.Contains("state", StringComparison.Ordinal)
            && normalized.Contains("running", StringComparison.Ordinal))
        {
            return new WelcomeIndicator("Daemon", "running", IndicatorState.Healthy);
        }

        if (normalized.Contains("stopped", StringComparison.Ordinal))
            return new WelcomeIndicator("Daemon", "stopped", IndicatorState.Warning);

        if (normalized.Contains("1060", StringComparison.Ordinal)
            || normalized.Contains("does not exist", StringComparison.Ordinal))
        {
            return new WelcomeIndicator("Daemon", "not installed", IndicatorState.Warning);
        }

        return exitCode == 0
            ? new WelcomeIndicator("Daemon", "installed", IndicatorState.Neutral)
            : new WelcomeIndicator("Daemon", "unknown", IndicatorState.Neutral);
    }

    internal static WelcomeIndicator ParseSystemdDaemonProbe(
        int exitCode,
        string output,
        bool timedOut)
    {
        if (timedOut)
            return new WelcomeIndicator("Daemon", "timeout", IndicatorState.Warning);

        var normalized = output.Trim().ToLowerInvariant();
        var firstToken = normalized
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.Equals(firstToken, "active", StringComparison.Ordinal))
            return new WelcomeIndicator("Daemon", "running", IndicatorState.Healthy);

        if (string.Equals(firstToken, "inactive", StringComparison.Ordinal)
            || string.Equals(firstToken, "dead", StringComparison.Ordinal)
            || string.Equals(firstToken, "unknown", StringComparison.Ordinal))
        {
            return new WelcomeIndicator("Daemon", "stopped", IndicatorState.Warning);
        }

        if (string.Equals(firstToken, "failed", StringComparison.Ordinal))
            return new WelcomeIndicator("Daemon", "failed", IndicatorState.Error);

        if (normalized.Contains("not found", StringComparison.Ordinal)
            || normalized.Contains("could not be found", StringComparison.Ordinal))
        {
            return new WelcomeIndicator("Daemon", "not installed", IndicatorState.Warning);
        }

        return exitCode == 0
            ? new WelcomeIndicator("Daemon", normalized.Length == 0 ? "installed" : normalized, IndicatorState.Neutral)
            : new WelcomeIndicator("Daemon", "unknown", IndicatorState.Neutral);
    }

    private static Uri BuildHealthUri(Uri baseUri)
    {
        var path = baseUri.AbsolutePath;
        if (path.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
            return baseUri;

        if (!path.EndsWith('/'))
            path += "/";

        var builder = new UriBuilder(baseUri)
        {
            Path = $"{path}health",
        };
        return builder.Uri;
    }

    private static async Task<CommandResult> RunCommandAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                return new CommandResult(1, string.Empty, "failed to start process", false);

            var outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
                var output = await outputTask.ConfigureAwait(false);
                var error = await errorTask.ConfigureAwait(false);
                return new CommandResult(process.ExitCode, output, error, false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                TryKill(process);
                var output = await outputTask.ConfigureAwait(false);
                var error = await errorTask.ConfigureAwait(false);
                return new CommandResult(1, output, error, true);
            }
        }
        catch (Win32Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message, false);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
            // best effort
        }
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error, bool TimedOut);
}
