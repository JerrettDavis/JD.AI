using System.ComponentModel;
using System.Globalization;
using System.Text;
using JD.AI.Core.Agents;
using JD.AI.Core.Attributes;
using JD.AI.Core.Sessions;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tools;

/// <summary>
/// Session orchestration tools compatible with OpenClaw patterns, built atop
/// existing <see cref="AgentSession"/> and <see cref="SessionStore"/> infrastructure.
/// Provides explicit session inventory, history, spawning, and agent listing.
/// </summary>
[ToolPlugin("sessions", RequiresInjection = true)]
public sealed class SessionOrchestrationTools
{
    private readonly AgentSession _session;

    public SessionOrchestrationTools(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    [KernelFunction("sessions_list")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List sessions for this project. Returns session IDs, names, dates, token counts, and status.")]
    public async Task<string> ListSessionsAsync(
        [Description("Maximum number of sessions to return (default 20)")] int limit = 20,
        [Description("Filter: 'active', 'closed', or 'all' (default 'all')")] string filter = "all",
        CancellationToken ct = default)
    {
        _ = ct; // reserved for future async cancellation
        var store = _session.Store;
        if (store is null)
            return "Session persistence is not enabled.";

        var projectHash = _session.SessionInfo?.ProjectHash;
        var sessions = await store.ListSessionsAsync(projectHash, limit).ConfigureAwait(false);

        if (string.Equals(filter, "active", StringComparison.OrdinalIgnoreCase))
            sessions = new System.Collections.ObjectModel.ReadOnlyCollection<SessionInfo>(
                sessions.Where(s => s.IsActive).ToList());
        else if (string.Equals(filter, "closed", StringComparison.OrdinalIgnoreCase))
            sessions = new System.Collections.ObjectModel.ReadOnlyCollection<SessionInfo>(
                sessions.Where(s => !s.IsActive).ToList());

        if (sessions.Count == 0)
            return "No sessions found.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Sessions ({sessions.Count}):");
        sb.AppendLine("─────────────────────────────────────────────────────");

        var currentId = _session.SessionInfo?.Id;
        foreach (var s in sessions)
        {
            var marker = string.Equals(s.Id, currentId, StringComparison.Ordinal) ? " ◀ current" : "";
            var status = s.IsActive ? "active" : "closed";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {s.Id}  {s.Name ?? "(unnamed)",-20}  {status,-7}  {s.MessageCount,3} turns  {s.TotalTokens,8} tokens  {s.UpdatedAt:yyyy-MM-dd HH:mm}{marker}");
        }

        return sb.ToString();
    }

    [KernelFunction("sessions_history")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get turn history for a session. Returns user/assistant messages with token counts and tool calls. Defaults to the current session.")]
    public async Task<string> GetSessionHistoryAsync(
        [Description("Session ID (default: current session)")] string? sessionId = null,
        [Description("Starting turn index, 0-based (default 0)")] int startTurn = 0,
        [Description("Maximum number of turns to return (default 20)")] int maxTurns = 20,
        CancellationToken ct = default)
    {
        _ = ct;
        var store = _session.Store;
        if (store is null)
            return "Session persistence is not enabled.";

        sessionId ??= _session.SessionInfo?.Id;
        if (sessionId is null)
            return "No active session and no session ID specified.";

        // Security: only allow access to sessions in the same project
        var target = await store.GetSessionAsync(sessionId).ConfigureAwait(false);
        if (target is null)
            return $"Session '{sessionId}' not found.";

        if (!string.Equals(target.ProjectHash, _session.SessionInfo?.ProjectHash, StringComparison.Ordinal))
            return "Access denied: session belongs to a different project.";

        var turns = target.Turns
            .OrderBy(t => t.TurnIndex)
            .Skip(startTurn)
            .Take(maxTurns)
            .ToList();

        if (turns.Count == 0)
            return $"No turns found in session '{sessionId}' starting from turn {startTurn}.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Session {sessionId} history (turns {startTurn}–{startTurn + turns.Count - 1} of {target.Turns.Count}):");
        sb.AppendLine("─────────────────────────────────────────────────────");

        foreach (var turn in turns)
        {
            var role = turn.Role.ToUpperInvariant();
            var content = turn.Content ?? "(empty)";
            if (content.Length > 200)
                content = string.Concat(content.AsSpan(0, 197), "...");

            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  [{turn.TurnIndex}] {role}: {content}");

            if (turn.TokensIn > 0 || turn.TokensOut > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"       tokens: {turn.TokensIn} in / {turn.TokensOut} out  ({turn.DurationMs}ms)");

            if (turn.ToolCalls.Count > 0)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"       tools: {string.Join(", ", turn.ToolCalls.Select(tc => $"{tc.ToolName}({tc.Status})"))}");
            }
        }

