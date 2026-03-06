using System.Text.Json;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Mcp;
using JD.AI.Rendering;
using JD.AI.Startup;
using JD.SemanticKernel.Extensions.Mcp;

namespace JD.AI.Commands;

/// <summary>
/// Handles the <c>jdai mcp</c> family of CLI subcommands.
/// Entry point: <see cref="RunAsync"/>.
/// </summary>
internal static class McpCliHandler
{
    private static readonly JsonSerializerOptions JsonOpts = JsonDefaults.Options;

    /// <summary>
    /// Dispatches the <c>jdai mcp &lt;subcommand&gt;</c> CLI.
    /// Returns the process exit code.
    /// </summary>
    public static async Task<int> RunAsync(string[] args)
    {
        var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "list";

        return sub switch
        {
            "list" => await ListAsync(args[1..]).ConfigureAwait(false),
            "add" => await AddAsync(args[1..]).ConfigureAwait(false),
            "remove" => await RemoveAsync(args[1..]).ConfigureAwait(false),
            "enable" => await SetEnabledAsync(args[1..], true).ConfigureAwait(false),
            "disable" => await SetEnabledAsync(args[1..], false).ConfigureAwait(false),
            "browse" => await BrowseAsync(args[1..]).ConfigureAwait(false),
            "--help" or "-h" or "help" => PrintHelp(),
            _ => PrintUnknown(sub),
        };
    }

    // ── list ──────────────────────────────────────────────────────────────────

    private static async Task<int> ListAsync(string[] args)
    {
        var jsonOutput = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var manager = new McpManager();
        var servers = await manager.GetAllServersAsync().ConfigureAwait(false);

        if (jsonOutput)
        {
            var list = servers.Select(s => new McpServerListEntry(
                s.Name,
                s.DisplayName,
                s.Transport.ToString().ToLowerInvariant(),
                s.Scope.ToString().ToLowerInvariant(),
                s.SourceProvider,
                s.SourcePath,
                s.IsEnabled,
                s.Command,
                s.Args,
                s.Url?.ToString()));

            Console.WriteLine(JsonSerializer.Serialize(list, JsonOpts));
            return 0;
        }

        if (servers.Count == 0)
        {
            Console.WriteLine("No MCP servers configured.");
            Console.WriteLine("Add one with: jdai mcp add <name> --transport stdio --command <cmd>");
            return 0;
        }

        Console.WriteLine($"MCP servers ({servers.Count}):");
        Console.WriteLine();

        var groups = servers
            .GroupBy(s => (s.Scope, s.SourceProvider, s.SourcePath))
            .OrderByDescending(g => ScopePriority(g.Key.Scope));

        foreach (var group in groups)
        {
            var (scope, provider, path) = group.Key;
            var label = scope switch
            {
                McpScope.Project => $"  Project MCPs ({path ?? provider})",
                McpScope.BuiltIn => "  Built-in MCPs (always available)",
                _ => $"  User MCPs ({path ?? provider})",
            };
            Console.WriteLine(label);

            foreach (var s in group)
            {
                var status = manager.GetStatus(s.Name);
                // If disabled, always show the disabled icon/state regardless of cached status.
                var icon = s.IsEnabled ? status.Icon : "○";
                var stateText = s.IsEnabled
                    ? status.State.ToString().ToLowerInvariant()
                    : "disabled";
                Console.WriteLine($"    {s.Name} · {icon} {stateText}");
            }

            Console.WriteLine();
        }

        return 0;
    }

    // ── add ───────────────────────────────────────────────────────────────────

    private static async Task<int> AddAsync(string[] args)
    {
        // jdai mcp add <name> --transport <stdio|http> [--command <cmd>] [--args ...] [<url>]
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: jdai mcp add <name> --transport <stdio|http> [options]");
            return 1;
        }

        var name = args[0];
        var transportIdx = Array.FindIndex(args, a =>
            string.Equals(a, "--transport", StringComparison.OrdinalIgnoreCase));

        if (transportIdx < 0 || transportIdx + 1 >= args.Length)
        {
            Console.Error.WriteLine("--transport <stdio|http> is required.");
            return 1;
        }

        var transportStr = args[transportIdx + 1];
        McpServerDefinition server;

