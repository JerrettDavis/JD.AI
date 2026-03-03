namespace JD.AI.Core.Mcp;

/// <summary>
/// Discovers MCP servers from the Claude Code project-level config file
/// (<c>.mcp.json</c> in the current working directory).
/// </summary>
public sealed class ClaudeCodeProjectMcpDiscoveryProvider : IMcpDiscoveryProvider
{
    private readonly string _configPath;

    public ClaudeCodeProjectMcpDiscoveryProvider()
        : this(Path.Combine(Directory.GetCurrentDirectory(), ".mcp.json")) { }

    internal ClaudeCodeProjectMcpDiscoveryProvider(string configPath)
    {
        _configPath = configPath;
    }

    public string SourceLabel => $"Claude Code project config ({_configPath})";

    public Task<IReadOnlyList<McpServerDefinition>> DiscoverAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_configPath))
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);

        try
        {
            var json = File.ReadAllText(_configPath);
            var servers = ClaudeCodeUserMcpDiscoveryProvider.ParseClaudeJson(
                json, McpScope.Project, "ClaudeCode", _configPath);
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>(servers);
        }
#pragma warning disable CA1031 // non-fatal: return empty on parse error
        catch
        {
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);
        }
#pragma warning restore CA1031
    }
}
