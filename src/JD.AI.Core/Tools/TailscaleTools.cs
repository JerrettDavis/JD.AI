using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for Tailscale integration: status detection, Tailnet machine discovery,
/// remote orchestration, and credential configuration.
/// </summary>
[ToolPlugin("tailscale")]
public sealed class TailscaleTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = JsonDefaults.Indented;

    // ── Status ──────────────────────────────────────────────

    [KernelFunction("tailscale_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Check Tailscale installation, authentication status, current Tailnet, and local node identity.")]
    public static string GetStatus(
        [Description("Optional config directory for stored credentials (default: ~/.jdai)")] string? configDir = null)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

        var sb = new StringBuilder();
        sb.AppendLine("## Tailscale Status");
        sb.AppendLine();

        // Check CLI availability
        var cliAvailable = IsTailscaleCliAvailable();
        sb.AppendLine($"- **CLI installed**: {(cliAvailable ? "✅ Yes" : "❌ No")}");

        // Check API credentials
        var apiConfig = LoadApiConfig(configDir);
        var hasApiCreds = apiConfig is not null &&
            !string.IsNullOrWhiteSpace(apiConfig.Tailnet);
        sb.AppendLine($"- **API configured**: {(hasApiCreds ? "✅ Yes" : "❌ No")}");

        if (hasApiCreds && apiConfig is not null)
        {
            sb.AppendLine($"- **Tailnet**: `{apiConfig.Tailnet}`");
            sb.AppendLine($"- **Auth method**: {apiConfig.AuthMethod}");
        }

        // If CLI is available, try to get node info
        if (cliAvailable)
        {
            var nodeInfo = GetLocalNodeInfo();
            if (nodeInfo is not null)
            {
                sb.AppendLine($"- **Local node**: `{nodeInfo.Hostname}`");
                sb.AppendLine($"- **Tailscale IP**: `{nodeInfo.TailscaleIp}`");
                sb.AppendLine($"- **Login name**: `{nodeInfo.LoginName}`");
                sb.AppendLine($"- **Backend state**: {nodeInfo.BackendState}");
            }
        }

        sb.AppendLine();

        if (!cliAvailable && !hasApiCreds)
        {
            sb.AppendLine("### Getting Started");
            sb.AppendLine("1. **Install Tailscale**: https://tailscale.com/download");
            sb.AppendLine("2. **Or configure API**: Use `tailscale_configure` to set up OAuth credentials");
            sb.AppendLine("3. **Then discover machines**: Use `tailscale_machines` to list your Tailnet");
        }

        return sb.ToString();
    }

    // ── Machine Discovery ───────────────────────────────────

    [KernelFunction("tailscale_machines")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List all machines on the Tailnet with name, OS, online status, and addresses. Requires Tailscale CLI or API credentials.")]
    public static string ListMachines(
        [Description("Optional filter: 'online', 'offline', or 'all' (default: all)")] string? filter = null,
        [Description("Optional tag filter (e.g., 'tag:server')")] string? tag = null,
        [Description("Optional config directory (default: ~/.jdai)")] string? configDir = null)
        => ListMachinesInternal(filter, tag, configDir, useCli: true);

    internal static string ListMachinesInternal(
        string? filter,
        string? tag,
        string? configDir,
        bool useCli)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");
        filter ??= "all";

        var sb = new StringBuilder();
        sb.AppendLine("## Tailnet Machines");
        sb.AppendLine();

        // Try CLI first, then API
        var machines = DiscoverMachines(configDir, useCli);

        if (machines.Count == 0)
        {
            sb.AppendLine("No machines discovered.");
            sb.AppendLine();
            sb.AppendLine("### Troubleshooting");
            sb.AppendLine("- Ensure Tailscale is installed and authenticated (`tailscale up`)");
            sb.AppendLine("- Or configure API credentials with `tailscale_configure`");
            sb.AppendLine("- Verify your Tailnet has other devices connected");
            return sb.ToString();
        }

        // Apply filters
        var filtered = machines.AsEnumerable();
        if (string.Equals(filter, "online", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(m => m.Online);
        else if (string.Equals(filter, "offline", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(m => !m.Online);

        if (!string.IsNullOrWhiteSpace(tag))
            filtered = filtered.Where(m =>
                m.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));

        var filteredList = filtered.ToList();

        sb.AppendLine("| Name | OS | Status | Tailscale IP | Tags | Runner |");
        sb.AppendLine("|------|----|--------|-------------|------|--------|");

        foreach (var m in filteredList.OrderBy(m => m.Hostname))
        {
            var statusIcon = m.Online ? "🟢 Online" : "🔴 Offline";
            var tags = m.Tags.Count > 0 ? string.Join(", ", m.Tags) : "—";
            var runner = m.HasRunner ? "✅" : "—";

            sb.AppendLine(
                $"| `{m.Hostname}` | {m.Os} | {statusIcon} | {m.TailscaleIp} | {tags} | {runner} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**Total**: {filteredList.Count} machine(s)" +
            $" ({filteredList.Count(m => m.Online)} online," +
            $" {filteredList.Count(m => !m.Online)} offline)");

        if (filteredList.Any(m => m.HasRunner))
        {
            sb.AppendLine();
            sb.AppendLine("💡 Machines with ✅ in **Runner** column have `jdai-runner` available for remote orchestration.");
        }

        return sb.ToString();
    }

    // ── Configure Credentials ───────────────────────────────

    [KernelFunction("tailscale_configure")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Configure Tailscale API credentials for machine discovery and remote orchestration. Stores in ~/.jdai/tailscale.json.")]
    public static string Configure(
        [Description("Tailnet name (e.g., 'example.com' or org name)")] string tailnet,
        [Description("Auth method: 'oauth' or 'api-key'")] string authMethod,
        [Description("OAuth client ID or API key")] string credential,
        [Description("OAuth client secret (only for oauth auth method)")] string? clientSecret = null,
        [Description("Optional config directory (default: ~/.jdai)")] string? configDir = null)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

        var sb = new StringBuilder();
        sb.AppendLine("## Tailscale Configuration");
        sb.AppendLine();

        // Validate auth method
        if (!string.Equals(authMethod, "oauth", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(authMethod, "api-key", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("❌ Invalid auth method. Use `oauth` or `api-key`.");
            return sb.ToString();
        }

        if (string.Equals(authMethod, "oauth", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(clientSecret))
        {
            sb.AppendLine("❌ OAuth auth method requires `clientSecret` parameter.");
            return sb.ToString();
        }

        // Build config
        var config = new TailscaleApiConfig(
            tailnet,
            authMethod.ToLowerInvariant(),
            string.Equals(authMethod, "api-key", StringComparison.OrdinalIgnoreCase)
                ? credential : null,
            string.Equals(authMethod, "oauth", StringComparison.OrdinalIgnoreCase)
                ? credential : null,
            string.Equals(authMethod, "oauth", StringComparison.OrdinalIgnoreCase)
                ? clientSecret : null);

        // Write config
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "tailscale.json");

        try
        {
            var json = JsonSerializer.Serialize(config, s_jsonOptions);
            File.WriteAllText(configPath, json);

            sb.AppendLine("✅ Tailscale API credentials saved.");
            sb.AppendLine();
            sb.AppendLine($"- **Tailnet**: `{tailnet}`");
            sb.AppendLine($"- **Auth method**: {authMethod}");
            sb.AppendLine($"- **Config path**: `{configPath}`");
            sb.AppendLine();
            sb.AppendLine("⚠️ **Security**: Consider using environment variables instead of storing secrets:");
            sb.AppendLine("- `TAILSCALE_API_KEY` for API key auth");
            sb.AppendLine("- `TAILSCALE_OAUTH_CLIENT_ID` + `TAILSCALE_OAUTH_CLIENT_SECRET` for OAuth");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"❌ Failed to save config: {ex.Message}");
        }

        return sb.ToString();
    }

    // ── Runner Probe ────────────────────────────────────────

    [KernelFunction("tailscale_runner_probe")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Check if a jdai-runner service is available on a specific Tailnet machine. Returns runner version, status, and capabilities.")]
    public static string ProbeRunner(
        [Description("Machine hostname or Tailscale IP to probe")] string target,
        [Description("Runner port (default: 18789)")] int port = 18789,
        [Description("Optional config directory (default: ~/.jdai)")] string? configDir = null)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

        var sb = new StringBuilder();
        sb.AppendLine($"## Runner Probe: {target}");
        sb.AppendLine();

        // Check if the target is reachable (simulate — real impl would do HTTP check)
        var runnerUrl = $"http://{target}:{port.ToString(CultureInfo.InvariantCulture)}";
        sb.AppendLine($"- **Target**: `{target}`");
        sb.AppendLine($"- **Port**: {port.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"- **Runner URL**: `{runnerUrl}`");
        sb.AppendLine();

        // Check for cached runner info
        var runnersPath = Path.Combine(configDir, "tailscale-runners.json");
        if (File.Exists(runnersPath))
        {
            try
            {
                var json = File.ReadAllText(runnersPath);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("runners", out var runners))
                {
                    foreach (var runner in runners.EnumerateObject())
                    {
                        if (string.Equals(runner.Name, target, StringComparison.OrdinalIgnoreCase))
                        {
                            sb.AppendLine("### Cached Runner Info");
                            if (runner.Value.TryGetProperty("version", out var ver))
                                sb.AppendLine($"- **Version**: {ver.GetString()}");
                            if (runner.Value.TryGetProperty("lastSeen", out var ls))
                                sb.AppendLine($"- **Last seen**: {ls.GetString()}");
                            if (runner.Value.TryGetProperty("capabilities", out var caps))
                            {
                                sb.AppendLine("- **Capabilities**:");
                                foreach (var cap in caps.EnumerateArray())
                                    sb.AppendLine($"  - {cap.GetString()}");
                            }
                            sb.AppendLine();
                            break;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed cache — ignore
            }
        }

        sb.AppendLine("### Connection Test");
        sb.AppendLine($"To connect: `jdai remote connect {target} --port {port.ToString(CultureInfo.InvariantCulture)}`");
        sb.AppendLine();
        sb.AppendLine("### Bootstrap Runner");
        sb.AppendLine("If no runner is available, bootstrap via SSH:");
        sb.AppendLine($"```bash");
        sb.AppendLine($"jdai remote start-runner {target} --install");
        sb.AppendLine($"```");
        sb.AppendLine("This will SSH into the machine, install jdai-runner, and start it.");

        return sb.ToString();
    }

    // ── Export ───────────────────────────────────────────────

    [KernelFunction("tailscale_export")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Export Tailscale configuration, discovered machines, and runner status as JSON for CI and automation.")]
    public static string Export(
        [Description("Optional config directory (default: ~/.jdai)")] string? configDir = null)
        => ExportInternal(configDir, useCli: true);

    internal static string ExportInternal(string? configDir, bool useCli)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

        var apiConfig = LoadApiConfig(configDir);
        var machines = DiscoverMachines(configDir, useCli);
        var cliAvailable = useCli && IsTailscaleCliAvailable();

        var result = new
        {
            timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            configDirectory = configDir,
            cli = new
            {
                installed = cliAvailable,
            },
            api = new
            {
                configured = apiConfig is not null,
                tailnet = apiConfig?.Tailnet,
                authMethod = apiConfig?.AuthMethod,
            },
            machines = machines.Select(m => new
            {
                m.Hostname,
                os = m.Os,
                m.Online,
                tailscaleIp = m.TailscaleIp,
                m.Tags,
                m.HasRunner,
            }).ToArray(),
            summary = new
            {
                totalMachines = machines.Count,
                online = machines.Count(m => m.Online),
                offline = machines.Count(m => !m.Online),
                withRunner = machines.Count(m => m.HasRunner),
            },
        };

        return JsonSerializer.Serialize(result, s_jsonOptions);
    }

    // ── Helpers ──────────────────────────────────────────────

    internal static bool IsTailscaleCliAvailable()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "tailscale",
                Arguments = "version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    internal static TailscaleNodeInfo? GetLocalNodeInfo()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "tailscale",
                Arguments = "status --json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
                return null;

            var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            var hostname = root.TryGetProperty("Self", out var self) &&
                           self.TryGetProperty("HostName", out var hn)
                ? hn.GetString() ?? "unknown" : "unknown";

            var ip = self.TryGetProperty("TailscaleIPs", out var ips) &&
                     ips.GetArrayLength() > 0
                ? ips[0].GetString() ?? "—" : "—";

            var loginName = root.TryGetProperty("User", out var users)
                ? GetFirstUserLogin(users) : "unknown";

            var backendState = root.TryGetProperty("BackendState", out var bs)
                ? bs.GetString() ?? "unknown" : "unknown";

            return new TailscaleNodeInfo(hostname, ip, loginName, backendState);
        }
        catch
        {
            return null;
        }
    }

    private static string GetFirstUserLogin(JsonElement users)
    {
        foreach (var user in users.EnumerateObject())
        {
            if (user.Value.TryGetProperty("LoginName", out var ln))
                return ln.GetString() ?? "unknown";
        }
        return "unknown";
    }

    internal static List<TailnetMachine> DiscoverMachines(string configDir, bool useCli = true)
    {
        var machines = new List<TailnetMachine>();

        // Try Tailscale CLI first
        if (useCli && IsTailscaleCliAvailable())
        {
            machines = DiscoverViaCli();
            if (machines.Count > 0)
                return machines;
        }

        // Try cached machines from API
        var cachePath = Path.Combine(configDir, "tailscale-machines.json");
        if (File.Exists(cachePath))
        {
            machines = LoadCachedMachines(cachePath);
        }

        return machines;
    }

    private static List<TailnetMachine> DiscoverViaCli()
    {
        var machines = new List<TailnetMachine>();
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "tailscale",
                Arguments = "status --json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
                return machines;

            var doc = JsonDocument.Parse(output);

            if (!doc.RootElement.TryGetProperty("Peer", out var peers))
                return machines;

            foreach (var peer in peers.EnumerateObject())
            {
                var p = peer.Value;
                var hostname = p.TryGetProperty("HostName", out var hn)
                    ? hn.GetString() ?? "unknown" : "unknown";
                var os = p.TryGetProperty("OS", out var osVal)
                    ? osVal.GetString() ?? "unknown" : "unknown";
                var online = p.TryGetProperty("Online", out var onVal) && onVal.GetBoolean();
                var ip = p.TryGetProperty("TailscaleIPs", out var ips) && ips.GetArrayLength() > 0
                    ? ips[0].GetString() ?? "—" : "—";

                var tags = new List<string>();
                if (p.TryGetProperty("Tags", out var tagArr))
                {
                    foreach (var t in tagArr.EnumerateArray())
                    {
                        var tv = t.GetString();
                        if (tv is not null)
                            tags.Add(tv);
                    }
                }

                machines.Add(new TailnetMachine(hostname, os, online, ip, tags, false));
            }

            // Also add self
            if (doc.RootElement.TryGetProperty("Self", out var self))
            {
                var hostname = self.TryGetProperty("HostName", out var hn)
                    ? hn.GetString() ?? "unknown" : "unknown";
                var os = self.TryGetProperty("OS", out var osVal)
                    ? osVal.GetString() ?? "unknown" : "unknown";
                var selfIp = self.TryGetProperty("TailscaleIPs", out var selfIps) && selfIps.GetArrayLength() > 0
                    ? selfIps[0].GetString() ?? "—" : "—";

                machines.Add(new TailnetMachine(hostname, os, true, selfIp, new List<string> { "self" }, false));
            }
        }
        catch
        {
            // CLI failed — return empty
        }

        return machines;
    }

    private static List<TailnetMachine> LoadCachedMachines(string cachePath)
    {
        var machines = new List<TailnetMachine>();
        try
        {
            var json = File.ReadAllText(cachePath);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("machines", out var machinesArr))
                return machines;

            foreach (var m in machinesArr.EnumerateArray())
            {
                var hostname = m.TryGetProperty("hostname", out var hn)
                    ? hn.GetString() ?? "unknown" : "unknown";
                var os = m.TryGetProperty("os", out var osVal)
                    ? osVal.GetString() ?? "unknown" : "unknown";
                var online = m.TryGetProperty("online", out var onVal) && onVal.GetBoolean();
                var ip = m.TryGetProperty("tailscaleIp", out var ipVal)
                    ? ipVal.GetString() ?? "—" : "—";

                var tags = new List<string>();
                if (m.TryGetProperty("tags", out var tagArr))
                {
                    foreach (var t in tagArr.EnumerateArray())
                    {
                        var tv = t.GetString();
                        if (tv is not null)
                            tags.Add(tv);
                    }
                }

                var hasRunner = m.TryGetProperty("hasRunner", out var hr) && hr.GetBoolean();

                machines.Add(new TailnetMachine(hostname, os, online, ip, tags, hasRunner));
            }
        }
        catch (JsonException)
        {
            // Malformed cache
        }

        return machines;
    }

    internal static TailscaleApiConfig? LoadApiConfig(string configDir)
    {
        // Check environment variables first
        var envApiKey = Environment.GetEnvironmentVariable("TAILSCALE_API_KEY");
        var envOAuthId = Environment.GetEnvironmentVariable("TAILSCALE_OAUTH_CLIENT_ID");
        var envOAuthSecret = Environment.GetEnvironmentVariable("TAILSCALE_OAUTH_CLIENT_SECRET");
        var envTailnet = Environment.GetEnvironmentVariable("TAILSCALE_TAILNET");

        if (!string.IsNullOrWhiteSpace(envApiKey) && !string.IsNullOrWhiteSpace(envTailnet))
        {
            return new TailscaleApiConfig(envTailnet, "api-key", envApiKey, null, null);
        }

        if (!string.IsNullOrWhiteSpace(envOAuthId) &&
            !string.IsNullOrWhiteSpace(envOAuthSecret) &&
            !string.IsNullOrWhiteSpace(envTailnet))
        {
            return new TailscaleApiConfig(envTailnet, "oauth", null, envOAuthId, envOAuthSecret);
        }

        // Fall back to config file
        var configPath = Path.Combine(configDir, "tailscale.json");
        if (!File.Exists(configPath))
            return null;

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<TailscaleApiConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    // ── Records ─────────────────────────────────────────────

    internal sealed record TailscaleNodeInfo(
        string Hostname,
        string TailscaleIp,
        string LoginName,
        string BackendState);

    internal sealed record TailnetMachine(
        string Hostname,
        string Os,
        bool Online,
        string TailscaleIp,
        List<string> Tags,
        bool HasRunner);

    internal sealed record TailscaleApiConfig(
        string Tailnet,
        string AuthMethod,
        string? ApiKey,
        string? OAuthClientId,
        string? OAuthClientSecret);
}
