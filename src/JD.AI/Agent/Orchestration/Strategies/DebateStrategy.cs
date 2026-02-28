using System.Diagnostics;

namespace JD.AI.Tui.Agent.Orchestration.Strategies;

/// <summary>
/// Debate strategy — multiple agents independently answer the same question with
/// different perspectives (e.g., optimist, skeptic, pragmatist), then a judge agent
/// evaluates and synthesizes the best answer.
/// Good for: architectural decisions, risk assessment, trade-off analysis.
/// </summary>
public sealed class DebateStrategy : IOrchestrationStrategy
{
    public string Name => "debate";

    public async Task<TeamResult> ExecuteAsync(
        IReadOnlyList<SubagentConfig> agents,
        TeamContext context,
        ISubagentExecutor executor,
        AgentSession parentSession,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new Dictionary<string, AgentResult>(StringComparer.Ordinal);

        // Phase 1: All debaters argue in parallel
        var debaterConfigs = agents.Select(agent =>
        {
            var perspective = agent.Perspective ?? agent.Name;
            return new SubagentConfig
            {
                Name = agent.Name,
                Type = agent.Type,
                Prompt = agent.Prompt,
                SystemPrompt = agent.SystemPrompt ??
                    $"""
                    {SubagentPrompts.GetSystemPrompt(agent.Type)}

                    You are arguing from the perspective of: {perspective}
                    Present your strongest case from this viewpoint. Be specific and cite evidence.
                    Acknowledge potential weaknesses in your position.
                    """,
                MaxTurns = agent.MaxTurns,
                ModelId = agent.ModelId,
                AdditionalTools = agent.AdditionalTools,
                Perspective = perspective,
            };
        }).ToList();

        context.RecordEvent("debate", AgentEventType.Started, $"Starting debate with {debaterConfigs.Count} perspectives");

        var debaterTasks = debaterConfigs.Select(config =>
            executor.ExecuteAsync(config, parentSession, context, onProgress, ct));

        var debaterResults = await Task.WhenAll(debaterTasks).ConfigureAwait(false);

        foreach (var result in debaterResults)
        {
            results[result.AgentName] = result;
            context.WriteScratchpad($"argument:{result.AgentName}", result.Output);
        }

        // Phase 2: Judge evaluates and synthesizes
        context.RecordEvent("judge", AgentEventType.Started, "Evaluating debate arguments");
        onProgress?.Invoke(new SubagentProgress("judge", SubagentStatus.Started, "Evaluating arguments"));

        var judgePrompt = BuildJudgePrompt(context, debaterResults);
        var judgeConfig = new SubagentConfig
        {
            Name = "judge",
            Type = SubagentType.General,
            Prompt = judgePrompt,
            SystemPrompt = """
                You are a judge evaluating a debate between multiple agents.
                Each agent argued from a different perspective. Your job is to:
                1. Evaluate the strength and evidence of each argument
                2. Identify areas of agreement and disagreement
                3. Synthesize the strongest elements into a balanced recommendation
                4. Be explicit about trade-offs and remaining uncertainties

                Structure your response as:
                - **Summary**: One paragraph overview
                - **Analysis**: Brief evaluation of each perspective
                - **Recommendation**: Your synthesized conclusion
                - **Caveats**: Important uncertainties or conditions
                """,
            MaxTurns = 1,
        };

        var judgeResult = await executor.ExecuteAsync(
            judgeConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

        results["judge"] = judgeResult;

        sw.Stop();

        return new TeamResult
        {
            Output = judgeResult.Output,
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = judgeResult.Success,
        };
    }

    private static string BuildJudgePrompt(TeamContext context, AgentResult[] debaterResults)
    {
        var parts = new List<string>
        {
            $"Debate topic / goal: {context.Goal}",
            "",
            "Arguments presented:",
        };

        foreach (var result in debaterResults)
        {
            var perspective = context.ReadScratchpad($"perspective:{result.AgentName}") ?? result.AgentName;
            parts.Add($"\n--- Perspective: {perspective} (Agent: {result.AgentName}) ---");
            parts.Add(result.Output);
        }

        parts.Add("\nEvaluate these arguments and provide your recommendation.");
        return string.Join('\n', parts);
    }
}
