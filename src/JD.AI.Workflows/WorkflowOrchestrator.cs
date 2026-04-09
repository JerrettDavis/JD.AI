using System.Diagnostics;
using JD.AI.Workflows.History;
using JD.AI.Workflows.Steps;

namespace JD.AI.Workflows;

/// <summary>
/// Orchestrates the full workflow pipeline:
/// 1. Classify prompt (is this a workflow?)
/// 2. Match against catalog (do we have a workflow for this?)
/// 3. Optionally advise from history before execution
/// 4. If match found, execute via WorkflowBridge
/// 5. If no match, return "planning needed" result
/// 6. Optionally ingest the run result into the history graph
/// </summary>
public sealed class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IPromptIntentClassifier _classifier;
    private readonly IWorkflowMatcher _matcher;
    private readonly IWorkflowBridge _bridge;
    private readonly IWorkflowHistoryAdvisor? _historyAdvisor;
    private readonly IWorkflowHistoryObserver? _historyObserver;

    public WorkflowOrchestrator(
        IPromptIntentClassifier classifier,
        IWorkflowMatcher matcher,
        IWorkflowBridge bridge,
        IWorkflowHistoryAdvisor? historyAdvisor = null,
        IWorkflowHistoryObserver? historyObserver = null)
    {
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _historyAdvisor = historyAdvisor;
        _historyObserver = historyObserver;
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

        // 3. Advise from history (optional)
        HistoryAdvisory? advisory = null;
        if (_historyAdvisor is not null)
        {
            advisory = await _historyAdvisor.AdviseAsync(match.Definition, ct)
                .ConfigureAwait(false);
        }

        // 4. Execute
        data ??= new AgentWorkflowData { Prompt = prompt };

        WorkflowBridgeResult executionResult;
        try
        {
            executionResult = await _bridge.ExecuteAsync(match.Definition, data, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            executionResult = new WorkflowBridgeResult
            {
                Success = false,
                Errors = [ex.Message],
            };
        }

        // 5. Ingest run into history (optional, fire-and-forget on failure)
        if (_historyObserver is not null)
        {
            await _historyObserver.IngestRunAsync(match.Definition, executionResult, ct)
                .ConfigureAwait(false);
        }

        return executionResult.Success
            ? WorkflowOrchestratorResult.Executed(intent, match, executionResult, advisory)
            : WorkflowOrchestratorResult.Failed(intent, match, executionResult, advisory);
    }
}
