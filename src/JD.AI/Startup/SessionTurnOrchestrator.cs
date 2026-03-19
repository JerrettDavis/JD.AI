using JD.AI.Core.Agents;
using JD.AI.Core.Skills;
using JD.AI.Core.Usage;
using JD.SemanticKernel.Extensions.Compaction;

namespace JD.AI.Startup;

/// <summary>
/// UI-agnostic orchestration for a single agent turn. Hosts (TUI, dashboard, desktop, API)
/// provide rendering/input behavior while this class enforces core turn policy.
/// </summary>
internal sealed class SessionTurnOrchestrator
{
    private readonly AgentSession _session;
    private readonly GovernanceSetup _governance;
    private readonly SkillLifecycleManager _skillLifecycleManager;
    private readonly ICostEstimator _costEstimator;

    public SessionTurnOrchestrator(
        AgentSession session,
        GovernanceSetup governance,
        SkillLifecycleManager skillLifecycleManager,
        ICostEstimator? costEstimator = null)
    {
        _session = session;
        _governance = governance;
        _skillLifecycleManager = skillLifecycleManager;
        _costEstimator = costEstimator ?? new DefaultCostEstimator();
    }

    public async Task<SessionTurnExecutionResult> ExecuteAsync(
        AgentLoop agentLoop,
        string input,
        SessionTurnExecutionOptions options,
        CancellationToken ct = default)
    {
        var budgetPolicy = _governance.BudgetPolicy;
        if (budgetPolicy is not null)
        {
            if (budgetPolicy.MaxSessionUsd.HasValue &&
                _session.SessionSpendUsd >= budgetPolicy.MaxSessionUsd.Value)
            {
                options.OnWarning?.Invoke(
                    $"Budget limit (${budgetPolicy.MaxSessionUsd:F2}) reached — spent ${_session.SessionSpendUsd:F2}.");
                return SessionTurnExecutionResult.BudgetBlockedResult;
            }

            if (!await _governance.BudgetTracker.IsWithinBudgetAsync(budgetPolicy, ct).ConfigureAwait(false))
            {
                var status = await _governance.BudgetTracker.GetStatusAsync(ct).ConfigureAwait(false);
                options.OnWarning?.Invoke(
                    $"Budget exceeded — daily: ${status.TodayUsd:F2}, monthly: ${status.MonthUsd:F2}.");
                return SessionTurnExecutionResult.BudgetBlockedResult;
            }
        }

        string response;
        try
        {
            using (_skillLifecycleManager.BeginRunScope())
            {
                response = options.Streaming
                    ? await agentLoop.RunTurnStreamingAsync(input, ct).ConfigureAwait(false)
                    : await agentLoop.RunTurnAsync(input, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            options.OnWarning?.Invoke("Turn cancelled.");
            return SessionTurnExecutionResult.CancelledResult;
        }

        if (budgetPolicy is not null && _session.CurrentModel is not null)
        {
            var lastTurn = _session.SessionInfo?.Turns.LastOrDefault();
            if (lastTurn is not null)
            {
                var estimatedCost = _costEstimator.EstimateTurnCostUsd(
                    _session.CurrentModel,
                    lastTurn.TokensIn,
                    lastTurn.TokensOut);

                if (estimatedCost > 0m)
                {
                    _session.SessionSpendUsd += estimatedCost;
                    await _governance.BudgetTracker.RecordSpendAsync(
                            estimatedCost,
                            _session.CurrentModel.ProviderName,
                            ct)
                        .ConfigureAwait(false);
                }
            }
        }

        if (options.AutoCompact &&
            options.CompactThresholdPercent > 0 &&
            options.ContextWindowTokens > 0)
        {
            var estimatedTokens = TokenEstimator.EstimateTokens(_session.History);
            var threshold = (long)(options.ContextWindowTokens * (options.CompactThresholdPercent / 100.0));
            if (estimatedTokens > threshold)
            {
                options.OnInfo?.Invoke("Compacting context...");
                await _session.CompactAsync(ct).ConfigureAwait(false);
            }
        }

        return new SessionTurnExecutionResult(
            Completed: true,
            Cancelled: false,
            BlockedByBudget: false,
            Response: response);
    }
}

internal sealed record SessionTurnExecutionOptions(
    bool Streaming,
    bool AutoCompact,
    double CompactThresholdPercent,
    long ContextWindowTokens,
    Action<string>? OnInfo = null,
    Action<string>? OnWarning = null);

internal sealed record SessionTurnExecutionResult(
    bool Completed,
    bool Cancelled,
    bool BlockedByBudget,
    string? Response)
{
    public static SessionTurnExecutionResult CancelledResult { get; } =
        new(false, true, false, null);

    public static SessionTurnExecutionResult BudgetBlockedResult { get; } =
        new(false, false, true, null);
}
