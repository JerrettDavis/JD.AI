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

    /// <summary>Factory for JDAIToolkit operations. Override in tests to inject mock behavior.</summary>
    internal static Func<CancellationToken, Task<IReadOnlyList<InstalledTool>>> GetInstalledToolsAsync { get; set; } =
        JDAIToolkit.GetInstalledToolsAsync;

    /// <summary>Factory for JDAIToolkit.CheckAllAsync. Override in tests.</summary>
    internal static Func<IReadOnlyList<InstalledTool>?, CancellationToken, Task<UpdatePlan>> CheckAllAsync { get; set; } =
        JDAIToolkit.CheckAllAsync;

    /// <summary>Factory for JDAIToolkit.GetLatestVersionAsync. Override in tests.</summary>
    internal static Func<string, CancellationToken, Task<string?>> GetLatestVersionAsync { get; set; } =
        JDAIToolkit.GetLatestVersionAsync;

    /// <summary>Factory for resolving the currently installed version of a named global tool.</summary>
    internal static Func<string, CancellationToken, Task<string?>> GetInstalledToolVersionAsync { get; set; } =
        GetInstalledToolVersionCoreAsync;

    /// <summary>Factory for applying a named tool update. Override in tests.</summary>
    internal static Func<string, string?, CancellationToken, Task<InstallResult>> ApplyToolUpdateAsync { get; set; } =
        JDAIToolkit.ApplyAsync;

    /// <summary>Factory for applying a multi-tool update plan. Override in tests.</summary>
    internal static Func<UpdatePlan, bool, Action<InstalledTool, InstallResult>?, CancellationToken, Task>
        ApplyAllToolUpdatesAsync { get; set; } = JDAIToolkit.ApplyAllAsync;

    public static async Task<int> RunAsync(string subcommand, string[] args)
    {
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            return subcommand switch
            {
                "update" => await RunUpdateAsync(args, cts.Token).ConfigureAwait(false),
                "install" => await RunInstallAsync(args, cts.Token).ConfigureAwait(false),
                _ => PrintHelp(),
            };
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return 130;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    // ── jdai update [--self] [--check] [--all] [tool] ──────────

    private static async Task<int> RunUpdateAsync(string[] args, CancellationToken ct)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            AnsiConsole.MarkupLine("[bold]Usage:[/] jdai update [[--self]] [[--check]] [[--all]] [[--force]]");
            AnsiConsole.MarkupLine("  [dim]--self[/]    Update only the jdai tool (default: all tools)");
            AnsiConsole.MarkupLine("  [dim]--check[/]   Show update plan without applying");
            AnsiConsole.MarkupLine("  [dim]--all[/]     Check and update all installed JD.AI tools");
            AnsiConsole.MarkupLine("  [dim]--force[/]   Apply even if already on latest (self or named tool only)");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  Update a named tool: jdai update [dim]<tool-name>[/]");
            return 0;
        }

        var allowedFlags = new HashSet<string>(StringComparer.Ordinal)
        {
            "--check",
            "--self",
            "--all",
            "--force",
            "--help",
            "-h",
        };

        var unknownFlags = args
            .Where(a => a.StartsWith('-') && !allowedFlags.Contains(a))
            .ToArray();
        var positionals = args
            .Where(a => !a.StartsWith('-'))
            .ToArray();

        if (unknownFlags.Length > 0 || positionals.Length > 1)
        {
            AnsiConsole.MarkupLine("[red]Invalid arguments.[/]");
            return 1;
        }

        var checkOnly = args.Contains("--check");
        var selfOnly = args.Contains("--self");
        var allTools = args.Contains("--all");
        var namedTool = positionals.FirstOrDefault();
        var force = args.Contains("--force");

        var targetModes = (selfOnly ? 1 : 0) + (allTools ? 1 : 0) + (namedTool is not null ? 1 : 0);
        if (targetModes > 1)
        {
            AnsiConsole.MarkupLine("[red]Choose only one of --self, --all, or <tool>.[/]");
            return 1;
        }

        if (checkOnly && force)
        {
            AnsiConsole.MarkupLine("[red]--force cannot be used with --check.[/]");
            return 1;
        }

        if (force && !selfOnly && namedTool is null)
        {
            AnsiConsole.MarkupLine("[red]--force is not supported with bulk update yet.[/]");
            return 1;
        }

        if (selfOnly)
        {
            return await RunSelfUpdateAsync(checkOnly, force, ct).ConfigureAwait(false);
        }

        if (checkOnly && !allTools && namedTool is null)
        {
            var installedTools = await GetInstalledToolsAsync(ct).ConfigureAwait(false);
            if (installedTools.Count == 0)
            {
                return await RunSelfUpdateAsync(checkOnly: true, force: false, ct).ConfigureAwait(false);
            }
        }

        // Multi-tool mode: --check, --all, or positional tool name
        if (checkOnly || allTools || namedTool is not null)
        {
            return await RunMultiToolUpdateAsync(checkOnly, namedTool, force, ct)
                .ConfigureAwait(false);
        }

        // Default (no flags): multi-tool update all
        var tools = await GetInstalledToolsAsync(ct).ConfigureAwait(false);

        // Fall back to single-tool mode if no other JD.AI tools are detected
        if (tools.Count == 0)
        {
            return await RunSelfUpdateAsync(checkOnly, force, ct).ConfigureAwait(false);
        }

        return await RunMultiToolUpdateAsync(checkOnly: false, namedTool: null, force: false, ct)
            .ConfigureAwait(false);
    }

    // ── Multi-tool update (all JD.AI tools) ──────────────────────

    private static async Task<int> RunMultiToolUpdateAsync(
        bool checkOnly,
        string? namedTool,
        bool force,
        CancellationToken ct)
    {
        if (namedTool is not null)
        {
            // Update a specific named tool
            return await UpdateNamedToolAsync(namedTool, checkOnly, force, ct)
                .ConfigureAwait(false);
        }

        // Get all installed JD.AI tools
        var tools = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync("Detecting installed JD.AI tools...", async _ =>
                await GetInstalledToolsAsync(ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (tools.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No JD.AI tools found installed.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[dim]Found [bold]{tools.Count}[/] JD.AI tool(s):[/]");
        foreach (var tool in tools)
        {
            AnsiConsole.MarkupLine($"  [dim]-[/] [bold]{tool.PackageId}[/] [dim]({tool.ToolName})[/] — v{tool.CurrentVersion}");
        }

        var plan = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Checking for updates...", async _ =>
                await CheckAllAsync(tools, ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        // Print the update table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Tool[/]")
            .AddColumn("[bold]Current[/]")
            .AddColumn("[bold]Latest[/]")
            .AddColumn("[bold]Status[/]");

        foreach (var tool in plan.Tools)
        {
            var result = plan.Results.FirstOrDefault(u => string.Equals(u.Tool.PackageId, tool.PackageId, StringComparison.Ordinal));
            var status = result?.LatestVersion is null
                ? "[yellow]Unknown[/]"
                : result.IsNewer
                ? $"[yellow]Update available[/]"
                : "[green]Up to date[/]";
            table.AddRow(
                tool.PackageId,
                tool.CurrentVersion,
                result?.LatestVersion ?? tool.CurrentVersion,
                status);
        }

        AnsiConsole.Write(table);

        if (plan.UnknownCount > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Some tool versions could not be determined.[/]");
            return 1;
        }

        if (!plan.HasUpdates && !force)
        {
            AnsiConsole.MarkupLine("[green]All tools are up to date.[/]");
            return 0;
        }

        if (checkOnly)
        {
            if (plan.HasUpdates)
            {
                AnsiConsole.MarkupLine($"[yellow]Run [bold]jdai update --all[/] to apply.[/]");
            }
            return 0;
        }

        // Apply all updates
        AnsiConsole.MarkupLine($"[dim]Applying [bold]{plan.UpdateCount}[/] update(s)...[/]");

        var success = true;
        await ApplyAllToolUpdatesAsync(
            plan,
            true,
            (tool, result) =>
            {
                if (result.Success)
                {
                    AnsiConsole.MarkupLine(
                        $"  [green]✓[/] [bold]{tool.PackageId}[/] updated.");
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"  [red]✗[/] [bold]{tool.PackageId}[/] failed: {result.Output}");
                    success = false;
                }
            },
            ct).ConfigureAwait(false);

        if (success && plan.UpdateCount > 0)
        {
            AnsiConsole.MarkupLine("[green]All tools updated successfully.[/]");
        }

        return success ? 0 : 1;
    }

    // ── Single-tool update (named) ────────────────────────────────

    private static async Task<int> UpdateNamedToolAsync(
        string toolName,
        bool checkOnly,
        bool force,
        CancellationToken ct)
    {
        // Resolve tool name → package ID
        var packageId = ResolveToolName(toolName);

        AnsiConsole.MarkupLine($"[dim]Tool:[/] [bold]{packageId}[/]");

        // Get current version via dotnet tool show
        var showResult = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync($"Checking {packageId}...", async _ =>
                await GetInstalledToolVersionAsync(packageId, ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (showResult is null)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(packageId)} is not installed as a global tool.[/]");
            return 1;
        }

        var currentVersion = showResult;
        AnsiConsole.MarkupLine($"[dim]Current:[/] [bold]{currentVersion}[/]");

        var latest = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Fetching latest version...", async _ =>
                await GetLatestVersionAsync(packageId, ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (latest is null)
        {
            AnsiConsole.MarkupLine("[red]Could not determine latest version from NuGet.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[dim]Latest:[/]  [bold]{latest}[/]");

        var isNewer = JDAIToolkit.CompareVersions(latest, currentVersion) > 0;
        if (!isNewer && !force)
        {
            AnsiConsole.MarkupLine("[green]Already up to date.[/]");
            return 0;
        }

        if (checkOnly)
        {
            AnsiConsole.MarkupLine($"[yellow]Update available:[/] {currentVersion} → [bold]{latest}[/]");
            return 0;
        }

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync($"Updating {packageId}...", async _ =>
                await ApplyToolUpdateAsync(packageId, latest, ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]✓ {packageId} updated successfully.[/]");
            if (result.RequiresRestart)
                AnsiConsole.MarkupLine("[dim]Restart the tool to use the new version.[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ Update failed:[/] {result.Output}");
            return 1;
        }
    }

    // ── jdai update --self (legacy single-tool) ──────────────────

    private static async Task<int> RunSelfUpdateAsync(bool checkOnly, bool force, CancellationToken ct)
    {
        // Detect installation
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
            AnsiConsole.MarkupLine($"[dim]Latest version:[/]  [bold]{Markup.Escape(latest)}[/]");
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
                AnsiConsole.MarkupLine($"Run [bold]jdai update --self[/] to apply.");
            }
            return 0;
        }

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync($"Updating via {strategy.Name}...", async _ =>
                await strategy.ApplyAsync(force ? null : latest, ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (result.LaunchedDetached)
        {
            AnsiConsole.MarkupLine("[yellow]Update process launched in a new window.[/]");
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

    // ── jdai install [version] [--force] ─────────────────────────

    private static async Task<int> RunInstallAsync(string[] args, CancellationToken ct)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            AnsiConsole.MarkupLine("[bold]Usage:[/] jdai install [[version]] [[--force]]");
            AnsiConsole.MarkupLine("  [dim]version[/]   Version to install (default: latest)");
            AnsiConsole.MarkupLine("  [dim]--force[/]   Force reinstall even if same version");
            return 0;
        }

        var allowedFlags = new HashSet<string>(StringComparer.Ordinal)
        {
            "--force",
            "--help",
            "-h",
        };

        var unknownFlags = args
            .Where(a => a.StartsWith('-') && !allowedFlags.Contains(a))
            .ToArray();
        var positionals = args
            .Where(a => !a.StartsWith('-'))
            .ToArray();

        if (unknownFlags.Length > 0 || positionals.Length > 1)
        {
            AnsiConsole.MarkupLine("[red]Invalid arguments.[/]");
            return 1;
        }

        var force = args.Contains("--force");
        var targetVersion = positionals.FirstOrDefault();

        var info = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync("Detecting installation...", async _ =>
                await DetectInstallationAsync(ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        AnsiConsole.MarkupLine(
            $"[dim]Platform:[/] [bold]{info.RuntimeId}[/]  " +
            $"[dim]Current:[/] [bold]{Markup.Escape(info.CurrentVersion)}[/]");

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
                AnsiConsole.MarkupLine($"[green]Already on latest version ({Markup.Escape(latest)}).[/]");
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
        AnsiConsole.MarkupLine("[bold]jdai update[/] — Check for and apply updates (all tools by default)");
        AnsiConsole.MarkupLine("[bold]jdai install[/] — Install from GitHub releases (no .NET required)");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Run [bold]jdai update --help[/] or [bold]jdai install --help[/] for details.");
        return 0;
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>Resolves a tool name (jdai, jdai-daemon, etc.) to a package ID.</summary>
    private static string ResolveToolName(string name)
    {
        if (name.Equals("jdai", StringComparison.OrdinalIgnoreCase))
            return "JD.AI";
        if (name.Equals("jdai-daemon", StringComparison.OrdinalIgnoreCase))
            return "JD.AI.Daemon";
        if (name.Equals("jdai-gateway", StringComparison.OrdinalIgnoreCase))
            return "JD.AI.Gateway";
        if (name.Equals("jdai-tui", StringComparison.OrdinalIgnoreCase))
            return "JD.AI.TUI";

        // Not a known short name — treat as a package ID directly
        return name;
    }

    private static string? ParseVersionFromOutput(string output)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            output, @"Version:\s*([^\s]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static async Task<string?> GetInstalledToolVersionCoreAsync(string packageId, CancellationToken ct)
    {
        var result = await Core.Infrastructure.ProcessExecutor.RunAsync(
            "dotnet", $"tool show -g {packageId}",
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: ct).ConfigureAwait(false);

        return result.Success ? ParseVersionFromOutput(result.StandardOutput) : null;
    }
}
