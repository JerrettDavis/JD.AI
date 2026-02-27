using System.ComponentModel;
using JD.AI.Tui.Agent;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// Kernel functions for spawning subagents from the parent agent loop.
/// </summary>
public sealed class SubagentTools
{
    private readonly SubagentRunner _runner;

    public SubagentTools(SubagentRunner runner)
    {
        _runner = runner;
    }

    [KernelFunction("spawn_agent")]
    [Description("Spawn a specialized subagent to handle a task. Types: explore (fast codebase Q&A), task (run commands), plan (create implementation plans), review (code review), general (full capability).")]
    public async Task<string> SpawnAgentAsync(
        [Description("Subagent type: explore, task, plan, review, or general")] string type,
        [Description("The task or question for the subagent")] string prompt,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<SubagentType>(type, ignoreCase: true, out var subagentType))
        {
            return $"Invalid subagent type '{type}'. Valid types: explore, task, plan, review, general.";
        }

        return await _runner.RunAsync(subagentType, prompt, ct).ConfigureAwait(false);
    }
}
