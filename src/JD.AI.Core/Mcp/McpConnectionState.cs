namespace JD.AI.Core.Mcp;

/// <summary>Live connection state of an MCP server.</summary>
public enum McpConnectionState
{
    /// <summary>State has not been checked yet.</summary>
    Unknown,

    /// <summary>Successfully connected.</summary>
    Connected,

    /// <summary>Connection attempt failed.</summary>
    Failed,

    /// <summary>Connection check is in progress.</summary>
    Connecting,

    /// <summary>Server is administratively disabled.</summary>
    Disabled,
}
