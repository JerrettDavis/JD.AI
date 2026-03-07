namespace JD.AI.Core.Agents;

/// <summary>Type of subagent with scoped tool access and model preference.</summary>
public enum SubagentType
{
    /// <summary>Fast codebase analysis — read-only tools, cheap model.</summary>
    Explore,

    /// <summary>Run commands, report pass/fail — shell + read tools, cheap model.</summary>
    Task,

    /// <summary>Create implementation plans — read + search + memory, smart model.</summary>
    Plan,

    /// <summary>Code review on diffs/files — read + git + search, smart model.</summary>
    Review,

    /// <summary>Full capability — all tools, same model as parent.</summary>
    General,
}
