using System.Text.RegularExpressions;

namespace JD.AI.Core.Governance;

/// <summary>
/// Evaluates tool, provider, and model requests against a resolved <see cref="PolicySpec"/>.
/// </summary>
public sealed class PolicyEvaluator : IPolicyEvaluator
{
    private readonly PolicySpec _policy;

    public PolicyEvaluator(PolicySpec policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    /// <inheritdoc/>
    public PolicySpec GetResolvedPolicy() => _policy;

    /// <inheritdoc/>
    public PolicyEvaluationResult EvaluateTool(string toolName, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(context);

        var tools = _policy.Tools;
        if (tools is null)
            return Allow();

        if (tools.Denied.Any(d => string.Equals(d, toolName, StringComparison.OrdinalIgnoreCase)))
            return Deny($"Tool '{toolName}' is in the denied list.");

        if (tools.Allowed.Count > 0 &&
            !tools.Allowed.Any(a => string.Equals(a, toolName, StringComparison.OrdinalIgnoreCase)))
        {
            return Deny($"Tool '{toolName}' is not in the allowed list.");
        }

        return Allow();
    }

    /// <inheritdoc/>
    public PolicyEvaluationResult EvaluateProvider(string providerName, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(providerName);
        ArgumentNullException.ThrowIfNull(context);

        var providers = _policy.Providers;
        if (providers is null)
            return Allow();

        if (providers.Denied.Any(d => string.Equals(d, providerName, StringComparison.OrdinalIgnoreCase)))
            return Deny($"Provider '{providerName}' is in the denied list.");

        if (providers.Allowed.Count > 0 &&
            !providers.Allowed.Any(a => string.Equals(a, providerName, StringComparison.OrdinalIgnoreCase)))
        {
            return Deny($"Provider '{providerName}' is not in the allowed list.");
        }

        return Allow();
    }

    /// <inheritdoc/>
    public PolicyEvaluationResult EvaluateModel(string modelId, int? contextWindow, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(context);

        var models = _policy.Models;
        if (models is null)
            return Allow();

        if (models.Denied.Any(pattern => MatchesGlob(modelId, pattern)))
            return Deny($"Model '{modelId}' matches a denied pattern.");

        if (contextWindow.HasValue && models.MaxContextWindow.HasValue &&
            contextWindow.Value > models.MaxContextWindow.Value)
        {
            return Deny(
                $"Model context window {contextWindow.Value} exceeds maximum allowed {models.MaxContextWindow.Value}.");
        }

        return Allow();
    }

    /// <inheritdoc/>
    public PolicyEvaluationResult EvaluateWorkflowPublish(PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var workflows = _policy.Workflows;
        if (workflows is null)
            return Allow();

        var userId = context.UserId ?? Environment.UserName;

        // Deny takes precedence
        if (workflows.PublishDenied.Any(d =>
            string.Equals(d, userId, StringComparison.OrdinalIgnoreCase)))
        {
            return Deny($"User '{userId}' is denied from publishing workflows.");
        }

        // If allow list is configured, user must be on it
        if (workflows.PublishAllowed.Count > 0 &&
            !workflows.PublishAllowed.Any(a =>
                string.Equals(a, userId, StringComparison.OrdinalIgnoreCase)))
        {
            return Deny($"User '{userId}' is not in the workflow publish allowed list.");
        }

        return Allow();
    }

    private static PolicyEvaluationResult Allow() =>
        new(PolicyDecision.Allow);

    private static PolicyEvaluationResult Deny(string reason) =>
        new(PolicyDecision.Deny, reason);

    /// <summary>
    /// Matches a value against a glob pattern supporting <c>*</c> (any characters)
    /// and <c>?</c> (single character).
    /// </summary>
    private static bool MatchesGlob(string value, string pattern)
    {
        if (string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        // Convert glob to regex: * -> .*, ? -> .
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";

        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }
}
