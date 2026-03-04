namespace JD.AI.Core.Governance;

public enum PolicyDecision { Allow, Deny, RequireApproval, Audit }

public sealed record PolicyEvaluationResult(
    PolicyDecision Decision,
    string? Reason = null,
    string? PolicyName = null);

public sealed record PolicyContext(
    string? UserId = null,
    string? ProjectPath = null,
    string? ProviderName = null,
    string? ModelId = null);

public interface IPolicyEvaluator
{
    PolicyEvaluationResult EvaluateTool(string toolName, PolicyContext context);
    PolicyEvaluationResult EvaluateProvider(string providerName, PolicyContext context);
    PolicyEvaluationResult EvaluateModel(string modelId, int? contextWindow, PolicyContext context);

    /// <summary>Evaluates whether the current user can publish workflows.</summary>
    PolicyEvaluationResult EvaluateWorkflowPublish(PolicyContext context);

    PolicySpec GetResolvedPolicy();
}
