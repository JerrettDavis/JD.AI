using System.Diagnostics;

namespace JD.AI.Core.Agents.Orchestration.Strategies;

/// <summary>
/// Map-Reduce strategy — split input into chunks, process in parallel, then reduce.
/// Good for: bulk file analysis, large codebase scanning, data processing.
/// </summary>
public sealed class MapReduceStrategy : IOrchestrationStrategy
{
    public string Name => "map-reduce";

    /// <summary>Maximum number of parallel mapper agents.</summary>
    public int MaxParallelism { get; init; } = 4;

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

        if (agents.Count == 0)
        {
            sw.Stop();
            return new TeamResult
            {
                Output = "No agents configured for map-reduce.",
                Strategy = Name,
                AgentResults = results,
                Duration = sw.Elapsed,
                Success = false,
            };
        }

        // Last agent is the reducer; all others are mappers
        var mappers = agents.Count > 1 ? agents.Take(agents.Count - 1).ToList() : agents.ToList();
        var reducerTemplate = agents.Count > 1 ? agents[^1] : null;

        // Execute mappers in parallel with bounded concurrency
        using var semaphore = new SemaphoreSlim(MaxParallelism);
        var mapTasks = mappers.Select(async mapper =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await executor.ExecuteAsync(
                    mapper, parentSession, context, onProgress, ct).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var mapResults = await Task.WhenAll(mapTasks).ConfigureAwait(false);

        foreach (var result in mapResults)
        {
            results[result.AgentName] = result;
            context.WriteScratchpad($"map:{result.AgentName}", result.Output);
        }

        // Reduce phase — synthesize mapper outputs
        string finalOutput;
        if (reducerTemplate is not null)
        {
            var reducePrompt = BuildReducePrompt(context, mapResults);
            var reducerConfig = new SubagentConfig
            {
                Name = reducerTemplate.Name,
                Type = reducerTemplate.Type,
                Prompt = reducePrompt,
                SystemPrompt = reducerTemplate.SystemPrompt ?? """
                    You are a reducer agent in a map-reduce pipeline.
                    Multiple mapper agents have independently processed different parts of the input.
                    Your job is to merge, deduplicate, and synthesize their outputs into a
                    single coherent result. Resolve conflicts by preferring the most detailed analysis.
                    Organize the output logically (don't just concatenate).
                    """,
                MaxTurns = reducerTemplate.MaxTurns,
                ModelId = reducerTemplate.ModelId,
            };

            var reduceResult = await executor.ExecuteAsync(
                reducerConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

            results[reducerConfig.Name] = reduceResult;
            finalOutput = reduceResult.Output;
        }
        else
        {
            // No separate reducer — concatenate map results
            finalOutput = string.Join("\n\n---\n\n",
                mapResults.Select(r => $"## {r.AgentName}\n{r.Output}"));
        }

        sw.Stop();

        return new TeamResult
        {
            Output = finalOutput,
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = mapResults.All(r => r.Success),
        };
    }

    private static string BuildReducePrompt(TeamContext context, AgentResult[] mapResults)
    {
        var parts = new List<string>
        {
            $"Team goal: {context.Goal}",
            "",
            $"{mapResults.Length} mapper agents have processed different parts of the input:",
            "",
        };

        foreach (var result in mapResults)
        {
            parts.Add($"--- Mapper: {result.AgentName} (success={result.Success}) ---");
            parts.Add(result.Output);
            parts.Add("");
        }

        parts.Add("Merge and synthesize all mapper outputs into a single unified result.");
        return string.Join('\n', parts);
    }
}
