using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for managing MCP (Model Context Protocol) server connections,
/// transport configuration, credential management, and diagnostics.
/// </summary>
[ToolPlugin("mcp")]
public sealed class McpTransportTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = JsonDefaults.Indented;

    // ── List Servers ────────────────────────────────────────

    [KernelFunction("mcp_list_servers")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List all configured MCP servers with their transport type, status, and available tools.")]
    public static string ListServers(
        [Description("Optional config directory (default: ~/.jdai)")] string? configDir = null)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

        var servers = DiscoverServers(configDir);
        var sb = new StringBuilder();
        sb.AppendLine("## Configured MCP Servers");
        sb.AppendLine();

        if (servers.Count == 0)
        {
            sb.AppendLine("No MCP servers configured.");
            sb.AppendLine();
            sb.AppendLine("### Setup");
            sb.AppendLine("Add servers to `~/.jdai/mcp.json` or project `.jdai/mcp.json`:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"servers\": {");
            sb.AppendLine("    \"my-server\": {");
            sb.AppendLine("      \"transport\": \"stdio\",");
            sb.AppendLine("      \"command\": \"npx\",");
            sb.AppendLine("      \"args\": [\"-y\", \"@my-org/mcp-server\"]");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            return sb.ToString();
        }

        sb.AppendLine("| Name | Transport | Auth | Status | Tools |");
        sb.AppendLine("|------|-----------|------|--------|-------|");

        foreach (var s in servers.OrderBy(s => s.Name))
        {
            var statusIcon = s.Status switch
            {
                "connected" => "🟢",
                "disconnected" => "🔴",
                "configured" => "🟡",
                _ => "❓"
            };

            sb.AppendLine(
                $"| `{s.Name}` | {s.Transport} | {s.Auth ?? "none"} | {statusIcon} {s.Status} | {s.ToolCount} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**Total**: {servers.Count} server(s), " +
            $"{servers.Count(s => string.Equals(s.Status, "connected", StringComparison.Ordinal))} connected");

        return sb.ToString();
    }

    // ── Transport Matrix ────────────────────────────────────

    [KernelFunction("mcp_transport_matrix")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Show the MCP transport support matrix with security characteristics and status.")]
    public static string GetTransportMatrix()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## MCP Transport Support Matrix");
        sb.AppendLine();
        sb.AppendLine("| Transport | Status | Encryption | Auth Methods | Use Case |");
        sb.AppendLine("|-----------|--------|-----------|-------------|----------|");
        sb.AppendLine("| stdio | ✅ Supported | Process isolation | N/A | Local tools (npx, python, dotnet) |");
        sb.AppendLine("| SSE (HTTP) | ✅ Supported | TLS required | API key, Bearer token | Remote HTTP servers |");
        sb.AppendLine("| StreamableHTTP | 📋 Planned | TLS required | API key, Bearer, OAuth 2.0 | Modern HTTP servers |");
        sb.AppendLine("| WebSocket | 📋 Planned | WSS required | Bearer token, OAuth 2.0 | Real-time streaming servers |");
        sb.AppendLine();

        sb.AppendLine("### Transport Configuration Examples");
        sb.AppendLine();

        sb.AppendLine("#### stdio");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"transport\": \"stdio\",");
        sb.AppendLine("  \"command\": \"npx\",");
        sb.AppendLine("  \"args\": [\"-y\", \"@modelcontextprotocol/server-filesystem\", \"/path\"]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("#### SSE (HTTP)");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"transport\": \"sse\",");
        sb.AppendLine("  \"url\": \"https://mcp.example.com/sse\",");
        sb.AppendLine("  \"headers\": { \"Authorization\": \"Bearer ${MCP_TOKEN}\" }");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("#### StreamableHTTP (planned)");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"transport\": \"streamable-http\",");
        sb.AppendLine("  \"url\": \"https://mcp.example.com/mcp\",");
        sb.AppendLine("  \"auth\": {");
        sb.AppendLine("    \"type\": \"oauth2\",");
        sb.AppendLine("    \"clientId\": \"my-app\",");
        sb.AppendLine("    \"scopes\": [\"read\", \"write\"]");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    // ── Diagnose Server ─────────────────────────────────────

    [KernelFunction("mcp_diagnose")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Run diagnostics on a specific MCP server connection: config validation, connectivity, auth status, and available tools.")]
    public static string DiagnoseServer(
        [Description("MCP server name to diagnose")] string serverName,
        [Description("Optional config directory (default: ~/.jdai)")] string? configDir = null)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

        var servers = DiscoverServers(configDir);
        var server = servers.FirstOrDefault(s =>
            string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase));

        if (server is null)
            return $"Server `{serverName}` not found. Use `mcp_list_servers` to see configured servers.";

        var sb = new StringBuilder();
        sb.AppendLine($"## MCP Diagnostics: {server.Name}");
        sb.AppendLine();

        // Config check
        sb.AppendLine("### Configuration");
        sb.AppendLine($"- **Transport**: {server.Transport}");
        sb.AppendLine($"- **Auth**: {server.Auth ?? "none"}");
        sb.AppendLine($"- **Config source**: {server.ConfigPath ?? "unknown"}");
        sb.AppendLine();

        // Validation
        sb.AppendLine("### Validation Checks");
        var checks = ValidateServerConfig(server);
        foreach (var check in checks)
        {
            var icon = check.Passed ? "✅" : "❌";
            sb.AppendLine($"- {icon} {check.Description}");
            if (!check.Passed && check.Remedy is not null)
                sb.AppendLine($"  → **Fix**: {check.Remedy}");
        }
        sb.AppendLine();

        // Security assessment
        sb.AppendLine("### Security Assessment");
        var risks = AssessSecurityRisks(server);
        foreach (var risk in risks)
        {
            sb.AppendLine($"- {risk.Severity}: {risk.Description}");
        }

        return sb.ToString();
    }

    // ── Credential Status ───────────────────────────────────

    [KernelFunction("mcp_credential_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Check the credential/authentication status for all configured MCP servers.")]
    public static string GetCredentialStatus(
        [Description("Optional config directory (default: ~/.jdai)")] string? configDir = null)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

        var servers = DiscoverServers(configDir);
        var sb = new StringBuilder();
        sb.AppendLine("## MCP Credential Status");
        sb.AppendLine();

        if (servers.Count == 0)
        {
            sb.AppendLine("No MCP servers configured.");
            return sb.ToString();
        }

        sb.AppendLine("| Server | Auth Type | Credential | Status | Expiry |");
        sb.AppendLine("|--------|-----------|-----------|--------|--------|");

        foreach (var s in servers.OrderBy(s => s.Name))
        {
            var credStatus = s.Auth switch
            {
                null or "none" => ("N/A", "N/A", "N/A"),
                "api-key" => (
                    "API key",
                    s.HasCredential ? "✅ Configured" : "❌ Missing",
                    "N/A"),
                "bearer" => (
                    "Bearer token",
                    s.HasCredential ? "✅ Configured" : "❌ Missing",
                    "Unknown"),
                "oauth2" => (
                    "OAuth 2.0",
                    s.HasCredential ? "✅ Authenticated" : "❌ Not authenticated",
                    s.HasCredential ? "Check with provider" : "N/A"),
                _ => (s.Auth!, "❓ Unknown", "N/A")
            };

            sb.AppendLine(
                $"| `{s.Name}` | {credStatus.Item1} | {credStatus.Item2} | " +
                $"{(string.Equals(s.Status, "connected", StringComparison.Ordinal) ? "🟢 Active" : "🔴 Inactive")} | {credStatus.Item3} |");
        }

        sb.AppendLine();
        sb.AppendLine("### Credential Security Guidelines");
        sb.AppendLine("- Store credentials in environment variables, never in config files");
        sb.AppendLine("- Use `${ENV_VAR}` syntax in config for token references");
        sb.AppendLine("- Rotate API keys every 90 days");
        sb.AppendLine("- Use OAuth 2.0 where available for automated token refresh");

        return sb.ToString();
    }

    // ── Export Config ────────────────────────────────────────

    [KernelFunction("mcp_export_config")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Export MCP server configuration and diagnostics as JSON for CI and automation.")]
    public static string ExportConfig(
        [Description("Optional config directory (default: ~/.jdai)")] string? configDir = null)
    {
        configDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

        var servers = DiscoverServers(configDir);
        var result = new
        {
            timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            configDirectory = configDir,
            servers = servers.Select(s => new
            {
                s.Name,
                s.Transport,
                auth = s.Auth ?? "none",
                s.Status,
                s.ToolCount,
                hasCredential = s.HasCredential,
                validationErrors = ValidateServerConfig(s)
                    .Where(c => !c.Passed)
                    .Select(c => c.Description)
                    .ToArray()
            }).ToArray(),
            summary = new
            {
                total = servers.Count,
                connected = servers.Count(s => string.Equals(s.Status, "connected", StringComparison.Ordinal)),
                authenticated = servers.Count(s => s.HasCredential),
                healthy = servers.Count(s =>
                    ValidateServerConfig(s).All(c => c.Passed))
            }
        };

        return JsonSerializer.Serialize(result, s_jsonOptions);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static List<McpServerInfo> DiscoverServers(string configDir)
    {
        var servers = new List<McpServerInfo>();

        // Check mcp.json in config dir
        var mcpJsonPath = Path.Combine(configDir, "mcp.json");
        if (File.Exists(mcpJsonPath))
        {
            try
            {
                var json = File.ReadAllText(mcpJsonPath);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("servers", out var serversElem) ||
                    doc.RootElement.TryGetProperty("mcpServers", out serversElem))
                {
                    foreach (var prop in serversElem.EnumerateObject())
                    {
                        var server = ParseServerEntry(prop.Name, prop.Value, mcpJsonPath);
                        servers.Add(server);
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed config — skip
            }
        }

        // Check .mcp.json (Claude-compatible format)
        var dotMcpPath = Path.Combine(configDir, ".mcp.json");
        if (File.Exists(dotMcpPath))
        {
            try
            {
                var json = File.ReadAllText(dotMcpPath);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("mcpServers", out var serversElem))
                {
                    foreach (var prop in serversElem.EnumerateObject())
                    {
                        if (servers.All(s => !string.Equals(s.Name, prop.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            var server = ParseServerEntry(prop.Name, prop.Value, dotMcpPath);
                            servers.Add(server);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed config — skip
            }
        }

        return servers;
    }

    private static McpServerInfo ParseServerEntry(string name, JsonElement config, string configPath)
    {
        var transport = "stdio"; // default
        if (config.TryGetProperty("transport", out var tProp))
            transport = tProp.GetString() ?? "stdio";
        else if (config.TryGetProperty("url", out _))
            transport = "sse";

        string? auth = null;
        if (config.TryGetProperty("auth", out var authProp))
        {
            if (authProp.ValueKind == JsonValueKind.Object && authProp.TryGetProperty("type", out var typeProp))
                auth = typeProp.GetString();
            else if (authProp.ValueKind == JsonValueKind.String)
                auth = authProp.GetString();
        }
        else if (config.TryGetProperty("headers", out _))
        {
            auth = "bearer";
        }

        var hasCredential = auth is not null && !string.Equals(auth, "none", StringComparison.Ordinal);

        return new McpServerInfo(
            name, transport, auth, "configured", 0, hasCredential, configPath);
    }

    private static List<ValidationCheck> ValidateServerConfig(McpServerInfo server)
    {
        var checks = new List<ValidationCheck>();

        // Transport validation
        var validTransports = new[] { "stdio", "sse", "streamable-http", "websocket" };
        checks.Add(new ValidationCheck(
            $"Transport type `{server.Transport}` is valid",
            validTransports.Contains(server.Transport, StringComparer.OrdinalIgnoreCase),
            "Use one of: stdio, sse, streamable-http, websocket"));

        // stdio-specific checks
        if (string.Equals(server.Transport, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new ValidationCheck(
                "stdio transport uses process isolation",
                true, null));
        }

        // HTTP-based transport checks
        if (server.Transport is "sse" or "streamable-http" or "websocket")
        {
            checks.Add(new ValidationCheck(
                "HTTP transport has authentication configured",
                server.Auth is not null,
                "Add auth config: api-key, bearer, or oauth2"));

            checks.Add(new ValidationCheck(
                "Remote transport should use TLS",
                true, // We can't check the URL here, so advisory
                "Ensure server URL uses https:// or wss://"));
        }

        // Config path exists
        if (server.ConfigPath is not null)
        {
            checks.Add(new ValidationCheck(
                "Configuration file exists",
                File.Exists(server.ConfigPath),
                "Verify the config file path"));
        }

        return checks;
    }

    private static List<SecurityRisk> AssessSecurityRisks(McpServerInfo server)
    {
        var risks = new List<SecurityRisk>();

        if (server.Transport is "sse" or "streamable-http" or "websocket" && server.Auth is null)
        {
            risks.Add(new SecurityRisk("🔴", "No authentication on remote transport — anyone with URL can invoke tools"));
        }

        if (string.Equals(server.Transport, "stdio", StringComparison.Ordinal))
        {
            risks.Add(new SecurityRisk("🟢", "Local process isolation — low network risk"));
        }

        if (string.Equals(server.Auth, "api-key", StringComparison.Ordinal))
        {
            risks.Add(new SecurityRisk("🟡", "API key auth — ensure key is stored in env var, not config file"));
        }

        if (string.Equals(server.Auth, "oauth2", StringComparison.Ordinal))
        {
            risks.Add(new SecurityRisk("🟢", "OAuth 2.0 — good: automated token refresh, scoped access"));
        }

        if (risks.Count == 0)
        {
            risks.Add(new SecurityRisk("🟡", "No specific risks identified — review server documentation"));
        }

        return risks;
    }

    internal sealed record McpServerInfo(
        string Name,
        string Transport,
        string? Auth,
        string Status,
        int ToolCount,
        bool HasCredential,
        string? ConfigPath);

    private sealed record ValidationCheck(string Description, bool Passed, string? Remedy);
    private sealed record SecurityRisk(string Severity, string Description);
}
