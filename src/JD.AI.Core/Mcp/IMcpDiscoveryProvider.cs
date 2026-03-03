namespace JD.AI.Core.Mcp;

/// <summary>
/// Discovers MCP server definitions from a single configuration source.
/// Implement this interface to add new discovery back-ends (Claude Code, Copilot, etc.)
/// without changing the TUI or CLI code.
/// </summary>
public interface IMcpDiscoveryProvider
{
    /// <summary>Human-readable label for the source (e.g. "Claude Code user config").</summary>
    string SourceLabel { get; }

    /// <summary>Discover and return all MCP servers from this source.</summary>
    Task<IReadOnlyList<McpServerDefinition>> DiscoverAsync(CancellationToken ct = default);
}
