using System.Diagnostics;
using JD.AI.Tui.Agent.Orchestration.Strategies;
using JD.AI.Tui.Rendering;

namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>
/// Manages team lifecycle: parse team config, select strategy, create executors,
/// run strategy, and collect results. Entry point for spawn_team.
/// </summary>
public sealed class TeamOrchestrator
{
    private readonly AgentSession _parentSession;
    private readonly int _maxDepth;

    public TeamOrchestrator(AgentSession parentSession, int maxDepth = 2)
    {
        _parentSession = parentSession;
        _maxDepth = maxDepth;
    }

    /// <summary>
    /// Spawn and orchestrate a team of agents with the given strategy.
    /// </summary>
    public async Task<TeamResult> RunTeamAsync(
        string strategyName,
        IReadOnlyList<SubagentConfig> agents,
        string goal,
        bool multiTurn = false,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        var strategy = ResolveStrategy(strategyName);
        if (strategy == null)
        {
            return new TeamResult
            {
                Output = $"Unknown strategy '{strategyName}'. Valid: sequential, fan-out, supervisor, debate.",
                Strategy = strategyName,
                Success = false,
            };
        }

        var context = new TeamContext(goal) { MaxDepth = _maxDepth };

        ChatRenderer.RenderInfo($"  🏗️ Team ({strategy.Name}): {agents.Count} agent(s) — {goal}");

        ISubagentExecutor executor = multiTurn
            ? new MultiTurnExecutor()
            : new SingleTurnExecutor();

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await strategy.ExecuteAsync(
                agents, context, executor, _parentSession, onProgress, ct).ConfigureAwait(false);

            sw.Stop();
            ChatRenderer.RenderInfo(
                $"  ✅ Team complete ({strategy.Name}): {result.AgentResults.Count} agents, " +
                $"{result.TotalTokens} tokens, {sw.Elapsed.TotalSeconds:F1}s");

            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new TeamResult
            {
                Output = "[Team execution cancelled]",
                Strategy = strategy.Name,
                Duration = sw.Elapsed,
                Success = false,
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            sw.Stop();
            ChatRenderer.RenderWarning($"  ⚠ Team failed: {ex.Message}");
            return new TeamResult
            {
                Output = $"Team execution failed: {ex.Message}",
                Strategy = strategy.Name,
                Duration = sw.Elapsed,
                Success = false,
            };
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Spawn a single agent with the specified execution mode.
    /// </summary>
    public async Task<AgentResult> RunAgentAsync(
        SubagentConfig config,
        bool multiTurn = false,
        TeamContext? teamContext = null,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        // Check nesting depth
        if (teamContext != null && !teamContext.CanNest)
        {
            return new AgentResult
            {
                AgentName = config.Name,
                Output = $"Cannot spawn: max nesting depth ({teamContext.MaxDepth}) reached.",
                Success = false,
                Error = "Max nesting depth reached",
            };
        }

        ISubagentExecutor executor = multiTurn
            ? new MultiTurnExecutor()
            : new SingleTurnExecutor();

        ChatRenderer.RenderInfo($"  🔀 Spawning {config.Type} agent '{config.Name}' ({(multiTurn ? "multi-turn" : "single-turn")})...");

        return await executor.ExecuteAsync(
            config, _parentSession, teamContext, onProgress, ct).ConfigureAwait(false);
    }

    private static IOrchestrationStrategy? ResolveStrategy(string name) =>
        name.ToUpperInvariant() switch
        {
            "SEQUENTIAL" or "PIPELINE" => new SequentialStrategy(),
            "FAN-OUT" or "FANOUT" or "PARALLEL" => new FanOutStrategy(),
            "SUPERVISOR" or "COORDINATOR" => new SupervisorStrategy(),
            "DEBATE" or "ADVERSARIAL" => new DebateStrategy(),
            _ => null,
        };
}