        if (string.Equals(transportStr, "http", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transportStr, "https", StringComparison.OrdinalIgnoreCase))
        {
            // Positional URL: last token that looks like a URL, or --url flag
            var urlIdx = Array.FindIndex(args, a =>
                string.Equals(a, "--url", StringComparison.OrdinalIgnoreCase));

            string? url = null;
            if (urlIdx >= 0 && urlIdx + 1 < args.Length)
                url = args[urlIdx + 1];
            else
                url = args.LastOrDefault(a =>
                    a.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    a.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(url))
            {
                Console.Error.WriteLine("Usage: jdai mcp add <name> --transport http <url>");
                return 1;
            }

            server = new McpServerDefinition(
                name: name,
                displayName: name,
                transport: McpTransportType.Http,
                scope: McpScope.User,
                sourceProvider: "JD.AI",
                sourcePath: null,
                url: Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl) ? parsedUrl : null,
                command: null,
                args: null,
                env: null,
                isEnabled: true);
        }
        else if (string.Equals(transportStr, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            var cmdIdx = Array.FindIndex(args, a =>
                string.Equals(a, "--command", StringComparison.OrdinalIgnoreCase));

            if (cmdIdx < 0 || cmdIdx + 1 >= args.Length)
            {
                Console.Error.WriteLine(
                    "Usage: jdai mcp add <name> --transport stdio --command <cmd> [--args arg1 arg2...]");
                return 1;
            }

            var command = args[cmdIdx + 1];

            var argFlagIdx = Array.FindIndex(args, a =>
                string.Equals(a, "--args", StringComparison.OrdinalIgnoreCase));

            var serverArgs = argFlagIdx >= 0
                ? (IReadOnlyList<string>)args[(argFlagIdx + 1)..].ToList()
                : [];

            server = new McpServerDefinition(
                name: name,
                displayName: name,
                transport: McpTransportType.Stdio,
                scope: McpScope.User,
                sourceProvider: "JD.AI",
                sourcePath: null,
                url: null,
                command: command,
                args: serverArgs,
                env: null,
                isEnabled: true);
        }
        else
        {
            Console.Error.WriteLine($"Unknown transport '{transportStr}'. Use stdio or http.");
            return 1;
        }

        var manager = new McpManager();
        await manager.AddOrUpdateAsync(server).ConfigureAwait(false);
        Console.WriteLine($"Added MCP server '{name}' ({transportStr}).");
        return 0;
    }

    // ── remove ────────────────────────────────────────────────────────────────

    private static async Task<int> RemoveAsync(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: jdai mcp remove <name>");
            return 1;
        }

        var name = args[0];
        var manager = new McpManager();
        await manager.RemoveAsync(name).ConfigureAwait(false);
        Console.WriteLine($"Removed MCP server '{name}' (if it existed in JD.AI config).");
        return 0;
    }

    // ── enable / disable ──────────────────────────────────────────────────────

    private static async Task<int> SetEnabledAsync(string[] args, bool enabled)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine($"Usage: jdai mcp {(enabled ? "enable" : "disable")} <name>");
            return 1;
        }

        var name = args[0];
        var manager = new McpManager();
        await manager.SetEnabledAsync(name, enabled).ConfigureAwait(false);
        Console.WriteLine($"MCP server '{name}' {(enabled ? "enabled" : "disabled")}.");
        return 0;
    }

    // ── browse ────────────────────────────────────────────────────────────────

    private static async Task<int> BrowseAsync(string[] args)
    {
        // jdai mcp browse [--category <name>]
        var categoryIdx = Array.FindIndex(args, a =>
            string.Equals(a, "--category", StringComparison.OrdinalIgnoreCase));
        var categoryFilter = categoryIdx >= 0 && categoryIdx + 1 < args.Length
            ? args[categoryIdx + 1]
            : null;

        var manager = new McpManager();
        var installed = await manager.GetAllServersAsync().ConfigureAwait(false);
        var installedIds = installed
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var catalog = categoryFilter is null
            ? CuratedMcpCatalog.All
            : (IReadOnlyList<CuratedMcpEntry>)CuratedMcpCatalog.All
                .Where(e => string.Equals(e.Category, categoryFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (catalog.Count == 0)
        {
            Console.Error.WriteLine(
                categoryFilter is null
                    ? "The curated MCP catalog is empty."
                    : $"No catalog entries for category '{categoryFilter}'.");
            return 1;
        }

        var selected = McpCatalogPicker.Pick(catalog, installedIds, categoryFilter);

        var toInstall = selected
            .Where(e => !installedIds.Contains(e.Id))
            .ToList();

        if (toInstall.Count == 0)
        {
            Console.WriteLine("No new MCP servers selected.");
            return 0;
        }

        var count = await McpInstaller.InstallAsync(toInstall, manager).ConfigureAwait(false);
        Console.WriteLine(count > 0
            ? $"{count} MCP server(s) installed. Run 'jdai mcp list' to verify."
            : "No servers were installed. Check errors above.");

        return count > 0 ? 0 : 1;
    }

    // ── help ──────────────────────────────────────────────────────────────────

    private static int PrintHelp()
    {
        Console.WriteLine("""
            jdai mcp — Manage MCP (Model Context Protocol) servers

            Usage: jdai mcp <subcommand> [options]

            Subcommands:
              list [--json]                        List all configured MCP servers
              browse [--category <name>]           Browse and install from curated catalog
              add <name> --transport stdio         Add a stdio MCP server
                         --command <cmd>
                         [--args arg1 arg2...]
              add <name> --transport http <url>    Add an HTTP MCP server
              remove <name>                        Remove a JD.AI-managed server
              enable <name>                        Enable a JD.AI-managed server
              disable <name>                       Disable a JD.AI-managed server

            Examples:
              jdai mcp list
              jdai mcp list --json
              jdai mcp browse
              jdai mcp browse --category "Source Control"
              jdai mcp add notion --transport http https://mcp.notion.com/mcp
              jdai mcp add azure-devops --transport stdio --command npx --args -y @azure-devops/mcp Quiktrip
              jdai mcp disable azure-devops
              jdai mcp remove azure-devops
            """);
        return 0;
    }

    private static int PrintUnknown(string sub)
    {
        Console.Error.WriteLine($"Unknown mcp subcommand '{sub}'. Run 'jdai mcp --help' for usage.");
        return 1;
    }

    // ── JSON output DTO ───────────────────────────────────────────────────────

    private sealed record McpServerListEntry(
        string Name,
        string DisplayName,
        string Transport,
        string Scope,
        string SourceProvider,
        string? SourcePath,
        bool IsEnabled,
        string? Command,
        IReadOnlyList<string>? Args,
        string? Url);

    private static int ScopePriority(McpScope scope) => scope switch
    {
        McpScope.BuiltIn => 0,
        McpScope.User => 1,
        McpScope.Project => 2,
        _ => -1,
    };
}
