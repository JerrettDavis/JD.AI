using JD.AI.Core.Infrastructure;
using Spectre.Console;

namespace JD.AI.Startup;

internal static class SetupCliHandler
{
    internal static Func<string[], Task<int>> RunOnboardingAsync { get; set; } =
        OnboardingCliHandler.RunAsync;

    internal static Func<string, CancellationToken, Task<bool>> IsToolAvailableAsync { get; set; } =
        ProcessExecutor.IsAvailableAsync;

    internal static Func<string, string, TimeSpan, CancellationToken, Task<ProcessResult>> RunProcessAsync { get; set; } =
        (file, arguments, timeout, ct) => ProcessExecutor.RunAsync(file, arguments, timeout: timeout, cancellationToken: ct);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        var daemonOnly = HasFlag(args, "--daemon-only");
        var skipDaemon = HasFlag(args, "--skip-daemon");
        var skipOnboard = HasFlag(args, "--skip-onboard");
        var noUpdate = HasFlag(args, "--no-update");
        var noStart = HasFlag(args, "--no-start");
        var bridge = GetFlagValue(args, "--bridge");

        if (daemonOnly)
            skipOnboard = true;

        if (skipDaemon && skipOnboard)
        {
            AnsiConsole.MarkupLine("[yellow]Nothing to do: both daemon and onboarding steps are skipped.[/]");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(bridge) &&
            !IsKnownBridgeAction(bridge))
        {
            AnsiConsole.MarkupLine($"[red]Unknown bridge action:[/] {Markup.Escape(bridge)}");
            AnsiConsole.MarkupLine("[dim]Allowed values: status, enable, disable, passthrough[/]");
            return 1;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        AnsiConsole.MarkupLine("[bold]JD.AI Setup[/]");
        AnsiConsole.MarkupLine("[dim]Idempotent setup for provider/model defaults, MCP, daemon service, and gateway runtime.[/]");

        var failureCount = 0;

        if (!skipDaemon)
        {
            var daemonExitCode = await RunDaemonSetupAsync(noUpdate, noStart, bridge, cts.Token).ConfigureAwait(false);
            if (daemonExitCode != 0)
                failureCount++;
        }

        if (!skipOnboard)
        {
            var onboardingArgs = FilterOnboardingArgs(args);
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[dim]Onboarding[/]").LeftJustified());
            var onboardExitCode = await RunOnboardingAsync(onboardingArgs).ConfigureAwait(false);
            if (onboardExitCode != 0)
                failureCount++;
        }

        AnsiConsole.WriteLine();
        if (failureCount == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ Setup complete.[/]");
            AnsiConsole.MarkupLine("[dim]Re-run `jdai setup` anytime to change provider/model defaults, MCP servers, or daemon runtime state.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[red]Setup completed with {failureCount} failed step(s).[/]");
        AnsiConsole.MarkupLine("[dim]Fix the failing step(s) above and re-run `jdai setup`.[/]");
        return 1;
    }

    private static async Task<int> RunDaemonSetupAsync(
        bool noUpdate,
        bool noStart,
        string? bridgeAction,
        CancellationToken ct)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[dim]Daemon + Gateway[/]").LeftJustified());

        var dotnetAvailable = await IsToolAvailableAsync("dotnet", ct).ConfigureAwait(false);
        if (!dotnetAvailable)
        {
            AnsiConsole.MarkupLine("[red]dotnet CLI not found on PATH. Install .NET SDK/runtime first.[/]");
            return 1;
        }

        if (!noUpdate)
        {
            var daemonInstalled = await IsToolAvailableAsync("jdai-daemon", ct).ConfigureAwait(false);
            if (daemonInstalled)
            {
                if (await RunCommandStepAsync(
                        "Updating jdai-daemon tool",
                        "dotnet",
                        "tool update -g JD.AI.Daemon",
                        failOnError: false,
                        ct).ConfigureAwait(false) != 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Continuing with existing jdai-daemon installation.[/]");
                }
            }
            else
            {
                var installRc = await RunCommandStepAsync(
                    "Installing jdai-daemon tool",
                    "dotnet",
                    "tool install -g JD.AI.Daemon",
                    failOnError: true,
                    ct).ConfigureAwait(false);
                if (installRc != 0)
                    return 1;
            }
        }

        var installServiceRc = await RunCommandStepAsync(
            "Installing/refreshing daemon service",
            "jdai-daemon",
            "install",
            failOnError: true,
            ct).ConfigureAwait(false);
        if (installServiceRc != 0)
            return 1;

        if (!noStart)
        {
            var startRc = await RunCommandStepAsync(
                "Starting daemon service",
                "jdai-daemon",
                "start",
                failOnError: true,
                ct).ConfigureAwait(false);
            if (startRc != 0)
                return 1;
        }

        var statusRc = await RunCommandStepAsync(
            "Daemon status",
            "jdai-daemon",
            "status",
            failOnError: false,
            ct).ConfigureAwait(false);

        var normalizedBridge = string.IsNullOrWhiteSpace(bridgeAction) ? "status" : bridgeAction.Trim().ToLowerInvariant();
        var bridgeRc = await RunCommandStepAsync(
            "Bridge status",
            "jdai-daemon",
            $"bridge {normalizedBridge}",
            failOnError: false,
            ct).ConfigureAwait(false);

        return statusRc == 0 && bridgeRc == 0 ? 0 : 1;
    }

