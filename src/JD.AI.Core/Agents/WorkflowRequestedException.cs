namespace JD.AI.Core.Agents;

/// <summary>
/// Thrown by <see cref="IAutoFunctionInvocationFilter"/> implementations when a
/// tool call should be intercepted and routed through workflow planning instead
/// of executing ad-hoc. The <see cref="AgentLoop"/> catches this to enter
/// workflow generation mode.
/// </summary>
public sealed class WorkflowRequestedException : Exception
{
    /// <summary>The tool that triggered the workflow request.</summary>
    public string TriggeringTool { get; }

    public WorkflowRequestedException()
        : base("Workflow requested") => TriggeringTool = string.Empty;

    public WorkflowRequestedException(string triggeringTool)
        : base($"Workflow requested (triggered by tool: {triggeringTool})")
    {
        TriggeringTool = triggeringTool;
    }

    public WorkflowRequestedException(string message, Exception innerException)
        : base(message, innerException) => TriggeringTool = string.Empty;
}
