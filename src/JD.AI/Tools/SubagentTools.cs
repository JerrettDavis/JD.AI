using System.ComponentModel;
using System.Text.Json;
using JD.AI.Tui.Agent;
using JD.AI.Tui.Agent.Orchestration;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// Kernel functions for spawning subagents and teams from the parent agent loop.
/// </summary>
public sealed class SubagentTools
{
    private readonly TeamOrchestrator _orchestrator;

    public SubagentTools(TeamOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [KernelFunction("spawn_agent")]
    [Description("Spawn a specialized subagent to handle a task. Types: explore (fast codebase Q&A), task (run commands), plan (create implementation plans), review (code review), general (full capability). Modes: single (default, one-shot), multi (multi-turn with tool calling loop).")]
    public async Task<string> SpawnAgentAsync(
        [Description("Subagent type: explore, task, plan, review, or general")] string type,
        [Description("The task or question for the subagent")] string prompt,
        [Description("Execution mode: 'single' (default) or 'multi' (multi-turn with tool calling)")] string mode = "single",
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<SubagentType>(type, ignoreCase: true, out var subagentType))
        {
            return $"Invalid subagent type '{type}'. Valid types: explore, task, plan, review, general.";
        }

        var config = new SubagentConfig
        {
            Name = $"{type}-{Guid.NewGuid():N}"[..16],
            Type = subagentType,
            Prompt = prompt,
        };

        var multiTurn = string.Equals(mode, "multi", StringComparison.OrdinalIgnoreCase);
        var result = await _orchestrator.RunAgentAsync(config, multiTurn, ct: ct).ConfigureAwait(false);

        return result.Success ? result.Output : $"[Agent error: {result.Error}]";
    }

    [KernelFunction("spawn_team")]
    [Description("""
        Spawn an orchestrated team of agents. Strategies:
        - 'sequential': Agents run in order, each gets previous output (pipeline)
        - 'fan-out': All agents run in parallel, results merged by synthesizer
        - 'supervisor': Coordinator dispatches work, reviews, can redirect/retry
        - 'debate': Multiple perspectives argue, judge synthesizes best answer

        The agents parameter is a JSON array of objects with fields:
        - name (string): unique agent name
        - type (string): explore, task, plan, review, or general
        - prompt (string): the agent's task
        - perspective (string, optional): for debate strategy
        """)]
    public async Task<string> SpawnTeamAsync(
        [Description("Orchestration strategy: sequential, fan-out, supervisor, or debate")] string strategy,
        [Description("JSON array of agent configs: [{name, type, prompt, perspective?}]")] string agents,
        [Description("The team's high-level goal")] string goal,
        [Description("Use multi-turn execution for agents (default: false)")] bool multiTurn = false,
        CancellationToken ct = default)
    {
#pragma warning disable CA1031
        try
        {
            var configs = ParseAgentConfigs(agents);
            if (configs.Count == 0)
            {
                return "No valid agent configurations provided. Expected JSON array of {name, type, prompt}.";
            }

            var result = await _orchestrator.RunTeamAsync(
                strategy, configs, goal, multiTurn, ct: ct).ConfigureAwait(false);

            return result.Success
                ? result.Output
                : $"[Team failed ({result.Strategy}): {result.Output}]";
        }
        catch (JsonException ex)
        {
            return $"Invalid agents JSON: {ex.Message}. Expected: [{{\"name\":\"...\",\"type\":\"...\",\"prompt\":\"...\"}}]";
        }
#pragma warning restore CA1031
    }

    [KernelFunction("query_team_context")]
    [Description("Query the current team's shared scratchpad or event log. Use key to read a specific scratchpad entry, or 'events' to get the event log, or 'results' to see completed agent outputs.")]
    public string QueryTeamContext(
        [Description("What to query: a scratchpad key, 'events', or 'results'")] string key)
    {
        // This is a simplified version — in a real team, the TeamContextTools
        // are injected directly. This provides a fallback for the parent agent.
        return $"Team context query for '{key}' — use spawn_team to run a team with shared context.";
    }

    private static List<SubagentConfig> ParseAgentConfigs(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var configs = new List<SubagentConfig>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var name = element.GetProperty("name").GetString() ?? "agent";
            var typeStr = element.GetProperty("type").GetString() ?? "general";
            var prompt = element.GetProperty("prompt").GetString() ?? "";
            var perspective = element.TryGetProperty("perspective", out var p) ? p.GetString() : null;

            if (!Enum.TryParse<SubagentType>(typeStr, ignoreCase: true, out var type))
            {
                type = SubagentType.General;
            }

            configs.Add(new SubagentConfig
            {
                Name = name,
                Type = type,
                Prompt = prompt,
                Perspective = perspective,
            });
        }

        return configs;
    }
}