    private static async Task<int> RunCommandStepAsync(
        string title,
        string fileName,
        string arguments,
        bool failOnError,
        CancellationToken ct)
    {
        ProcessResult result;
        try
        {
            result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync($"{title}...", async _ =>
                    await RunProcessAsync(fileName, arguments, TimeSpan.FromMinutes(2), ct).ConfigureAwait(false))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(title)} failed:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(title)}");
            PrintProcessOutput(result);
            return 0;
        }

        var output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(title)} failed[/]");
        if (!string.IsNullOrWhiteSpace(output))
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(output)}[/]");
        if (failOnError)
            return 1;

        return 0;
    }

    private static void PrintProcessOutput(ProcessResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(result.StandardOutput)}[/]");
        else if (!string.IsNullOrWhiteSpace(result.StandardError))
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(result.StandardError)}[/]");
    }

    private static string[] FilterOnboardingArgs(string[] args)
    {
        var setupFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--daemon-only",
            "--skip-daemon",
            "--skip-onboard",
            "--no-update",
            "--no-start",
            "--bridge",
            "--help",
            "-h",
        };

        var filtered = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (setupFlags.Contains(arg))
            {
                if (string.Equals(arg, "--bridge", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    i++;
                continue;
            }

            filtered.Add(arg);
        }

        return filtered.ToArray();
    }

    private static bool IsKnownBridgeAction(string action) =>
        action.Equals("status", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("enable", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("disable", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("passthrough", StringComparison.OrdinalIgnoreCase);

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetFlagValue(string[] args, string flag)
    {
        var idx = Array.FindIndex(args, a =>
            string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    private static void PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] jdai setup [[options]]");
        AnsiConsole.MarkupLine("[dim]Runs daemon/gateway setup plus provider/model + MCP onboarding in one flow.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Options:");
        AnsiConsole.MarkupLine("  [dim]--daemon-only[/]   Configure daemon/gateway only (skip onboarding)");
        AnsiConsole.MarkupLine("  [dim]--skip-daemon[/]   Run onboarding only");
        AnsiConsole.MarkupLine("  [dim]--skip-onboard[/]  Run daemon/gateway only");
        AnsiConsole.MarkupLine("  [dim]--no-update[/]     Skip `dotnet tool install/update -g JD.AI.Daemon`");
        AnsiConsole.MarkupLine("  [dim]--no-start[/]      Do not start daemon service");
        AnsiConsole.MarkupLine("  [dim]--bridge <mode>[/] bridge mode: status|enable|disable|passthrough");
        AnsiConsole.MarkupLine("  [dim]--provider <name>[/] provider selection (forwarded to onboarding)");
        AnsiConsole.MarkupLine("  [dim]--model <id>[/]      model selection (forwarded to onboarding)");
        AnsiConsole.MarkupLine("  [dim]--global[/]          save global defaults (forwarded to onboarding)");
        AnsiConsole.MarkupLine("  [dim]--skip-mcp[/]        skip MCP catalog step (forwarded)");
        AnsiConsole.MarkupLine("  [dim]--skip-import[/]     skip MCP import step (forwarded)");
    }
}
