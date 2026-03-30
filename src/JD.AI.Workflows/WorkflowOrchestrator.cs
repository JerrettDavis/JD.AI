using System.Diagnostics;
using JD.AI.Workflows.Steps;

namespace JD.AI.Workflows;

/// <summary>
/// Orchestrates the full workflow pipeline:
/// 1. Classify prompt (is this a workflow?)
/// 2. Match against catalog (do we have a workflow for this?)
/// 3. If match found, execute via WorkflowBridge
/// 4. If no match, return "planning needed" result
/// </summary>
public sealed class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IPromptIntentClassifier _classifier;
    private readonly IWorkflowMatcher _matcher;
    private readonly IWorkflowBridge _bridge;

    public WorkflowOrchestrator(
        IPromptIntentClassifier classifier,
        IWorkflowMatcher matcher,
        IWorkflowBridge bridge)
    {
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <inheritdoc/>
    public async Task<WorkflowOrchestratorResult> ProcessAsync(
        string prompt,
        AgentWorkflowData? data = null,
        CancellationToken ct = default)
    {
        // 1. Classify
        if (string.IsNullOrWhiteSpace(prompt))
            return WorkflowOrchestratorResult.PassThrough(
                new IntentClassification(false, 0.0, []));

        var intent = _classifier.Classify(prompt);
        if (!intent.IsWorkflow)
            return WorkflowOrchestratorResult.PassThrough(intent);

        // 2. Match
        var match = await _matcher.MatchAsync(new AgentRequest(prompt), ct)
            .ConfigureAwait(false);

        if (match is null)
            return WorkflowOrchestratorResult.PlanningNeeded(intent, prompt);

        // 3. Execute
        data ??= new AgentWorkflowData { Prompt = prompt };

        try
        {
            var result = await _bridge.ExecuteAsync(match.Definition, data, ct)
                .ConfigureAwait(false);

            return result.Success
                ? WorkflowOrchestratorResult.Executed(intent, match, result)
                : WorkflowOrchestratorResult.Failed(intent, match, result);
        }
        catch (Exception ex)
        {
            var errorResult = new WorkflowBridgeResult
            {
                Success = false,
                Errors = [ex.Message],
            };
            return WorkflowOrchestratorResult.Failed(intent, match, errorResult);
        }
    }
}
