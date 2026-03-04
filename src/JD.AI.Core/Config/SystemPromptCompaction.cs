namespace JD.AI.Core.Config;

/// <summary>
/// Controls automatic system prompt compaction behavior.
/// </summary>
public enum SystemPromptCompaction
{
    /// <summary>Never auto-compact system prompt.</summary>
    Off,

    /// <summary>Compact only when system prompt exceeds budget.</summary>
    Auto,

    /// <summary>Always compact system prompt at startup.</summary>
    Always,
}
