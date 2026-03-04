using System.Diagnostics;

namespace JD.AI.Core.Agents.Orchestration.Strategies;

/// <summary>
/// Pipeline strategy — a typed sequential execution where each agent's output
/// is explicitly transformed before being passed to the next stage.
/// Unlike Sequential/Relay which pass raw output, Pipeline stages have
/// defined input/output contracts and can specify transform functions.
/// Good for: CI/CD workflows, ETL processes, staged code review.
/// </summary>
public sealed class PipelineStrategy : IOrchestrationStrategy
{
    public string Name => "pipeline";

    /// <summary>
    /// Template for pipeline stage prompts.
    /// Supports {stage_input}, {stage_number}, {total_stages}, and {stage_role} placeholders.
    /// </summary>
    public string StagePromptTemplate { get; init; } =
        """
        You are stage {stage_number} of {total_stages} in a processing pipeline.
        Your role: {stage_role}

        Input from previous stage:
        {stage_input}

        Process this input according to your role and produce output for the next stage.
        Output ONLY the processed result, no meta-commentary.
        """;

    /// <summary>When true, a failed stage halts the entire pipeline.</summary>
    public bool FailFast { get; init; } = true;

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
        var stageInput = context.Goal;
        var pipelineFailed = false;
        string? failedStage = null;

        for (var i = 0; i < agents.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var agent = agents[i];
            var stageRole = agent.Perspective ?? agent.Name;
            var stageNumber = i + 1;

            var prompt = StagePromptTemplate
                .Replace("{stage_input}", stageInput, StringComparison.Ordinal)
                .Replace("{stage_number}", stageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{total_stages}", agents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{stage_role}", stageRole, StringComparison.Ordinal);

            var stageConfig = new SubagentConfig
            {
                Name = agent.Name,
                Type = agent.Type,
                Prompt = prompt,
                SystemPrompt = agent.SystemPrompt,
                MaxTurns = agent.MaxTurns,
                ModelId = agent.ModelId,
                AdditionalTools = agent.AdditionalTools,
            };

            var result = await executor.ExecuteAsync(
                stageConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

            results[agent.Name] = result;
            context.WriteScratchpad($"pipeline:{i}:{agent.Name}", result.Output);

            context.RecordEvent(new AgentEvent(
                agent.Name, AgentEventType.Decision,
                $"Pipeline stage {stageNumber}/{agents.Count} completed: {(result.Success ? "success" : "failed")}"));

            if (!result.Success && FailFast)
            {
                pipelineFailed = true;
                failedStage = agent.Name;
                context.RecordEvent(new AgentEvent(
                    agent.Name, AgentEventType.Error,
                    $"Pipeline halted at stage {stageNumber}/{agents.Count}: {agent.Name} failed"));
                break;
            }

            stageInput = result.Output;
        }

        sw.Stop();

        var output = pipelineFailed
            ? $"Pipeline failed at stage '{failedStage}'. Partial output:\n{stageInput}"
            : stageInput;

        return new TeamResult
        {
            Output = output,
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = !pipelineFailed && results.Values.All(r => r.Success),
        };
    }
}
