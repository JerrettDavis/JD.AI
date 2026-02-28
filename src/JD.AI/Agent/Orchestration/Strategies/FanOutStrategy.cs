using System.Diagnostics;

namespace JD.AI.Tui.Agent.Orchestration.Strategies;

/// <summary>
/// Fan-out / fan-in — all agents run in parallel, then a synthesizer agent
/// merges their results into a single coherent output.
/// Good for: parallel code review, multi-perspective analysis.
/// </summary>
public sealed class FanOutStrategy : IOrchestrationStrategy
{
    public string Name => "fan-out";

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

        // Run all agents in parallel
        var tasks = agents.Select(agent =>
            executor.ExecuteAsync(agent, parentSession, context, onProgress, ct));

        var agentResults = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in agentResults)
        {
            results[result.AgentName] = result;
            context.WriteScratchpad($"output:{result.AgentName}", result.Output);
        }

        // Run synthesizer to merge results
        var synthesisPrompt = BuildSynthesisPrompt(context, agentResults);
        var synthesizerConfig = new SubagentConfig
        {
            Name = "synthesizer",
            Type = SubagentType.General,
            Prompt = synthesisPrompt,
            SystemPrompt = """
                You are a synthesis agent. Your job is to merge the outputs of multiple agents
                into a single coherent, well-organized response. Resolve any conflicts or
                contradictions. Preserve important details from each agent's contribution.
                Do not add information that wasn't in the agent outputs.
                """,
            MaxTurns = 1,
        };

        var synthesisResult = await executor.ExecuteAsync(
            synthesizerConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

        results["synthesizer"] = synthesisResult;

        sw.Stop();

        return new TeamResult
        {
            Output = synthesisResult.Output,
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = agentResults.All(r => r.Success),
        };
    }

    private static string BuildSynthesisPrompt(TeamContext context, AgentResult[] results)
    {
        var parts = new List<string>
        {
            $"Team goal: {context.Goal}",
            "",
            "The following agents have completed their work. Synthesize their outputs into a single coherent response:",
            "",
        };

        foreach (var result in results)
        {
            parts.Add($"--- Agent: {result.AgentName} (success={result.Success}) ---");
            parts.Add(result.Output);
            parts.Add("");
        }

        parts.Add("Synthesize the above into a single well-organized response.");
        return string.Join('\n', parts);
    }
}