        return sb.ToString();
    }

    [KernelFunction("sessions_spawn")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Create a new session or fork the current session. Use mode='fork' to clone history, or mode='new' for a fresh session.")]
    public async Task<string> SpawnSessionAsync(
        [Description("Spawn mode: 'new' (fresh session) or 'fork' (clone current)")] string mode = "new",
        [Description("Optional name for the new session")] string? name = null,
        CancellationToken ct = default)
    {
        _ = ct;
        if (string.Equals(mode, "fork", StringComparison.OrdinalIgnoreCase))
        {
            if (_session.SessionInfo is null)
                return "Cannot fork: no active session.";

            var forked = await _session.ForkSessionAsync(name).ConfigureAwait(false);
            return forked is not null
                ? $"Forked session created: {forked.Id} (name: {forked.Name ?? "(unnamed)"})"
                : "Fork failed — session persistence may not be enabled.";
        }

        var store = _session.Store;
        if (store is null)
            return "Session persistence is not enabled.";

        var newSession = new SessionInfo
        {
            ProjectPath = _session.SessionInfo?.ProjectPath ?? Directory.GetCurrentDirectory(),
            ProjectHash = _session.SessionInfo?.ProjectHash ??
                ProjectHasher.Hash(Directory.GetCurrentDirectory()),
            Name = name,
        };

        await store.CreateSessionAsync(newSession).ConfigureAwait(false);
        return $"New session created: {newSession.Id} (name: {newSession.Name ?? "(unnamed)"})";
    }

    [KernelFunction("session_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get the current session's status: ID, project, model, turn count, tokens, spend, and timing.")]
    public string GetSessionStatus()
    {
        var info = _session.SessionInfo;
        if (info is null)
            return "No active session.";

        var model = _session.CurrentModel;
        var sb = new StringBuilder();
        sb.AppendLine("Session Status");
        sb.AppendLine("─────────────────────────────────────────────────────");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  ID:       {info.Id}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Name:     {info.Name ?? "(unnamed)"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Project:  {info.ProjectPath}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Model:    [{model?.ProviderName ?? "?"}] {model?.DisplayName ?? info.ModelId ?? "?"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Status:   {(info.IsActive ? "ACTIVE" : "CLOSED")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Turns:    {info.MessageCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Tokens:   {info.TotalTokens:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Spend:    ${_session.SessionSpendUsd:F4}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Created:  {info.CreatedAt:u}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Updated:  {info.UpdatedAt:u}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Turn idx: {_session.TurnIndex}");

        if (info.ForkPoints.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Forks:    {info.ForkPoints.Count} fork point(s)");

        if (info.ModelSwitchHistory.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Switches: {info.ModelSwitchHistory.Count} model switch(es)");

        return sb.ToString();
    }

    [KernelFunction("agents_list")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List available agent types and any active team agents from the current orchestration context.")]
    public string ListAgents()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Available Agent Types");
        sb.AppendLine("─────────────────────────────────────────────────────");
        sb.AppendLine("  explore  — Fast codebase Q&A (read-only tools)");
        sb.AppendLine("  task     — Execute commands, builds, tests");
        sb.AppendLine("  plan     — Create implementation plans");
        sb.AppendLine("  review   — Code review and analysis");
        sb.AppendLine("  general  — Full capability agent");
        sb.AppendLine();
        sb.AppendLine("Orchestration Strategies");
        sb.AppendLine("─────────────────────────────────────────────────────");
        sb.AppendLine("  sequential  — Pipeline: each agent gets previous output");
        sb.AppendLine("  fan-out     — Parallel execution, results merged");
        sb.AppendLine("  supervisor  — Coordinator dispatches and reviews");
        sb.AppendLine("  debate      — Multiple perspectives + judge");
        sb.AppendLine("  voting      — Consensus through voting");
        sb.AppendLine("  relay       — Handoff chain between agents");
        sb.AppendLine("  map-reduce  — Split work, merge results");
        sb.AppendLine("  pipeline    — Typed transformation chain");
        sb.AppendLine("  blackboard  — Shared knowledge board");
        sb.AppendLine();
        sb.AppendLine("Use spawn_agent(type, prompt) or spawn_team(strategy, agents, goal) to launch.");

        return sb.ToString();
    }

    [KernelFunction("sessions_send")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Send a message to the current session's history for context injection. The message is recorded as a system note visible to subsequent turns.")]
    public async Task<string> SendMessageAsync(
        [Description("The message content to inject")] string message,
        CancellationToken ct = default)
    {
        _ = ct;
        if (_session.SessionInfo is null || _session.Store is null)
            return "Session persistence is not enabled.";

        if (string.IsNullOrWhiteSpace(message))
            return "Message cannot be empty.";

        // Record as a user turn in the session history
        await _session.RecordUserTurnAsync(message).ConfigureAwait(false);
        return $"Message recorded at turn {_session.TurnIndex}.";
    }
}
