namespace JD.AI.Core.Mcp;

/// <summary>Configuration scope of an MCP server entry.</summary>
public enum McpScope
{
    /// <summary>Always-available built-in server.</summary>
    BuiltIn,

    /// <summary>User-level config (e.g. ~/.claude.json or ~/.jdai/mcp.json).</summary>
    User,

    /// <summary>Project-level config (e.g. .mcp.json in the workspace root).</summary>
    Project,
}
