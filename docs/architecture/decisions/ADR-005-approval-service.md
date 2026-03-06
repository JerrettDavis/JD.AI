# ADR-005: IApprovalService — Human-in-the-Loop Workflow Approval

## Status

Accepted

## Context

As JD.AI supports increasingly autonomous agents that can invoke tools, execute
multi-step workflows, and interact with external systems, organizations require the
ability to insert human approval checkpoints into agent execution.

Without formal approval gates, agents operating under strict governance policies
may invoke sensitive operations (e.g., deploying infrastructure, calling external
APIs, running destructive commands) without any human review.

The approval system must:

1. Be opt-in — zero runtime overhead when not configured
2. Integrate with the existing `PolicySpec` / `PolicyResolver` stack
3. Support multiple implementations (auto-approve, auto-reject, console, webhook, etc.)
4. Be tested in isolation without UI or network dependencies

## Decision

We introduce three types:

### `ApprovalRequest`

A record capturing the approval context:

```csharp
public sealed record ApprovalRequest(
    string Id,
    string Description,
    string? Details = null,
    ApprovalKind Kind = ApprovalKind.Workflow,
    string? WorkflowName = null,
    string? ToolName = null,
    string? UserId = null);
```

### `ApprovalResult`

Returned by approval services:

```csharp
public sealed class ApprovalResult(ApprovalDecision decision, string? reason = null)
{
    public bool IsApproved => Decision == ApprovalDecision.Approved;
    public ApprovalDecision Decision { get; } = decision;
    public string? Reason { get; } = reason;

    public static ApprovalResult Approved()           => new(ApprovalDecision.Approved);
    public static ApprovalResult Rejected(string r)   => new(ApprovalDecision.Rejected, r);
    public static ApprovalResult TimedOut()           => new(ApprovalDecision.Timeout);
}
```

### `IApprovalService`

```csharp
public interface IApprovalService
{
    Task<ApprovalResult> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);
}
```

### Built-in Implementations

| Class | Behavior |
|-------|----------|
| `AutoApproveService` | Always returns `Approved`. Suitable for automation and testing. |
| `AutoRejectService` | Always returns `Rejected` with a configurable reason. Suitable for locked-down environments. |
| `PolicyBasedApprovalService` | Delegates to an inner service only when policy requires it; otherwise approves automatically. |

### Policy Integration

Two new policy flags drive `PolicyBasedApprovalService`:

- `WorkflowPolicy.RequireApprovalGate` (bool): If `true`, all workflow plans require approval before execution.
- `ToolPolicy.RequireApprovalFor` (IList\<string\>): Tool names that require explicit approval before invocation.

`PolicyResolver.Resolve()` merges these:
- `RequireApprovalGate`: any-wins semantics (one restrictive policy wins)
- `RequireApprovalFor`: union semantics (all listed tools aggregate across policies)

### AgentLoop Integration

`AgentLoop.RunWorkflowPlanningAsync()` checks `_session.ApprovalService` after
generating a plan text. If `IsApproved` is false, the method returns early with a
rejection message.

### Default at Startup

`GovernanceInitializer` registers:
- `AutoApproveService` as the default (no policy loaded, or print mode)
- `PolicyBasedApprovalService` (wrapping `AutoApproveService`) when policies are present

Future implementations can replace these with console-interactive, webhook-based,
or Slack-notification approval services.

## Consequences

### Positive

- Zero overhead when no `IApprovalService` is registered (null check)
- Policy-driven gates integrate with existing YAML policy files
- Easy to extend: implement `IApprovalService` and wire it in `GovernanceInitializer`
- Fully testable without UI or network

### Negative / Trade-offs

- `PolicyBasedApprovalService` wraps an inner service — the current default inner is
  `AutoApproveService`, meaning the gate triggers but immediately approves. A real
  human-in-the-loop implementation (console, webhook, etc.) must be substituted.
- `ToolName` matching uses `OrdinalIgnoreCase` — callers must supply consistent names.

## Related

- ADR-002: Tool Safety Tiers
- ADR-003: RBAC Policy Evaluation
- ADR-004: SQLite Audit Sink
- Issue #213: IApprovalService human-in-the-loop workflow approval
