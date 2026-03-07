using JD.AI.Core.Installation;
using Spectre.Console;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai update</c> and <c>jdai install</c> CLI subcommands.
/// Detects the current installation method and applies updates using the
/// appropriate strategy (dotnet tool, GitHub release, package manager).
/// </summary>
internal static class UpdateCliHandler
{
    internal static Func<CancellationToken, Task<InstallationInfo>> DetectInstallationAsync { get; set; } =
        InstallationDetector.DetectAsync;

    internal static Func<InstallationInfo, IInstallStrategy> UpdateStrategyFactory { get; set; } =
        InstallerFactory.Create;

    internal static Func<InstallationInfo, IInstallStrategy> InstallStrategyFactory { get; set; } =
        static info => new GitHubReleaseStrategy(info.RuntimeId, info.ExecutablePath);

    public static async Task<int> RunAsync(string subcommand, string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        return subcommand switch
        {
            "update" => await RunUpdateAsync(args, cts.Token).ConfigureAwait(false),
            "install" => await RunInstallAsync(args, cts.Token).ConfigureAwait(false),
            _ => PrintHelp(),
        };
    }

    // ── jdai update [--check] [--force] ─────────────────────────

    private static async Task<int> RunUpdateAsync(string[] args, CancellationToken ct)
    {
        var checkOnly = args.Contains("--check");
        var force = args.Contains("--force");

        if (args.Contains("--help") || args.Contains("-h"))
        {
            AnsiConsole.MarkupLine("[bold]Usage:[/] jdai update [[--check]] [[--force]]");
            AnsiConsole.MarkupLine("  [dim]--check[/]   Check for updates without applying");
            AnsiConsole.MarkupLine("  [dim]--force[/]   Force update even if already on latest");
            return 0;
        }

        // 1. Detect installation
        var info = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync("Detecting installation...", async _ =>
                await DetectInstallationAsync(ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        var strategy = UpdateStrategyFactory(info);

        AnsiConsole.MarkupLine(
            $"[dim]Installed via[/] [bold]{strategy.Name}[/] " +
            $"[dim]({info.RuntimeId})[/]");
        AnsiConsole.MarkupLine(
            $"[dim]Current version:[/] [bold]{Markup.Escape(info.CurrentVersion)}[/]");

        // 2. Check for updates
        var latest = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Checking for updates...", async _ =>
                await strategy.GetLatestVersionAsync(ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (latest is null)
        {
            AnsiConsole.MarkupLine("[yellow]Could not determine the latest version.[/]");
            if (!force) return 1;
        }
        else
        {
            AnsiConsole.MarkupLine(
                $"[dim]Latest version:[/]  [bold]{Markup.Escape(latest)}[/]");
        }

        var isNewer = latest is not null && UpdateChecker.IsNewer(latest, info.CurrentVersion);

        if (!isNewer && !force)
        {
            AnsiConsole.MarkupLine("[green]Already up to date.[/]");
            return 0;
        }

        if (checkOnly)
        {
            if (isNewer)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Update available:[/] {Markup.Escape(info.CurrentVersion)} → [bold]{Markup.Escape(latest!)}[/]");
                AnsiConsole.MarkupLine($"Run [bold]jdai update[/] to apply.");
            }

            return 0;
        }

        // 3. Apply update
        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync($"Updating via {strategy.Name}...", async _ =>
                await strategy.ApplyAsync(force ? null : latest, ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (result.LaunchedDetached)
        {
            AnsiConsole.MarkupLine("[yellow]⬆ Update process launched in a new window.[/]");
            AnsiConsole.MarkupLine("[dim]The update will run after this process exits.[/]");
            AnsiConsole.MarkupLine("[bold yellow]Restart jdai once the update completes.[/]");
            return 0;
        }

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]✓ Update applied successfully.[/]");
            if (!string.IsNullOrWhiteSpace(result.Output))
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(result.Output)}[/]");
            if (result.RequiresRestart)
                AnsiConsole.MarkupLine("[bold yellow]Please restart jdai to use the new version.[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Update failed:[/] {Markup.Escape(result.Output)}");
            return 1;
        }
    }

    // ── jdai install [version] [--force] ────────────────────────

    private static async Task<int> RunInstallAsync(string[] args, CancellationToken ct)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            AnsiConsole.MarkupLine("[bold]Usage:[/] jdai install [[version]] [[--force]]");
            AnsiConsole.MarkupLine("  [dim]version[/]   Version to install (default: latest)");
            AnsiConsole.MarkupLine("  [dim]--force[/]   Force reinstall even if same version");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Installs JD.AI from GitHub releases as a native binary.[/]");
            AnsiConsole.MarkupLine("[dim]No .NET SDK required.[/]");
            return 0;
        }

        var force = args.Contains("--force");
        var targetVersion = args
            .Where(a => !a.StartsWith('-'))
            .FirstOrDefault();

        // Detect current state
        var info = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync("Detecting installation...", async _ =>
                await DetectInstallationAsync(ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        AnsiConsole.MarkupLine(
            $"[dim]Platform:[/] [bold]{info.RuntimeId}[/]  " +
            $"[dim]Current:[/] [bold]{Markup.Escape(info.CurrentVersion)}[/]");

        // For `install`, always use GitHub releases (that's the whole point — no dotnet required)
        var strategy = InstallStrategyFactory(info);

        if (targetVersion is null && !force)
        {
            var latest = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync("Fetching latest release...", async _ =>
                    await strategy.GetLatestVersionAsync(ct).ConfigureAwait(false))
                .ConfigureAwait(false);

            if (latest is null)
            {
                AnsiConsole.MarkupLine("[red]Could not fetch latest release from GitHub.[/]");
                return 1;
            }

            if (!UpdateChecker.IsNewer(latest, info.CurrentVersion))
            {
                AnsiConsole.MarkupLine(
                    $"[green]Already on latest version ({Markup.Escape(latest)}).[/]");
                AnsiConsole.MarkupLine("[dim]Use --force to reinstall.[/]");
                return 0;
            }

            targetVersion = latest;
        }

        AnsiConsole.MarkupLine(
            $"[dim]Installing:[/] [bold]{Markup.Escape(targetVersion ?? "latest")}[/] via GitHub release");

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Downloading and installing...", async _ =>
                await strategy.ApplyAsync(targetVersion, ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (result.Success)
        {
            AnsiConsole.MarkupLine("[green]✓ Installation complete.[/]");
            if (!string.IsNullOrWhiteSpace(result.Output))
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(result.Output)}[/]");
            if (result.RequiresRestart)
                AnsiConsole.MarkupLine("[bold yellow]Please restart jdai to use the new version.[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Installation failed:[/] {Markup.Escape(result.Output)}");
            return 1;
        }
    }

    private static int PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold]jdai update[/] — Check for and apply updates");
        AnsiConsole.MarkupLine("[bold]jdai install[/] — Install from GitHub releases (no .NET required)");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Run [bold]jdai update --help[/] or [bold]jdai install --help[/] for details.");
        return 0;
    }
}
