using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Core.Mcp;

/// <summary>
/// Discovers MCP servers from the Claude Code user-level config file
/// (<c>~/.claude.json</c> on Linux/macOS, <c>%USERPROFILE%\.claude.json</c> on Windows).
/// </summary>
public sealed class ClaudeCodeUserMcpDiscoveryProvider : IMcpDiscoveryProvider
{
    private readonly string _configPath;

    public ClaudeCodeUserMcpDiscoveryProvider()
        : this(DefaultPath()) { }

    internal ClaudeCodeUserMcpDiscoveryProvider(string configPath)
    {
        _configPath = configPath;
    }

    public string SourceLabel => $"Claude Code user config ({_configPath})";

    public Task<IReadOnlyList<McpServerDefinition>> DiscoverAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_configPath))
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);

        try
        {
            var json = File.ReadAllText(_configPath);
            var servers = ParseClaudeJson(json, McpScope.User, "ClaudeCode", _configPath);
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>(servers);
        }
#pragma warning disable CA1031 // non-fatal: log and return empty
        catch
        {
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);
        }
#pragma warning restore CA1031
    }

    internal static IReadOnlyList<McpServerDefinition> ParseClaudeJson(
        string json, McpScope scope, string sourceProvider, string? sourcePath)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("mcpServers", out var mcpServers))
            return [];

        var result = new List<McpServerDefinition>();
        foreach (var prop in mcpServers.EnumerateObject())
        {
            var def = ParseServer(prop.Name, prop.Value, scope, sourceProvider, sourcePath);
            if (def is not null)
                result.Add(def);
        }

        return result;
    }

    private static McpServerDefinition? ParseServer(
        string name, JsonElement el, McpScope scope, string sourceProvider, string? sourcePath)
    {
        var transportStr = el.TryGetProperty("type", out var typeProp)
            ? typeProp.GetString() ?? "stdio"
            : "stdio";

        var transport = string.Equals(transportStr, "http", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(transportStr, "https", StringComparison.OrdinalIgnoreCase)
            ? McpTransport.Http
            : McpTransport.Stdio;

        string? command = null;
        IReadOnlyList<string> args = [];
        IReadOnlyDictionary<string, string> env = new Dictionary<string, string>(StringComparer.Ordinal);
        string? url = null;

        if (transport == McpTransport.Stdio)
        {
            command = el.TryGetProperty("command", out var cmd) ? cmd.GetString() : null;

            if (el.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == JsonValueKind.Array)
                args = argsProp.EnumerateArray()
                    .Select(a => a.GetString() ?? string.Empty)
                    .ToList();

            if (el.TryGetProperty("env", out var envProp) && envProp.ValueKind == JsonValueKind.Object)
                env = envProp.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty,
                        StringComparer.Ordinal);
        }
        else
        {
            url = el.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        }

        return new McpServerDefinition
        {
            Name = name,
            DisplayName = name,
            Transport = transport,
            Command = command,
            Args = args,
            Env = env,
            Url = url,
            Scope = scope,
            SourceProvider = sourceProvider,
            SourcePath = sourcePath,
            IsEnabled = true,
        };
    }

    private static string DefaultPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude.json");
    }
}
