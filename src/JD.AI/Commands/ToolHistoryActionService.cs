using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Core.Config;
using JD.AI.Core.Sessions;
using JD.AI.Core.Tools;

namespace JD.AI.Commands;

internal enum ToolHistoryAction
{
    ViewDetails,
    AllowGlobal,
    AllowProject,
    DenyGlobal,
    DenyProject,
    RewindBefore,
    RewindAfter,
}

internal sealed record ToolHistoryActionRequest(bool RestoreLatestCheckpoint = false);

internal sealed class ToolHistoryActionService
{
    private readonly AgentSession _session;
    private readonly AtomicConfigStore? _configStore;
    private readonly ICheckpointStrategy? _checkpointStrategy;

    public ToolHistoryActionService(
        AgentSession session,
        AtomicConfigStore? configStore,
        ICheckpointStrategy? checkpointStrategy)
    {
        _session = session;
        _configStore = configStore;
        _checkpointStrategy = checkpointStrategy;
    }

    public async Task<string> ApplyAsync(
        ToolHistoryEntry entry,
        ToolHistoryAction action,
        ToolHistoryActionRequest? request = null,
        CancellationToken ct = default)
    {
        request ??= new ToolHistoryActionRequest();
        return action switch
        {
            ToolHistoryAction.ViewDetails => RenderToolHistoryEntryDetail(entry),
            ToolHistoryAction.AllowGlobal => await PersistToolPermissionAsync(entry, allow: true, projectScope: false, ct).ConfigureAwait(false),
            ToolHistoryAction.AllowProject => await PersistToolPermissionAsync(entry, allow: true, projectScope: true, ct).ConfigureAwait(false),
            ToolHistoryAction.DenyGlobal => await PersistToolPermissionAsync(entry, allow: false, projectScope: false, ct).ConfigureAwait(false),
            ToolHistoryAction.DenyProject => await PersistToolPermissionAsync(entry, allow: false, projectScope: true, ct).ConfigureAwait(false),
            ToolHistoryAction.RewindBefore => await RewindToToolUseAsync(entry, includeToolTurn: false, request.RestoreLatestCheckpoint, ct).ConfigureAwait(false),
            ToolHistoryAction.RewindAfter => await RewindToToolUseAsync(entry, includeToolTurn: true, request.RestoreLatestCheckpoint, ct).ConfigureAwait(false),
            _ => "No action applied.",
        };
    }

    private async Task<string> PersistToolPermissionAsync(
        ToolHistoryEntry entry,
        bool allow,
        bool projectScope,
        CancellationToken ct)
    {
        var canonical = OpenClawToolAliasResolver.Resolve(entry.ToolName);
        var projectPath = _session.SessionInfo?.ProjectPath ?? Directory.GetCurrentDirectory();
        if (_configStore is not null)
        {
            await _configStore.AddToolPermissionRuleAsync(
                canonical,
                allow,
                projectScope,
                projectPath,
                ct).ConfigureAwait(false);
        }

        if (allow)
            _session.ToolPermissionProfile.AddAllowed(canonical, projectScope);
        else
            _session.ToolPermissionProfile.AddDenied(canonical, projectScope);

        return $"{(allow ? "Allowed" : "Denied")} `{canonical}` in {(projectScope ? "project" : "global")} scope.";
    }

    private static string RenderToolHistoryEntryDetail(ToolHistoryEntry entry)
    {
        var summaryLines = TruncateLines(entry.Result, 2);
        var details = $"""
            Tool: {entry.ToolName}
            Session: {entry.SessionId}
            Turn: {entry.TurnIndex}
            Status: {entry.Status}
            Duration: {entry.DurationMs}ms
            Time: {entry.CreatedAt:g}
            Args: {entry.Arguments}
            Output:
            {summaryLines}
            """;
        return details;
    }

    private async Task<string> RewindToToolUseAsync(
        ToolHistoryEntry entry,
        bool includeToolTurn,
        bool restoreLatestCheckpoint,
        CancellationToken ct)
    {
        if (_session.Store is null || _session.SessionInfo is null)
            return "Session persistence not initialized.";

        if (!string.Equals(entry.SessionId, _session.SessionInfo.Id, StringComparison.Ordinal))
            return "Rewind is only supported for the current active session.";

        var targetTurn = includeToolTurn ? entry.TurnIndex : Math.Max(0, entry.TurnIndex - 1);

        await _session.Store.DeleteTurnsAfterAsync(_session.SessionInfo.Id, targetTurn).ConfigureAwait(false);
        while (_session.SessionInfo.Turns.Count > targetTurn + 1)
            _session.SessionInfo.Turns.RemoveAt(_session.SessionInfo.Turns.Count - 1);

        _session.History.Clear();
        if (!string.IsNullOrWhiteSpace(_session.OriginalSystemPrompt))
            _session.History.AddSystemMessage(_session.OriginalSystemPrompt);

        foreach (var turn in _session.SessionInfo.Turns)
        {
            if (string.Equals(turn.Role, "user", StringComparison.Ordinal))
                _session.History.AddUserMessage(turn.Content ?? string.Empty);
            else if (string.Equals(turn.Role, "assistant", StringComparison.Ordinal))
                _session.History.AddAssistantMessage(turn.Content ?? string.Empty);
        }

        if (!restoreLatestCheckpoint || _checkpointStrategy is null)
            return $"Rewound conversation to turn {targetTurn}.";

        var checkpoints = await _checkpointStrategy.ListAsync(ct).ConfigureAwait(false);
        if (checkpoints.Count == 0)
            return $"Rewound conversation to turn {targetTurn}.";

        var restored = await _checkpointStrategy.RestoreAsync(checkpoints[0].Id, ct).ConfigureAwait(false);
        return restored
            ? $"Rewound conversation to turn {targetTurn} and restored checkpoint {checkpoints[0].Id}."
            : $"Rewound conversation to turn {targetTurn}. Checkpoint restore failed.";
    }

    private static string TruncateLines(string? text, int maxLines)
    {
        var lines = (text ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries);
        if (lines.Length <= maxLines)
            return string.Join('\n', lines);

        return string.Join('\n', lines.Take(maxLines)) + "\n...";
    }
}

internal sealed record ToolHistoryEntry(
    string SessionId,
    string SessionName,
    int TurnIndex,
    DateTime CreatedAt,
    string ToolName,
    string Arguments,
    string Result,
    string Status,
    long DurationMs,
    string Label);
