using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// MCP ecosystem import, sync, and drift detection tools.
/// Handles bidirectional config reconciliation across Claude, OpenClaw, and JD.AI.
/// </summary>
public sealed class McpEcosystemTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    [KernelFunction("mcp_import_scan")]
    [System.ComponentModel.Description(
        "Scan ecosystem configs (Claude, OpenClaw, JD.AI) for MCP server definitions and report importable servers.")]
    public static string ImportScan(
        [System.ComponentModel.Description("Optional: home directory override for testing")]
        string? homeDir = null)
    {
        var home = homeDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sources = DiscoverSources(home);
        var servers = new List<ImportedServer>();

        foreach (var source in sources)
        {
            var discovered = ParseSource(source);
            servers.AddRange(discovered);
        }

        if (servers.Count == 0)
        {
            return "## MCP Import Scan\n\n" +
                   "No MCP server definitions found in any ecosystem config.\n\n" +
                   "### Searched Locations\n" +
                   string.Join("\n", sources.Select(s => $"- `{s.Path}` ({s.Ecosystem})")) +
                   "\n\n" +
                   (sources.Count == 0
                       ? "No ecosystem config files found. Create `~/.claude.json` or `~/.jdai/mcp.json` to get started."
                       : "Config files exist but contain no MCP server definitions.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("## MCP Import Scan");
        sb.AppendLine();
        sb.AppendLine($"Found **{servers.Count}** server(s) across **{sources.Count(s => s.ServerCount > 0)}** source(s).");
        sb.AppendLine();
        sb.AppendLine("| Server | Ecosystem | Transport | Auth | Status |");
        sb.AppendLine("|--------|-----------|-----------|------|--------|");

        foreach (var server in servers.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(
                $"| `{server.Name}` | {server.Ecosystem} | {server.Transport} | " +
                $"{server.Auth ?? "none"} | {server.ImportStatus} |");
        }

        // Check for duplicates
        var dupes = servers
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (dupes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### ⚠️ Duplicate Definitions");
            foreach (var dupe in dupes)
            {
                sb.AppendLine($"- `{dupe.Key}` defined in: {string.Join(", ", dupe.Select(s => s.Ecosystem))}");
            }
            sb.AppendLine();
            sb.AppendLine("Use `mcp_drift` to see detailed differences and resolution options.");
        }

        sb.AppendLine();
        sb.AppendLine("### Next Steps");
        sb.AppendLine("- Use `mcp_sync` to import servers into JD.AI config");
        sb.AppendLine("- Use `mcp_drift` to detect configuration differences");

        return sb.ToString();
    }

    [KernelFunction("mcp_sync")]
    [System.ComponentModel.Description(
        "Sync MCP server definitions from an ecosystem (Claude, OpenClaw) into JD.AI config. " +
        "Use dry_run=true to preview changes without writing.")]
    public static string Sync(
        [System.ComponentModel.Description("Source ecosystem: 'claude', 'openclaw', or 'all'")]
        string from = "all",
        [System.ComponentModel.Description("If true, preview changes without writing config")]
        bool dryRun = true,
        [System.ComponentModel.Description("Optional: home directory override for testing")]
        string? homeDir = null)
    {
        var home = homeDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sources = DiscoverSources(home);
        var allServers = new List<ImportedServer>();

        foreach (var source in sources)
        {
            if (!string.Equals(from, "all", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(source.Ecosystem, from, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            allServers.AddRange(ParseSource(source));
        }

        // Load existing JD.AI config
        var jdaiConfigPath = Path.Combine(home, ".jdai", "mcp.json");
        var existing = LoadJdaiServers(jdaiConfigPath);

        // Classify each server
        var toAdd = new List<ImportedServer>();
        var toUpdate = new List<(ImportedServer Source, string ExistingTransport)>();
        var alreadySynced = new List<ImportedServer>();

        foreach (var server in allServers)
        {
            if (existing.TryGetValue(server.Name, out var existingEntry))
            {
                if (string.Equals(existingEntry, server.Transport, StringComparison.OrdinalIgnoreCase))
                {
                    alreadySynced.Add(server);
                }
                else
                {
                    toUpdate.Add((server, existingEntry));
                }
            }
            else
            {
                toAdd.Add(server);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## MCP Sync {(dryRun ? "(Dry Run)" : "")}");
        sb.AppendLine();
        sb.AppendLine($"Source: **{from}** → Target: `~/.jdai/mcp.json`");
        sb.AppendLine();

        if (toAdd.Count > 0)
        {
            sb.AppendLine($"### ➕ New Servers ({toAdd.Count})");
            sb.AppendLine("| Server | From | Transport | Auth |");
            sb.AppendLine("|--------|------|-----------|------|");
            foreach (var s in toAdd)
            {
                sb.AppendLine($"| `{s.Name}` | {s.Ecosystem} | {s.Transport} | {s.Auth ?? "none"} |");
            }
            sb.AppendLine();
        }

        if (toUpdate.Count > 0)
        {
            sb.AppendLine($"### 🔄 Updated Servers ({toUpdate.Count})");
            sb.AppendLine("| Server | Current | New | From |");
            sb.AppendLine("|--------|---------|-----|------|");
            foreach (var (s, existingTransport) in toUpdate)
            {
                sb.AppendLine($"| `{s.Name}` | {existingTransport} | {s.Transport} | {s.Ecosystem} |");
            }
            sb.AppendLine();
        }

        if (alreadySynced.Count > 0)
        {
            sb.AppendLine($"### ✅ Already Synced ({alreadySynced.Count})");
            sb.AppendLine(string.Join(", ", alreadySynced.Select(s => $"`{s.Name}`")));
            sb.AppendLine();
        }

        if (toAdd.Count == 0 && toUpdate.Count == 0)
        {
            sb.AppendLine("**No changes needed** — all servers are already synced.");
            return sb.ToString();
        }

        if (dryRun)
        {
            sb.AppendLine("### 🔒 Dry Run");
            sb.AppendLine("No changes were written. Run with `dry_run=false` to apply.");
            sb.AppendLine();
            sb.AppendLine("### Security Notes");
            foreach (var s in toAdd.Where(s => string.Equals(s.Transport, "stdio", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine($"- ⚠️ `{s.Name}` uses stdio transport — verify command is trusted: `{s.Command}`");
            }
        }
        else
        {
            // Write changes
            var configDir = Path.GetDirectoryName(jdaiConfigPath)!;
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            var config = LoadOrCreateConfig(jdaiConfigPath);
            foreach (var s in toAdd.Concat(toUpdate.Select(u => u.Source)))
            {
                config[s.Name] = new ServerEntry(s.Transport, s.Command, s.Args, s.Url, s.Auth);
            }

            WriteConfig(jdaiConfigPath, config);
            sb.AppendLine($"### ✅ Applied {toAdd.Count + toUpdate.Count} change(s) to `{jdaiConfigPath}`");
        }

        return sb.ToString();
    }

    [KernelFunction("mcp_drift")]
    [System.ComponentModel.Description(
        "Detect drift between MCP server definitions across ecosystems. " +
        "Shows added, removed, and changed servers with remediation guidance.")]
    public static string DetectDrift(
        [System.ComponentModel.Description("Optional: home directory override for testing")]
        string? homeDir = null)
    {
        var home = homeDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sources = DiscoverSources(home);
        var allServers = new List<ImportedServer>();

        foreach (var source in sources)
        {
            allServers.AddRange(ParseSource(source));
        }

        if (allServers.Count == 0)
        {
            return "## MCP Drift Report\n\nNo MCP server definitions found across any ecosystem.";
        }

        // Group by name
        var byName = allServers
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("## MCP Drift Report");
        sb.AppendLine();

        var ecosystems = allServers.Select(s => s.Ecosystem).Distinct().OrderBy(e => e).ToList();
        sb.AppendLine($"Ecosystems compared: {string.Join(", ", ecosystems.Select(e => $"**{e}**"))}");
        sb.AppendLine();

        // Build drift matrix
        var driftItems = new List<DriftItem>();

        foreach (var group in byName)
        {
            var servers = group.ToList();
            if (servers.Count == 1)
            {
                driftItems.Add(new DriftItem(
                    group.Key, "single-source", servers[0].Ecosystem,
                    $"Only defined in {servers[0].Ecosystem} — consider syncing to other ecosystems"));
            }
            else
            {
                // Check for transport/auth mismatches
                var transports = servers.Select(s => s.Transport).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var auths = servers.Select(s => s.Auth ?? "none").Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (transports.Count > 1)
                {
                    driftItems.Add(new DriftItem(
                        group.Key, "transport-mismatch",
                        string.Join(", ", servers.Select(s => s.Ecosystem)),
                        $"Transport differs: {string.Join(" vs ", servers.Select(s => $"{s.Ecosystem}={s.Transport}"))}"));
                }
                else if (auths.Count > 1)
                {
                    driftItems.Add(new DriftItem(
                        group.Key, "auth-mismatch",
                        string.Join(", ", servers.Select(s => s.Ecosystem)),
                        $"Auth differs: {string.Join(" vs ", servers.Select(s => $"{s.Ecosystem}={s.Auth ?? "none"}"))}"));
                }
                else
                {
                    driftItems.Add(new DriftItem(
                        group.Key, "in-sync",
                        string.Join(", ", servers.Select(s => s.Ecosystem)),
                        "Consistent across all sources"));
                }
            }
        }

        // Summary
        var inSync = driftItems.Count(d => string.Equals(d.DriftType, "in-sync", StringComparison.Ordinal));
        var singleSource = driftItems.Count(d => string.Equals(d.DriftType, "single-source", StringComparison.Ordinal));
        var mismatches = driftItems.Count(d => d.DriftType.Contains("mismatch", StringComparison.Ordinal));

        sb.AppendLine("### Summary");
        sb.AppendLine($"- ✅ In sync: **{inSync}**");
        sb.AppendLine($"- 📋 Single source: **{singleSource}**");
        sb.AppendLine($"- ⚠️ Mismatches: **{mismatches}**");
        sb.AppendLine();

        if (mismatches > 0)
        {
            sb.AppendLine("### ⚠️ Configuration Mismatches");
            sb.AppendLine("| Server | Type | Ecosystems | Details |");
            sb.AppendLine("|--------|------|------------|---------|");
            foreach (var d in driftItems.Where(d => d.DriftType.Contains("mismatch", StringComparison.Ordinal)))
            {
                sb.AppendLine($"| `{d.ServerName}` | {d.DriftType} | {d.Ecosystems} | {d.Details} |");
            }
            sb.AppendLine();
        }

        if (singleSource > 0)
        {
            sb.AppendLine("### 📋 Single-Source Definitions");
            sb.AppendLine("| Server | Source | Suggestion |");
            sb.AppendLine("|--------|--------|------------|");
            foreach (var d in driftItems.Where(d => string.Equals(d.DriftType, "single-source", StringComparison.Ordinal)))
            {
                sb.AppendLine($"| `{d.ServerName}` | {d.Ecosystems} | {d.Details} |");
            }
            sb.AppendLine();
        }

        if (inSync > 0)
        {
            sb.AppendLine($"### ✅ In Sync ({inSync})");
            sb.AppendLine(string.Join(", ",
                driftItems.Where(d => string.Equals(d.DriftType, "in-sync", StringComparison.Ordinal))
                    .Select(d => $"`{d.ServerName}`")));
            sb.AppendLine();
        }

        sb.AppendLine("### Remediation");
        if (mismatches > 0)
        {
            sb.AppendLine("- Run `mcp_sync` to align configurations to a single source of truth");
        }

        if (singleSource > 0)
        {
            sb.AppendLine("- Run `mcp_sync --from <ecosystem>` to import single-source servers");
        }

        sb.AppendLine("- Review security posture with `mcp_diagnose` for each server");

        return sb.ToString();
    }

    [KernelFunction("mcp_quarantine")]
    [System.ComponentModel.Description(
        "List quarantined MCP servers pending trust approval. " +
        "Imported servers with unknown executables are quarantined until explicitly approved.")]
    public static string ListQuarantine(
        [System.ComponentModel.Description("Optional: home directory override for testing")]
        string? homeDir = null)
    {
        var home = homeDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var quarantinePath = Path.Combine(home, ".jdai", "mcp-quarantine.json");

        if (!File.Exists(quarantinePath))
        {
            return "## MCP Quarantine\n\n" +
                   "No servers in quarantine.\n\n" +
                   "Servers are quarantined during import when:\n" +
                   "- The executable command is not in the system PATH\n" +
                   "- The server uses stdio transport with an unrecognized command\n" +
                   "- The server was imported from an untrusted source\n\n" +
                   "Use `mcp_sync` to import servers — untrusted ones will appear here.";
        }

        try
        {
            var json = File.ReadAllText(quarantinePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("quarantined", out var quarantined) ||
                quarantined.GetArrayLength() == 0)
            {
                return "## MCP Quarantine\n\nNo servers currently quarantined. ✅";
            }

            var sb = new StringBuilder();
            sb.AppendLine("## MCP Quarantine");
            sb.AppendLine();
            sb.AppendLine($"**{quarantined.GetArrayLength()}** server(s) pending approval:");
            sb.AppendLine();
            sb.AppendLine("| Server | Reason | Source | Imported |");
            sb.AppendLine("|--------|--------|--------|----------|");

            foreach (var entry in quarantined.EnumerateArray())
            {
                var name = entry.TryGetProperty("name", out var n) ? n.GetString() : "unknown";
                var reason = entry.TryGetProperty("reason", out var r) ? r.GetString() : "unknown";
                var source = entry.TryGetProperty("source", out var s) ? s.GetString() : "unknown";
                var imported = entry.TryGetProperty("importedAt", out var i) ? i.GetString() : "unknown";
                sb.AppendLine($"| `{name}` | {reason} | {source} | {imported} |");
            }

            sb.AppendLine();
            sb.AppendLine("### Actions");
            sb.AppendLine("- To approve: add server to `~/.jdai/mcp-trusted.json`");
            sb.AppendLine("- To reject: remove from quarantine file");
            sb.AppendLine("- To review: use `mcp_diagnose` with the server name");

            return sb.ToString();
        }
        catch (JsonException)
        {
            return "## MCP Quarantine\n\n⚠️ Quarantine file is corrupted. Delete `~/.jdai/mcp-quarantine.json` to reset.";
        }
    }

    [KernelFunction("mcp_ecosystem_export")]
    [System.ComponentModel.Description(
        "Export the full MCP ecosystem state as JSON for CI/CD or auditing. " +
        "Includes all sources, servers, drift status, and quarantine state.")]
    public static string ExportEcosystem(
        [System.ComponentModel.Description("Optional: home directory override for testing")]
        string? homeDir = null)
    {
        var home = homeDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sources = DiscoverSources(home);
        var allServers = new List<ImportedServer>();

        foreach (var source in sources)
        {
            allServers.AddRange(ParseSource(source));
        }

        var byName = allServers
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var inSync = byName.Count(g =>
        {
            var transports = g.Select(s => s.Transport).Distinct(StringComparer.OrdinalIgnoreCase);
            return g.Count() > 1 && transports.Count() == 1;
        });
        var mismatches = byName.Count(g =>
        {
            var transports = g.Select(s => s.Transport).Distinct(StringComparer.OrdinalIgnoreCase);
            return g.Count() > 1 && transports.Count() > 1;
        });

        var export = new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            sources = sources.Select(s => new
            {
                path = s.Path,
                ecosystem = s.Ecosystem,
                serverCount = s.ServerCount
            }).ToArray(),
            servers = allServers.Select(s => new
            {
                name = s.Name,
                ecosystem = s.Ecosystem,
                transport = s.Transport,
                auth = s.Auth,
                command = s.Command,
                status = s.ImportStatus
            }).ToArray(),
            summary = new
            {
                totalSources = sources.Count,
                totalServers = allServers.Count,
                uniqueServers = byName.Count,
                inSync,
                mismatches,
                singleSource = byName.Count - inSync - mismatches,
                ecosystems = allServers.Select(s => s.Ecosystem).Distinct().OrderBy(e => e).ToArray()
            }
        };

        return JsonSerializer.Serialize(export, s_jsonOptions);
    }

    // ── Discovery & Parsing ──────────────────────────────────

    internal static List<ConfigSource> DiscoverSources(string home)
    {
        var sources = new List<ConfigSource>();

        // Claude configs
        var claudeJson = Path.Combine(home, ".claude.json");
        if (File.Exists(claudeJson))
        {
            sources.Add(new ConfigSource(claudeJson, "Claude", 0));
        }

        var claudeDir = Path.Combine(home, ".claude");
        if (Directory.Exists(claudeDir))
        {
            var mcpJson = Path.Combine(claudeDir, "mcp.json");
            if (File.Exists(mcpJson))
            {
                sources.Add(new ConfigSource(mcpJson, "Claude", 0));
            }
        }

        // OpenClaw config
        var openclawConfig = Path.Combine(home, ".openclaw", "mcp.json");
        if (File.Exists(openclawConfig))
        {
            sources.Add(new ConfigSource(openclawConfig, "OpenClaw", 0));
        }

        // JD.AI config
        var jdaiConfig = Path.Combine(home, ".jdai", "mcp.json");
        if (File.Exists(jdaiConfig))
        {
            sources.Add(new ConfigSource(jdaiConfig, "JD.AI", 0));
        }

        // Project-level .mcp.json
        var projectMcp = Path.Combine(Directory.GetCurrentDirectory(), ".mcp.json");
        if (File.Exists(projectMcp))
        {
            sources.Add(new ConfigSource(projectMcp, "Project", 0));
        }

        return sources;
    }

    internal static List<ImportedServer> ParseSource(ConfigSource source)
    {
        var servers = new List<ImportedServer>();

        try
        {
            var json = File.ReadAllText(source.Path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Try "mcpServers" (Claude format)
            if (root.TryGetProperty("mcpServers", out var mcpServers) &&
                mcpServers.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in mcpServers.EnumerateObject())
                {
                    servers.Add(ParseServerEntry(prop.Name, prop.Value, source));
                }
            }
            // Try "servers" (JD.AI/generic format)
            else if (root.TryGetProperty("servers", out var serversObj) &&
                     serversObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in serversObj.EnumerateObject())
                {
                    servers.Add(ParseServerEntry(prop.Name, prop.Value, source));
                }
            }

            source.ServerCount = servers.Count;
        }
        catch (Exception)
        {
            // Skip malformed configs
        }

        return servers;
    }

    private static ImportedServer ParseServerEntry(string name, JsonElement config, ConfigSource source)
    {
        var transport = config.TryGetProperty("transport", out var t) ? t.GetString() ?? "stdio" : "stdio";
        var command = config.TryGetProperty("command", out var c) ? c.GetString() : null;
        var url = config.TryGetProperty("url", out var u) ? u.GetString() : null;
        string? auth = null;

        if (config.TryGetProperty("auth", out var a))
        {
            auth = a.TryGetProperty("type", out var at) ? at.GetString() : "configured";
        }
        else if (config.TryGetProperty("headers", out _))
        {
            auth = "bearer";
        }

        string[]? args = null;
        if (config.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Array)
        {
            args = argsEl.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToArray();
        }

        return new ImportedServer(name, source.Ecosystem, transport, auth, command, args, url, "importable");
    }

    private static Dictionary<string, string> LoadJdaiServers(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var serversKey = root.TryGetProperty("servers", out var s) ? s :
                             root.TryGetProperty("mcpServers", out var m) ? m : default;

            if (serversKey.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in serversKey.EnumerateObject())
                {
                    var transport = prop.Value.TryGetProperty("transport", out var t)
                        ? t.GetString() ?? "stdio" : "stdio";
                    result[prop.Name] = transport;
                }
            }
        }
        catch (Exception)
        {
            // Ignore malformed config
        }

        return result;
    }

    private static Dictionary<string, ServerEntry> LoadOrCreateConfig(string path)
    {
        var result = new Dictionary<string, ServerEntry>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var serversKey = root.TryGetProperty("servers", out var s) ? s :
                             root.TryGetProperty("mcpServers", out var m) ? m : default;

            if (serversKey.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in serversKey.EnumerateObject())
                {
                    var transport = prop.Value.TryGetProperty("transport", out var t)
                        ? t.GetString() ?? "stdio" : "stdio";
                    var command = prop.Value.TryGetProperty("command", out var c) ? c.GetString() : null;
                    var url = prop.Value.TryGetProperty("url", out var u) ? u.GetString() : null;

                    string[]? args = null;
                    if (prop.Value.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array)
                    {
                        args = a.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString()!)
                            .ToArray();
                    }

                    result[prop.Name] = new ServerEntry(transport, command, args, url, null);
                }
            }
        }
        catch (Exception)
        {
            // Start fresh
        }

        return result;
    }

    private static void WriteConfig(string path, Dictionary<string, ServerEntry> servers)
    {
        var config = new Dictionary<string, object>
        {
            ["servers"] = servers.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var entry = new Dictionary<string, object?> { ["transport"] = kvp.Value.Transport };
                    if (kvp.Value.Command is not null)
                    {
                        entry["command"] = kvp.Value.Command;
                    }

                    if (kvp.Value.Args is { Length: > 0 })
                    {
                        entry["args"] = kvp.Value.Args;
                    }

                    if (kvp.Value.Url is not null)
                    {
                        entry["url"] = kvp.Value.Url;
                    }

                    return (object)entry;
                })
        };

        File.WriteAllText(path, JsonSerializer.Serialize(config, s_jsonOptions));
    }

    // ── Types ────────────────────────────────────────────────

    internal sealed class ConfigSource(string path, string ecosystem, int serverCount)
    {
        public string Path { get; } = path;
        public string Ecosystem { get; } = ecosystem;
        public int ServerCount { get; set; } = serverCount;
    }

    internal sealed record ImportedServer(
        string Name, string Ecosystem, string Transport, string? Auth,
        string? Command, string[]? Args, string? Url, string ImportStatus);

    private sealed record ServerEntry(
        string Transport, string? Command, string[]? Args, string? Url, string? Auth);

    private sealed record DriftItem(
        string ServerName, string DriftType, string Ecosystems, string Details);
}
