namespace JD.AI.Workflows;

/// <summary>
/// Rule-based, deterministic workflow detector.
/// A request is classified as workflow-worthy when it contains at least one
/// recognised lifecycle keyword and is substantive in length.
/// </summary>
public sealed class AgentWorkflowDetector : IAgentWorkflowDetector
{
    private static readonly string[] WorkflowKeywords =
    [
        "implement", "create", "scaffold", "build", "review",
        "test", "deploy", "generate", "design", "architect",
        "plan", "develop", "refactor", "migrate", "integrate",
        "setup", "initialize", "bootstrap",
    ];

    private const int MinMessageLength = 30;

    /// <inheritdoc/>
    public bool IsWorkflowRequired(AgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message) ||
            request.Message.Length < MinMessageLength)
            return false;

        var message = request.Message.AsSpan();
        foreach (var keyword in WorkflowKeywords)
        {
            if (message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
