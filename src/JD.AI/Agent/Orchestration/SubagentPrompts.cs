namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>
/// Centralized system prompts and tool scoping for subagent types.
/// </summary>
internal static class SubagentPrompts
{
    public static string GetSystemPrompt(SubagentType type) => type switch
    {
        SubagentType.Explore => """
            You are an explore subagent. Your job is to quickly analyze code and answer questions.
            Use search and read tools to find relevant code. Return focused answers under 300 words.
            Do NOT modify any files.
            """,
        SubagentType.Task => """
            You are a task subagent. Your job is to execute commands and report results.
            On success, return a brief summary (e.g., "All 247 tests passed", "Build succeeded").
            On failure, return the full error output (stack traces, compiler errors).
            """,
        SubagentType.Plan => """
            You are a planning subagent. Your job is to create structured implementation plans.
            Analyze the codebase, understand the architecture, and create a step-by-step plan
            with specific files to create/modify, components to build, and testing strategy.
            """,
        SubagentType.Review => """
            You are a code review subagent. Analyze diffs and files for:
            - Bugs and logic errors
            - Security vulnerabilities
            - Performance issues
            Only surface issues that genuinely matter. Never comment on style or formatting.
            """,
        SubagentType.General => """
            You are a general-purpose subagent with full tool access.
            Complete the assigned task thoroughly and report results.
            """,
        _ => "You are a subagent. Complete the assigned task.",
    };

    public static HashSet<string> GetToolSet(SubagentType type) => type switch
    {
        SubagentType.Explore => ["FileTools", "SearchTools", "GitTools", "MemoryTools"],
        SubagentType.Task => ["ShellTools", "FileTools", "SearchTools"],
        SubagentType.Plan => ["FileTools", "SearchTools", "MemoryTools", "GitTools"],
        SubagentType.Review => ["FileTools", "SearchTools", "GitTools"],
        SubagentType.General => ["FileTools", "SearchTools", "GitTools", "ShellTools", "WebTools", "MemoryTools"],
        _ => [],
    };
}
