namespace JD.AI.Core.Mcp;

/// <summary>
/// Describes a single entry in the curated MCP server catalog.
/// </summary>
public sealed record CuratedMcpEntry(
    /// <summary>Unique identifier used when registering the server (e.g. "github", "windows-mcp").</summary>
    string Id,
    /// <summary>Human-readable display name shown in picker UI.</summary>
    string DisplayName,
    /// <summary>Category used to group entries in the picker (e.g. "Source Control").</summary>
    string Category,
    /// <summary>One-line description of what the server does.</summary>
    string Description,
    /// <summary>Transport type: Stdio or Http.</summary>
    CuratedMcpTransport Transport,
    /// <summary>Executable to invoke for stdio transports (e.g. "uvx", "npx").</summary>
    string? Command = null,
    /// <summary>Default arguments for stdio transports. May contain placeholder tokens (see <see cref="PromptArgs"/>).</summary>
    IReadOnlyList<string>? DefaultArgs = null,
    /// <summary>HTTP endpoint URL for HTTP transports.</summary>
    string? Url = null,
    /// <summary>Environment variables required by this server that must be collected from the user.</summary>
    IReadOnlyList<CuratedMcpEnvVar>? RequiredEnvVars = null,
    /// <summary>
    /// Named argument placeholders that require user input before the server can be invoked.
    /// Each placeholder appears as <c>{name}</c> inside <see cref="DefaultArgs"/>.
    /// </summary>
    IReadOnlyList<CuratedMcpArgPrompt>? PromptArgs = null,
    /// <summary>Link to documentation or the server's GitHub page.</summary>
    string? DocsUrl = null,
    /// <summary>Short note displayed beneath the entry in the picker UI (e.g. "Requires Python 3.11+").</summary>
    string? InstallNote = null);

/// <summary>Transport mechanism used by a curated MCP server.</summary>
public enum CuratedMcpTransport
{
    /// <summary>Server communicates over stdin/stdout.</summary>
    Stdio,
    /// <summary>Server exposes an HTTP(S) endpoint.</summary>
    Http,
}

/// <summary>An environment variable that must be supplied by the user when installing a server.</summary>
public sealed record CuratedMcpEnvVar(
    /// <summary>Environment variable name (e.g. "GITHUB_TOKEN").</summary>
    string Name,
    /// <summary>Prompt shown to the user when collecting the value.</summary>
    string Prompt,
    /// <summary>When <c>true</c> the collected value is masked in the terminal.</summary>
    bool IsSecret = true);

/// <summary>A positional argument placeholder requiring user input before the server starts.</summary>
public sealed record CuratedMcpArgPrompt(
    /// <summary>Placeholder token that appears inside <see cref="CuratedMcpEntry.DefaultArgs"/> (without braces).</summary>
    string Placeholder,
    /// <summary>Prompt shown to the user when collecting the value.</summary>
    string Prompt,
    /// <summary>Optional example value shown alongside the prompt.</summary>
    string? Example = null);
