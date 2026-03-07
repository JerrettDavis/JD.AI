namespace JD.AI.Workflows.Security;

/// <summary>
/// Validates workflow integrity by checking signatures and publisher trust.
/// Used as a gate before workflow execution or installation.
/// </summary>
public sealed class WorkflowIntegrityValidator
{
    private readonly TrustedPublisherRegistry? _trustRegistry;
    private readonly byte[]? _signingKey;

    /// <param name="signingKey">HMAC key for signature verification. Null to skip signature checks.</param>
    /// <param name="trustRegistry">Publisher trust registry. Null to skip trust checks.</param>
    public WorkflowIntegrityValidator(
        byte[]? signingKey = null,
        TrustedPublisherRegistry? trustRegistry = null)
    {
        _signingKey = signingKey;
        _trustRegistry = trustRegistry;
    }

    /// <summary>
    /// Validates a workflow definition and its metadata.
    /// Returns a result indicating whether the workflow is safe to execute.
    /// </summary>
    public WorkflowValidationResult Validate(
        AgentWorkflowDefinition definition,
        string? author = null,
        string? signature = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var issues = new List<string>();

        // Schema validation
        if (string.IsNullOrWhiteSpace(definition.Name))
            issues.Add("Workflow name is required");

        if (string.IsNullOrWhiteSpace(definition.Version))
            issues.Add("Workflow version is required");

        if (definition.Steps.Count == 0)
            issues.Add("Workflow must have at least one step");

        // Validate step integrity
        ValidateSteps(definition.Steps, issues, depth: 0);

        // Signature verification
        if (_signingKey is not null)
        {
            if (string.IsNullOrWhiteSpace(signature))
            {
                issues.Add("Workflow is unsigned but signature verification is required");
            }
            else if (!WorkflowSignature.Verify(definition, _signingKey, signature))
            {
                issues.Add("Workflow signature is invalid — content may have been tampered with");
            }
        }

        // Publisher trust check
        if (_trustRegistry is not null && !string.IsNullOrWhiteSpace(author))
        {
            if (!_trustRegistry.IsTrusted(author))
                issues.Add($"Publisher '{author}' is not in the trusted publisher registry");
        }

        return new WorkflowValidationResult
        {
            IsValid = issues.Count == 0,
            Issues = issues.AsReadOnly(),
            WorkflowName = definition.Name,
            WorkflowVersion = definition.Version,
        };
    }

    private static void ValidateSteps(
        IList<AgentStepDefinition> steps, List<string> issues, int depth)
    {
        if (depth > 10)
        {
            issues.Add("Workflow step nesting exceeds maximum depth of 10");
            return;
        }

        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Name))
                issues.Add($"Step at depth {depth} has no name");

            if (step.Kind is AgentStepKind.Tool or AgentStepKind.Skill or AgentStepKind.Nested
                && string.IsNullOrWhiteSpace(step.Target))
            {
                issues.Add($"Step '{step.Name}' of kind {step.Kind} requires a target");
            }

            if (step.Kind is AgentStepKind.Loop or AgentStepKind.Conditional
                && string.IsNullOrWhiteSpace(step.Condition))
            {
                issues.Add($"Step '{step.Name}' of kind {step.Kind} requires a condition");
            }

            if (step.SubSteps.Count > 0)
                ValidateSteps(step.SubSteps, issues, depth + 1);
        }
    }
}

/// <summary>
/// Result of workflow integrity validation.
/// </summary>
public sealed class WorkflowValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<string> Issues { get; init; }
    public string? WorkflowName { get; init; }
    public string? WorkflowVersion { get; init; }
}
