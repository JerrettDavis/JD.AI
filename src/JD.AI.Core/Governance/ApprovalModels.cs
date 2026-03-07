namespace JD.AI.Core.Governance;

/// <summary>
/// Represents a request for human or system approval before performing a sensitive operation.
/// </summary>
/// <param name="Id">Unique identifier for this request.</param>
/// <param name="Description">Short description of the action requiring approval.</param>
/// <param name="Details">Optional extended description or context.</param>
/// <param name="Kind">The kind of operation requiring approval.</param>
/// <param name="WorkflowName">When the request originates from a workflow, the workflow name.</param>
/// <param name="ToolName">When the request originates from a tool call, the tool name.</param>
/// <param name="UserId">Optional user identity associated with the request.</param>
public sealed record ApprovalRequest(
    string Id,
    string Description,
    string? Details = null,
    ApprovalKind Kind = ApprovalKind.Workflow,
    string? WorkflowName = null,
    string? ToolName = null,
    string? UserId = null);

/// <summary>Kind of operation that triggered an approval request.</summary>
public enum ApprovalKind
{
    /// <summary>A structured workflow is about to begin execution.</summary>
    Workflow,

    /// <summary>A sensitive tool call requires user confirmation.</summary>
    ToolCall,

    /// <summary>An operation that accesses external data sources.</summary>
    DataAccess,

    /// <summary>An operation that communicates with external APIs or services.</summary>
    ExternalRequest,

    /// <summary>An operation that modifies files or the file system.</summary>
    FileSystem,
}

/// <summary>Outcome of an approval request.</summary>
public enum ApprovalDecision
{
    /// <summary>The operation is approved and may proceed.</summary>
    Approved,

    /// <summary>The operation was rejected and must not proceed.</summary>
    Rejected,

    /// <summary>The approval request timed out without a response.</summary>
    Timeout,
}

/// <summary>Result of an <see cref="IApprovalService.RequestApprovalAsync"/> call.</summary>
public sealed record ApprovalResult(
    ApprovalDecision Decision,
    string? Reason = null)
{
    /// <summary>Returns <see cref="ApprovalDecision.Approved"/> with no reason.</summary>
    public static ApprovalResult Approved() => new(ApprovalDecision.Approved);

    /// <summary>Returns <see cref="ApprovalDecision.Rejected"/> with the given reason.</summary>
    public static ApprovalResult Rejected(string reason) => new(ApprovalDecision.Rejected, reason);

    /// <summary>Returns <see cref="ApprovalDecision.Timeout"/> with no reason.</summary>
    public static ApprovalResult TimedOut() => new(ApprovalDecision.Timeout);

    /// <summary>Whether the decision is <see cref="ApprovalDecision.Approved"/>.</summary>
    public bool IsApproved => Decision == ApprovalDecision.Approved;
}
