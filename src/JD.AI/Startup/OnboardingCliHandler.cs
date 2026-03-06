using JD.AI.Core.Config;
using JD.AI.Core.Mcp;
using JD.AI.Core.Providers;
using JD.AI.Rendering;
using JD.SemanticKernel.Extensions.Mcp;
using Spectre.Console;

namespace JD.AI.Startup;

internal static class OnboardingCliHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        var useGlobalDefaults = args.Any(a =>
            string.Equals(a, "--global", StringComparison.OrdinalIgnoreCase));
        var skipMcp = args.Any(a =>
            string.Equals(a, "--skip-mcp", StringComparison.OrdinalIgnoreCase));
        var skipImport = args.Any(a =>
            string.Equals(a, "--skip-import", StringComparison.OrdinalIgnoreCase));
        var providerArg = GetFlagValue(args, "--provider");
        var modelArg = GetFlagValue(args, "--model");

        using var configStore = new AtomicConfigStore();
        var projectPath = Directory.GetCurrentDirectory();
        var (registry, _, _) = ProviderOrchestrator.CreateRegistry();

        AnsiConsole.MarkupLine("[bold]JD.AI Onboarding[/]");
        AnsiConsole.MarkupLine("[dim]Detecting providers and models...[/]");

        var providers = await registry
            .DetectProvidersAsync(forceRefresh: true)
            .ConfigureAwait(false);

        var available = providers
            .Where(p => p.IsAvailable && p.Models.Count > 0)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No available providers with models were detected.[/]");
            AnsiConsole.MarkupLine("[dim]Run `/provider add <name>` inside JD.AI to configure one, then rerun `jdai onboard`.[/]");
            return 1;
        }

        var provider = ResolveProvider(available, providerArg);
        if (provider is null)
        {
            AnsiConsole.MarkupLine($"[red]Provider '{Markup.Escape(providerArg!)}' was not found or has no models.[/]");
            return 1;
        }

        var model = ResolveModel(provider.Models, modelArg);
        if (model is null)
        {
            AnsiConsole.MarkupLine($"[red]Model '{Markup.Escape(modelArg!)}' was not found in provider '{Markup.Escape(provider.Name)}'.[/]");
            return 1;
        }

        await configStore.SetDefaultProviderAsync(provider.Name, projectPath).ConfigureAwait(false);
        await configStore.SetDefaultModelAsync(model.Id, projectPath).ConfigureAwait(false);

        if (useGlobalDefaults)
        {
            await configStore.SetDefaultProviderAsync(provider.Name).ConfigureAwait(false);
            await configStore.SetDefaultModelAsync(model.Id).ConfigureAwait(false);
        }

        AnsiConsole.MarkupLine(
            $"[green]Saved startup defaults:[/] {Markup.Escape(provider.Name)} / {Markup.Escape(model.Id)}");
        AnsiConsole.MarkupLine(
            $"[dim]Scope: {(useGlobalDefaults ? "project + global" : "project")} ({Markup.Escape(projectPath)})[/]");
        AnsiConsole.MarkupLine("[dim]Tip: run `jdai wizard` (alias) anytime to switch quickly.[/]");

        // ── MCP catalog step ──────────────────────────────────────────────────
        if (!skipMcp)
            await RunMcpStepAsync().ConfigureAwait(false);

        // ── Import existing MCP configs step ──────────────────────────────────
        if (!skipImport)
            await RunMcpImportStepAsync().ConfigureAwait(false);

        return 0;
    }

    // ── MCP import step ───────────────────────────────────────────────────────

    internal static async Task RunMcpImportStepAsync(CancellationToken ct = default)
    {
        var manager = new McpManager();
        IReadOnlyList<McpServerDefinition> candidates;
        try
        {
            candidates = await manager.GetImportCandidatesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ Could not scan external configs: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        if (candidates.Count == 0)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[dim]Import from existing configs[/]").LeftJustified());
        AnsiConsole.MarkupLine(
            $"[dim]Found [/][bold]{candidates.Count}[/][dim] MCP server(s) in Claude/VS Code/Codex/Copilot configs that aren't in JD.AI yet.[/]");

        var confirm = false;
        try
        {
            confirm = AnsiConsole.Confirm("Import them into JD.AI?", defaultValue: true);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (!confirm)
        {
            AnsiConsole.MarkupLine("[dim]Import skipped. Run `jdai mcp list` anytime to review.[/]");
            return;
        }

        IReadOnlyList<McpServerDefinition> toImport;
        try
        {
            var prompt = new MultiSelectionPrompt<McpServerDefinition>()
                .Title("[bold]Select servers to import[/]")
                .PageSize(15)
                .InstructionsText("[dim](Space to toggle, Enter to confirm)[/]")
                .UseConverter(s =>
                    $"{Markup.Escape(s.DisplayName ?? s.Name)} [dim]({Markup.Escape(s.SourceProvider ?? "unknown")})[/]")
                .AddChoices(candidates);

            // Pre-select all candidates
            foreach (var server in candidates)
                prompt.Select(server);

            toImport = AnsiConsole.Prompt(prompt);
        }
        catch (InvalidOperationException)
        {
            toImport = candidates;
        }

        if (toImport.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No servers selected for import.[/]");
            return;
        }

        var imported = 0;
        foreach (var server in toImport)
        {
            try
            {
                await manager.AddOrUpdateAsync(server, ct).ConfigureAwait(false);
                AnsiConsole.MarkupLine(
                    $"  [green]✓[/] {Markup.Escape(server.DisplayName ?? server.Name)} imported");
                imported++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"  [red]✗[/] Failed to import {Markup.Escape(server.DisplayName ?? server.Name)}: {Markup.Escape(ex.Message)}");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            imported > 0
                ? $"[green]✓ {imported} server(s) imported.[/] Run `jdai mcp list` to verify."
                : "[yellow]No servers were imported. Check errors above.[/]");
    }

    // ── MCP catalog step ──────────────────────────────────────────────────────

    internal static async Task RunMcpStepAsync(CancellationToken ct = default)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[dim]MCP Servers[/]").LeftJustified());

        if (!McpCatalogPicker.ConfirmCatalogStep())
        {
            AnsiConsole.MarkupLine("[dim]MCP setup skipped. Run `jdai mcp browse` anytime to add servers.[/]");
            return;
        }

        var manager = new McpManager();
        var installed = await manager.GetAllServersAsync(ct).ConfigureAwait(false);
        var installedIds = installed
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selected = McpCatalogPicker.Pick(CuratedMcpCatalog.All, installedIds);

        // Filter out already-installed servers (don't reinstall unless changed)
        var toInstall = selected
            .Where(e => !installedIds.Contains(e.Id))
            .ToList();

        if (toInstall.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No new MCP servers selected.[/]");
            return;
        }

        var count = await McpInstaller.InstallAsync(toInstall, manager, ct).ConfigureAwait(false);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            count > 0
                ? $"[green]✓ {count} MCP server(s) installed.[/] Run `jdai mcp list` to verify."
                : "[yellow]No servers were installed. Check errors above.[/]");
    }

    private static ProviderInfo? ResolveProvider(
        IReadOnlyList<ProviderInfo> providers,
        string? providerArg)
    {
        if (!string.IsNullOrWhiteSpace(providerArg))
        {
            return providers.FirstOrDefault(p =>
                p.Name.Contains(providerArg, StringComparison.OrdinalIgnoreCase));
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<ProviderInfo>()
                .Title("[bold]Select provider[/]")
                .PageSize(12)
                .UseConverter(p => $"{Markup.Escape(p.Name)} [dim]({p.Models.Count} models)[/]")
                .AddChoices(providers));
    }

    private static ProviderModelInfo? ResolveModel(
        IReadOnlyList<ProviderModelInfo> models,
        string? modelArg)
    {
        if (!string.IsNullOrWhiteSpace(modelArg))
        {
            return models.FirstOrDefault(m =>
                m.Id.Contains(modelArg, StringComparison.OrdinalIgnoreCase)
                || m.DisplayName.Contains(modelArg, StringComparison.OrdinalIgnoreCase));
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<ProviderModelInfo>()
                .Title("[bold]Select model[/] [dim](💬=Chat 🔧=Tools 👁=Vision 📐=Embed)[/]")
                .PageSize(15)
                .UseConverter(m =>
                {
                    var badge = m.Capabilities.ToBadge();
                    return $"{badge} {Markup.Escape(m.DisplayName)} [dim]({Markup.Escape(m.Id)})[/]";
                })
                .AddChoices(models));
    }

    private static string? GetFlagValue(string[] args, string flag)
    {
        var idx = Array.FindIndex(args, a =>
            string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}
