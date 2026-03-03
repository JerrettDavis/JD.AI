namespace JD.AI.Core.Mcp;

/// <summary>
/// Normalized, source-agnostic representation of an MCP server configuration.
/// </summary>
public sealed record McpServerDefinition
{
    /// <summary>Stable key used for look-up, persistence and CLI references.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable display name (defaults to <see cref="Name"/>).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>How the server is reached.</summary>
    public McpTransport Transport { get; init; } = McpTransport.Stdio;

    // ── Stdio-specific ────────────────────────────────────────────────────────

    /// <summary>Executable to launch (stdio transport only).</summary>
    public string? Command { get; init; }

    /// <summary>Arguments passed to <see cref="Command"/> (stdio transport only).</summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>Extra environment variables for the child process (stdio transport only).</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    // ── HTTP-specific ─────────────────────────────────────────────────────────

    /// <summary>HTTP/HTTPS endpoint URL (http transport only).</summary>
    public string? Url { get; init; }

    // ── Metadata ──────────────────────────────────────────────────────────────

    /// <summary>Configuration scope (BuiltIn, User, Project).</summary>
    public McpScope Scope { get; init; } = McpScope.User;

    /// <summary>Which discovery provider surfaced this entry.</summary>
    public string SourceProvider { get; init; } = string.Empty;

    /// <summary>Absolute path to the config file that defines this server, if applicable.</summary>
    public string? SourcePath { get; init; }

    /// <summary>Whether the server is enabled for use by the agent.</summary>
    public bool IsEnabled { get; init; } = true;
}
