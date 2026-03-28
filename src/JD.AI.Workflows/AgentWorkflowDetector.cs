namespace JD.AI.Workflows;

/// <summary>
/// Workflow detector that delegates to an <see cref="IPromptIntentClassifier"/>
/// for TF-IDF-based intent classification.
/// </summary>
public sealed class AgentWorkflowDetector : IAgentWorkflowDetector
{
    private readonly IPromptIntentClassifier _classifier;

    public AgentWorkflowDetector() : this(new TfIdfIntentClassifier()) { }
    public AgentWorkflowDetector(IPromptIntentClassifier classifier) => _classifier = classifier;

    /// <inheritdoc/>
    public bool IsWorkflowRequired(AgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return false;
        return _classifier.Classify(request.Message).IsWorkflow;
    }
}
