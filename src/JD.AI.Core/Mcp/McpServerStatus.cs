namespace JD.AI.Core.Mcp;

/// <summary>Runtime connection status of an MCP server.</summary>
public sealed record McpServerStatus
{
    /// <summary>Current connection state.</summary>
    public McpConnectionState State { get; init; } = McpConnectionState.Unknown;

    /// <summary>When the status was last evaluated (UTC).</summary>
    public DateTimeOffset LastCheckedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Short, human-readable summary of the last error (if any).</summary>
    public string? LastErrorSummary { get; init; }

    /// <summary>Full error details for debug output.</summary>
    public string? LastErrorDetails { get; init; }

    /// <summary>Returns a display icon matching the current state.</summary>
    public string Icon => State switch
    {
        McpConnectionState.Connected  => "✔",
        McpConnectionState.Failed     => "✘",
        McpConnectionState.Connecting => "…",
        McpConnectionState.Disabled   => "○",
        _                             => "?",
    };

    /// <summary>A pre-built <see cref="McpServerStatus"/> for the <c>Unknown</c> state.</summary>
    public static readonly McpServerStatus Default = new();
}
