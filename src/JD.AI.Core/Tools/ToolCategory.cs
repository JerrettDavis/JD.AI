namespace JD.AI.Core.Tools;

/// <summary>
/// Categories for grouping tool plugins into logical domains.
/// Used by <see cref="ToolLoadout"/> to include or exclude related tools as a set.
/// </summary>
public enum ToolCategory
{
    /// <summary>File read/write, directory listing, and batch-editing tools.</summary>
    Filesystem,

    /// <summary>Git version-control tools.</summary>
    Git,

    /// <summary>GitHub API integration tools.</summary>
    GitHub,

    /// <summary>Shell execution and process-management tools.</summary>
    Shell,

    /// <summary>Web-fetching and browser-automation tools.</summary>
    Web,

    /// <summary>Search and grep tools.</summary>
    Search,

    /// <summary>Network and connectivity tools (e.g. Tailscale).</summary>
    Network,

    /// <summary>In-session memory and knowledge-store tools.</summary>
    Memory,

    /// <summary>Agent orchestration, sessions, and workflow tools.</summary>
    Orchestration,

    /// <summary>Analysis, reasoning, notebook, and introspection tools.</summary>
    Analysis,

    /// <summary>Scheduling and cron tools.</summary>
    Scheduling,

    /// <summary>Multimodal processing tools (images, PDFs).</summary>
    Multimodal,

    /// <summary>Security, policy, and encoding/cryptography tools.</summary>
    Security,
}
