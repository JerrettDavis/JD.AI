namespace JD.AI.Startup;

/// <summary>
/// Builds the system prompt from overrides, files, instructions, and plan mode.
/// Extracted from Program.cs lines 441-492.
/// </summary>
internal static class SystemPromptBuilder
{
    public static async Task<string> BuildAsync(CliOptions opts, InstructionsResult instructions, bool planMode)
    {
        string systemPrompt;
        if (opts.SystemPromptOverride != null)
        {
            systemPrompt = opts.SystemPromptOverride;
        }
        else if (opts.SystemPromptFile != null && File.Exists(opts.SystemPromptFile))
        {
            systemPrompt = await File.ReadAllTextAsync(opts.SystemPromptFile).ConfigureAwait(false);
        }
        else
        {
            systemPrompt = """
                You are jdai, a helpful AI coding assistant running in a terminal.
                You have access to tools for file operations, code search, shell commands,
                git operations, web fetching, web search, semantic memory, and subagents.

                When helping with code tasks:
                - Read relevant files before making changes
                - Use search tools to find code patterns
                - Make minimal, surgical edits
                - Verify changes with builds/tests when appropriate
                - Store important decisions and facts in memory for future recall
                - Use subagents for specialized work (explore for analysis, task for commands, plan for planning, review for code review)
                - Use native tool/function calls when a tool is needed; if native tool calls are unavailable, emit a single tagged JSON tool call (<tool_use>...</tool_use> or <tool_call>...</tool_call>) so the runtime can execute it

                Be concise and direct. Use tools proactively when they'll help answer the question.
                """;

            if (instructions.HasInstructions)
            {
                systemPrompt += "\n\n" + instructions.ToSystemPrompt();
            }
        }

        // Append additional prompt text
        if (opts.AppendSystemPrompt != null)
        {
            systemPrompt += "\n\n" + opts.AppendSystemPrompt;
        }

        if (opts.AppendSystemPromptFile != null && File.Exists(opts.AppendSystemPromptFile))
        {
            systemPrompt += "\n\n" + await File.ReadAllTextAsync(opts.AppendSystemPromptFile).ConfigureAwait(false);
        }

        // Plan mode injection
        if (planMode)
        {
            systemPrompt += "\n\nYou are in plan mode. DO NOT make changes to files. Only read, explore, and plan.";
        }

        return systemPrompt;
    }
}
