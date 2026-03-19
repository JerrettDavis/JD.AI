using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Core.Skills;
using JD.AI.Startup;
using Xunit;

namespace JD.AI.IntegrationTests;

internal sealed class HeadlessAgentIntegrationHarness : IDisposable
{
    private readonly SkillLifecycleManager _skills = new([]);
    private readonly GovernanceSetup _governance;

    public AgentSession Session { get; }
    public AgentLoop Loop { get; }
    public SessionTurnOrchestrator Orchestrator { get; }

    private HeadlessAgentIntegrationHarness(
        AgentSession session,
        GovernanceSetup governance)
    {
        Session = session;
        _governance = governance;
        Loop = new AgentLoop(session);
        Orchestrator = new SessionTurnOrchestrator(session, governance, _skills);
    }

    public static HeadlessAgentIntegrationHarness Create(
        IProviderDetector detector,
        ProviderModelInfo model)
    {
        var kernel = detector.BuildKernel(model);
        var registry = new ProviderRegistry([detector]);
        var session = new AgentSession(registry, kernel, model);

        var opts = new CliOptions { PrintMode = true };
        var governance = GovernanceInitializer.Initialize(
            Directory.GetCurrentDirectory(),
            session,
            kernel,
            opts,
            maxBudgetUsd: null);

        return new HeadlessAgentIntegrationHarness(session, governance);
    }

    public async Task<string> ExecuteTurnAsync(
        string input,
        bool streaming = false,
        CancellationToken ct = default)
    {
        var contextWindow = Session.CurrentModel?.ContextWindowTokens ?? 0;
        var result = await Orchestrator
            .ExecuteAsync(
                Loop,
                input,
                new SessionTurnExecutionOptions(
                    Streaming: streaming,
                    AutoCompact: false,
                    CompactThresholdPercent: 0,
                    ContextWindowTokens: contextWindow),
                ct)
            .ConfigureAwait(false);

        Assert.True(result.Completed, "Headless orchestrator did not complete the turn.");
        return result.Response ?? string.Empty;
    }

    public void Dispose()
    {
        _skills.Dispose();
        _governance.BudgetTracker.Dispose();
        _governance.FileAuditSink.Dispose();
    }
}
