using System.Diagnostics;
using System.Text;

namespace JD.AI.Tui.Agent.Orchestration.Strategies;

/// <summary>
/// Supervisor strategy — a coordinator agent dispatches work to worker agents,
/// reviews their results, and can redirect or retry until satisfied.
/// Good for: complex multi-step tasks where quality matters.
/// </summary>
public sealed class SupervisorStrategy : IOrchestrationStrategy
{
    private const int MaxSupervisorIterations = 3;

    public string Name => "supervisor";

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

        // Phase 1: Dispatch all workers
        context.RecordEvent("supervisor", AgentEventType.Decision, "Dispatching workers");
        onProgress?.Invoke(new SubagentProgress("supervisor", SubagentStatus.Started, "Dispatching workers"));

        var workerTasks = agents.Select(agent =>
            executor.ExecuteAsync(agent, parentSession, context, onProgress, ct));

        var workerResults = await Task.WhenAll(workerTasks).ConfigureAwait(false);

        foreach (var result in workerResults)
        {
            results[result.AgentName] = result;
            context.WriteScratchpad($"output:{result.AgentName}", result.Output);
        }

        // Phase 2: Supervisor reviews and potentially redirects
        var supervisorOutput = new StringBuilder();

        for (var iteration = 0; iteration < MaxSupervisorIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            context.RecordEvent("supervisor", AgentEventType.Decision,
                $"Review iteration {iteration + 1}");
            onProgress?.Invoke(new SubagentProgress(
                "supervisor", SubagentStatus.Thinking,
                $"Review iteration {iteration + 1}/{MaxSupervisorIterations}"));

            var reviewPrompt = BuildReviewPrompt(context, results, iteration);
            var reviewConfig = new SubagentConfig
            {
                Name = $"supervisor-review-{iteration}",
                Type = SubagentType.Review,
                Prompt = reviewPrompt,
                SystemPrompt = $"""
                    You are a supervisor agent reviewing worker outputs for quality.
                    Team goal: {context.Goal}

                    Evaluate the combined worker outputs. If the work is satisfactory and complete,
                    respond with "APPROVED:" followed by your final synthesized output.

                    If work needs improvement, respond with "REDIRECT:" followed by specific
                    instructions for what needs to change. Be specific about which agent's work
                    needs revision and why.
                    """,
                MaxTurns = 1,
            };

            var reviewResult = await executor.ExecuteAsync(
                reviewConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

            results[$"supervisor-review-{iteration}"] = reviewResult;

            if (reviewResult.Output.StartsWith("APPROVED:", StringComparison.OrdinalIgnoreCase))
            {
                supervisorOutput.Append(reviewResult.Output["APPROVED:".Length..].Trim());
                context.RecordEvent("supervisor", AgentEventType.Completed, "Work approved");
                break;
            }

            if (reviewResult.Output.StartsWith("REDIRECT:", StringComparison.OrdinalIgnoreCase))
            {
                var redirectInstructions = reviewResult.Output["REDIRECT:".Length..].Trim();
                context.RecordEvent("supervisor", AgentEventType.Decision,
                    $"Redirecting: {(redirectInstructions.Length > 100 ? string.Concat(redirectInstructions.AsSpan(0, 100), "...") : redirectInstructions)}");

                // Re-run workers with redirect feedback
                var retryTasks = agents.Select(agent =>
                {
                    var retryConfig = new SubagentConfig
                    {
                        Name = $"{agent.Name}-retry-{iteration}",
                        Type = agent.Type,
                        Prompt = $"{agent.Prompt}\n\n--- Supervisor feedback ---\n{redirectInstructions}\n\n--- Your previous output ---\n{results.GetValueOrDefault(agent.Name)?.Output ?? "(none)"}",
                        SystemPrompt = agent.SystemPrompt,
                        MaxTurns = agent.MaxTurns,
                        ModelId = agent.ModelId,
                        AdditionalTools = agent.AdditionalTools,
                    };
                    return executor.ExecuteAsync(retryConfig, parentSession, context, onProgress, ct);
                });

                var retryResults = await Task.WhenAll(retryTasks).ConfigureAwait(false);
                foreach (var result in retryResults)
                {
                    results[result.AgentName] = result;
                    // Update scratchpad with retry output
                    var originalName = result.AgentName.Split("-retry-")[0];
                    context.WriteScratchpad($"output:{originalName}", result.Output);
                }

                continue;
            }

            // Neither APPROVED nor REDIRECT — treat as final output
            supervisorOutput.Append(reviewResult.Output);
            break;
        }

        sw.Stop();

        var finalOutput = supervisorOutput.Length > 0
            ? supervisorOutput.ToString()
            : string.Join("\n\n", results.Values.Where(r => r.Success).Select(r => r.Output));

        return new TeamResult
        {
            Output = finalOutput,
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = results.Values.Any(r => r.Success),
        };
    }

    private static string BuildReviewPrompt(
        TeamContext context,
        Dictionary<string, AgentResult> results,
        int iteration)
    {
        var parts = new List<string>
        {
            $"Review iteration: {iteration + 1}",
            $"Team goal: {context.Goal}",
            "",
            "Worker outputs to review:",
        };

        foreach (var (name, result) in results.Where(r => !r.Key.StartsWith("supervisor", StringComparison.Ordinal)))
        {
            parts.Add($"\n--- {name} (success={result.Success}) ---");
            parts.Add(result.Output);
        }

        return string.Join('\n', parts);
    }
}
