namespace JD.AI.Core.Mcp;

/// <summary>Transport type for an MCP server.</summary>
public enum McpTransport
{
    /// <summary>Standard I/O — launches a local process.</summary>
    Stdio,

    /// <summary>HTTP/HTTPS endpoint.</summary>
    Http,
}
