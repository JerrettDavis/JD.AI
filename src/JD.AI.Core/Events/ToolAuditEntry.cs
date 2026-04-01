using JD.AI.Core.Agents;

namespace JD.AI.Core.Events;

/// <summary>
/// Audit entry emitted via IEventBus for every tool execution decision made by
/// the permission evaluator. Stored in the in-memory event ring buffer and
/// surfaced in the Dashboard Logs page.
/// </summary>
public sealed record ToolAuditEntry(
    string ToolName,
    string? Arguments,
    string? Result,
    string Decision,
    long DurationMs,
    string SessionId,
    DateTimeOffset Timestamp) : GatewayEvent(
        "tool.audit",
        SessionId,
        Timestamp,
        Payload: null)
{
    /// <summary>Factory method that maps a <see cref="ToolExecutionGateDecision"/> to a human-readable string.</summary>
    public static ToolAuditEntry Create(
        string toolName,
        string? arguments,
        string? result,
        ToolExecutionGateDecision decision,
        long durationMs,
        string sessionId) => new(
            toolName,
            arguments,
            result,
            decision switch
            {
                ToolExecutionGateDecision.Blocked => "Denied",
                ToolExecutionGateDecision.AllowWithoutPrompt => "Allowed",
                _ => "Prompted"
            },
            durationMs,
            sessionId,
            DateTimeOffset.UtcNow);
}
