using System.Text.RegularExpressions;

namespace JD.AI.Core.Governance;

/// <summary>
/// Evaluates tool, provider, and model requests against a resolved <see cref="PolicySpec"/>.
/// Role-based grants are applied AFTER base policy evaluation: a role-specific allow
/// can override a base-policy deny, while a role-specific deny always wins.
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

        // Role-level deny always wins first
        var roleDef = GetEffectiveRoleDefinition(context);
        if (roleDef is not null &&
            roleDef.DenyTools.Any(d => MatchesGlob(toolName, d)))
        {
            return Deny($"Tool '{toolName}' is denied by role '{context.RoleName}'.");
        }

        var tools = _policy.Tools;
        if (tools is not null)
        {
            if (tools.Denied.Any(d => MatchesGlob(toolName, d)))
            {
                // Role can override a base deny
                if (roleDef?.AllowTools.Any(a => MatchesGlob(toolName, a)) == true)
                    return Allow();

                return Deny($"Tool '{toolName}' is in the denied list.");
            }

            if (tools.Allowed.Count > 0 &&
                !tools.Allowed.Any(a => MatchesGlob(toolName, a)))
            {
                // Role can extend the allowed list
                if (roleDef?.AllowTools.Any(a => MatchesGlob(toolName, a)) == true)
                    return Allow();

                return Deny($"Tool '{toolName}' is not in the allowed list.");
            }
        }

        return Allow();
    }

    /// <inheritdoc/>
    public PolicyEvaluationResult EvaluateProvider(string providerName, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(providerName);
        ArgumentNullException.ThrowIfNull(context);

        var roleDef = GetEffectiveRoleDefinition(context);
        if (roleDef is not null &&
            roleDef.DenyProviders.Any(d => MatchesGlob(providerName, d)))
        {
            return Deny($"Provider '{providerName}' is denied by role '{context.RoleName}'.");
        }

        var providers = _policy.Providers;
        if (providers is not null)
        {
            if (providers.Denied.Any(d => MatchesGlob(providerName, d)))
            {
                if (roleDef?.AllowProviders.Any(a => MatchesGlob(providerName, a)) == true)
                    return Allow();
                return Deny($"Provider '{providerName}' is in the denied list.");
            }

            if (providers.Allowed.Count > 0 &&
                !providers.Allowed.Any(a => MatchesGlob(providerName, a)))
            {
                if (roleDef?.AllowProviders.Any(a => MatchesGlob(providerName, a)) == true)
                    return Allow();
                return Deny($"Provider '{providerName}' is not in the allowed list.");
            }
        }

        return Allow();
    }

    /// <inheritdoc/>
    public PolicyEvaluationResult EvaluateModel(string modelId, int? contextWindow, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(context);

        var roleDef = GetEffectiveRoleDefinition(context);
        if (roleDef is not null &&
            roleDef.DenyModels.Any(d => MatchesGlob(modelId, d)))
        {
            return Deny($"Model '{modelId}' is denied by role '{context.RoleName}'.");
        }

        var models = _policy.Models;
        if (models is not null)
        {
            if (models.Denied.Any(pattern => MatchesGlob(modelId, pattern)))
            {
                if (roleDef?.AllowModels.Any(a => MatchesGlob(modelId, a)) == true)
                    return Allow();
                return Deny($"Model '{modelId}' matches a denied pattern.");
            }

            if (contextWindow.HasValue && models.MaxContextWindow.HasValue &&
                contextWindow.Value > models.MaxContextWindow.Value)
            {
                return Deny(
                    $"Model context window {contextWindow.Value} exceeds maximum allowed {models.MaxContextWindow.Value}.");
            }
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

        // If allow list is configured, user must be on it (or they have admin role)
        if (workflows.PublishAllowed.Count > 0 &&
            !workflows.PublishAllowed.Any(a =>
                string.Equals(a, userId, StringComparison.OrdinalIgnoreCase)))
        {
            // Check if role grants workflow publish
            var roleDef = GetEffectiveRoleDefinition(context);
            if (roleDef is null)
                return Deny($"User '{userId}' is not in the workflow publish allowed list.");
        }

        return Allow();
    }

    private static PolicyEvaluationResult Allow() =>
        new(PolicyDecision.Allow);

    private static PolicyEvaluationResult Deny(string reason) =>
        new(PolicyDecision.Deny, reason);

    /// <summary>
    /// Resolves the effective merged <see cref="RoleDefinition"/> for the current context,
    /// walking the <c>Inherits</c> chain additively.
    /// Returns <c>null</c> when no role policy is configured or no role is set on the context.
    /// </summary>
    private RoleDefinition? GetEffectiveRoleDefinition(PolicyContext context)
    {
        if (_policy.Roles is null || context.RoleName is null)
            return null;

        return MergeRoleChain(context.RoleName, _policy.Roles.Definitions, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static RoleDefinition? MergeRoleChain(
        string roleName,
        IDictionary<string, RoleDefinition> definitions,
        HashSet<string> visited)
    {
        if (!definitions.TryGetValue(roleName, out var def))
            return null;

        if (!visited.Add(roleName))
            return null; // cycle guard

        var merged = new RoleDefinition
        {
            AllowTools = [..def.AllowTools],
            DenyTools = [..def.DenyTools],
            AllowProviders = [..def.AllowProviders],
            DenyProviders = [..def.DenyProviders],
            AllowModels = [..def.AllowModels],
            DenyModels = [..def.DenyModels],
        };

        foreach (var parentName in def.Inherits)
        {
            var parent = MergeRoleChain(parentName, definitions, visited);
            if (parent is null) continue;

            foreach (var t in parent.AllowTools) merged.AllowTools.Add(t);
            foreach (var t in parent.DenyTools) merged.DenyTools.Add(t);
            foreach (var p in parent.AllowProviders) merged.AllowProviders.Add(p);
            foreach (var p in parent.DenyProviders) merged.DenyProviders.Add(p);
            foreach (var m in parent.AllowModels) merged.AllowModels.Add(m);
            foreach (var m in parent.DenyModels) merged.DenyModels.Add(m);
        }

        return merged;
    }

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
