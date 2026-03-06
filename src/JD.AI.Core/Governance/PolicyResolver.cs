namespace JD.AI.Core.Governance;

/// <summary>
/// Merges multiple <see cref="PolicyDocument"/> instances into a single resolved
/// <see cref="PolicySpec"/> using conservative (most-restrictive) rules.
/// </summary>
public static class PolicyResolver
{
    /// <summary>
    /// Resolves a collection of policies into a single effective <see cref="PolicySpec"/>.
    /// </summary>
    /// <remarks>
    /// Merge rules:
    /// <list type="bullet">
    ///   <item>Policies are processed in scope order (Global → Organization → Team → Project → User)
    ///   then by ascending Priority.</item>
    ///   <item>Allowed lists: intersection (more restrictive wins).</item>
    ///   <item>Denied lists: union (any deny applies).</item>
    ///   <item>Numeric limits: minimum wins.</item>
    ///   <item>Budget: minimum of all limits; alert threshold minimum.</item>
    ///   <item>Sessions: requireProjectTag = any true wins; retention = minimum.</item>
    /// </list>
    /// </remarks>
    public static PolicySpec Resolve(IEnumerable<PolicyDocument> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        var ordered = policies
            .OrderBy(p => p.Metadata.Scope)
            .ThenBy(p => p.Metadata.Priority)
            .ToList();

        if (ordered.Count == 0)
            return new PolicySpec();

        if (ordered.Count == 1)
            return ordered[0].Spec;

        var result = new PolicySpec();

        var toolSpecs = ordered.Select(p => p.Spec.Tools).Where(t => t is not null).ToList();
        var providerSpecs = ordered.Select(p => p.Spec.Providers).Where(p => p is not null).ToList();
        var modelSpecs = ordered.Select(p => p.Spec.Models).Where(m => m is not null).ToList();
        var budgetSpecs = ordered.Select(p => p.Spec.Budget).Where(b => b is not null).ToList();
        var dataSpecs = ordered.Select(p => p.Spec.Data).Where(d => d is not null).ToList();
        var sessionSpecs = ordered.Select(p => p.Spec.Sessions).Where(s => s is not null).ToList();
        var auditSpecs = ordered.Select(p => p.Spec.Audit).Where(a => a is not null).ToList();
        var roleSpecs = ordered.Select(p => p.Spec.Roles).Where(r => r is not null).Select(r => r!).ToList();

        result.Tools = MergeToolPolicy(toolSpecs!);
        result.Providers = MergeProviderPolicy(providerSpecs!);
        result.Models = MergeModelPolicy(modelSpecs!);
        result.Budget = MergeBudgetPolicy(budgetSpecs!);
        result.Data = MergeDataPolicy(dataSpecs!);
        result.Sessions = MergeSessionPolicy(sessionSpecs!);
        result.Audit = MergeAuditPolicy(auditSpecs!);
        result.Roles = MergeRolePolicy(roleSpecs);

        return result;
    }

    private static ToolPolicy? MergeToolPolicy(List<ToolPolicy> specs)
    {
        if (specs.Count == 0)
            return null;

        var allAllowed = specs.Where(s => s.Allowed.Count > 0).Select(s => s.Allowed).ToList();
        var mergedAllowed = IntersectAllowedLists(allAllowed);
        var mergedDenied = UnionDeniedLists(specs.Select(s => s.Denied).ToList());

        return new ToolPolicy
        {
            Allowed = mergedAllowed,
            Denied = mergedDenied,
        };
    }

    private static ProviderPolicy? MergeProviderPolicy(List<ProviderPolicy> specs)
    {
        if (specs.Count == 0)
            return null;

        var allAllowed = specs.Where(s => s.Allowed.Count > 0).Select(s => s.Allowed).ToList();
        var mergedAllowed = IntersectAllowedLists(allAllowed);
        var mergedDenied = UnionDeniedLists(specs.Select(s => s.Denied).ToList());

        return new ProviderPolicy
        {
            Allowed = mergedAllowed,
            Denied = mergedDenied,
        };
    }

    private static ModelPolicy? MergeModelPolicy(List<ModelPolicy> specs)
    {
        if (specs.Count == 0)
            return null;

        var contextWindows = specs.Where(s => s.MaxContextWindow.HasValue)
                                  .Select(s => s.MaxContextWindow!.Value)
                                  .ToList();

        var mergedDenied = UnionDeniedLists(specs.Select(s => s.Denied).ToList());

        return new ModelPolicy
        {
            MaxContextWindow = contextWindows.Count > 0 ? contextWindows.Min() : null,
            Denied = mergedDenied,
        };
    }

    private static BudgetPolicy? MergeBudgetPolicy(List<BudgetPolicy> specs)
    {
        if (specs.Count == 0)
            return null;

        var dailyLimits = specs.Where(s => s.MaxDailyUsd.HasValue)
                               .Select(s => s.MaxDailyUsd!.Value)
                               .ToList();

        var monthlyLimits = specs.Where(s => s.MaxMonthlyUsd.HasValue)
                                 .Select(s => s.MaxMonthlyUsd!.Value)
                                 .ToList();

        var alertThresholds = specs.Select(s => s.AlertThresholdPercent).ToList();

        return new BudgetPolicy
        {
            MaxDailyUsd = dailyLimits.Count > 0 ? dailyLimits.Min() : null,
            MaxMonthlyUsd = monthlyLimits.Count > 0 ? monthlyLimits.Min() : null,
            AlertThresholdPercent = alertThresholds.Count > 0 ? alertThresholds.Min() : 80,
        };
    }

    private static DataPolicy? MergeDataPolicy(List<DataPolicy> specs)
    {
        if (specs.Count == 0)
            return null;

        var noExternal = specs.SelectMany(s => s.NoExternalProviders).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var redactPatterns = specs.SelectMany(s => s.RedactPatterns).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new DataPolicy
        {
            NoExternalProviders = noExternal,
            RedactPatterns = redactPatterns,
        };
    }

    private static SessionPolicy? MergeSessionPolicy(List<SessionPolicy> specs)
    {
        if (specs.Count == 0)
            return null;

        var retentions = specs.Where(s => s.RetentionDays.HasValue)
                              .Select(s => s.RetentionDays!.Value)
                              .ToList();

        return new SessionPolicy
        {
            RetentionDays = retentions.Count > 0 ? retentions.Min() : null,
            RequireProjectTag = specs.Any(s => s.RequireProjectTag),
        };
    }

    private static AuditPolicy? MergeAuditPolicy(List<AuditPolicy> specs)
    {
        if (specs.Count == 0)
            return null;

        // Use the last (most specific scope) audit policy as the base,
        // but enable if any policy enables it.
        var last = specs[^1];
        return new AuditPolicy
        {
            Enabled = specs.Any(a => a.Enabled),
            Sink = last.Sink,
            Endpoint = last.Endpoint,
            Index = last.Index,
            Token = last.Token,
            Url = last.Url,
            ConnectionString = last.ConnectionString,
            Server = last.Server,
        };
    }

    private static IList<string> IntersectAllowedLists(List<IList<string>> lists)
    {
        if (lists.Count == 0)
            return [];

        if (lists.Count == 1)
            return [.. lists[0]];

        var result = new HashSet<string>(lists[0], StringComparer.OrdinalIgnoreCase);
        foreach (var list in lists.Skip(1))
        {
            result.IntersectWith(new HashSet<string>(list, StringComparer.OrdinalIgnoreCase));
        }

        return [.. result];
    }

    private static IList<string> UnionDeniedLists(List<IList<string>> lists)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in lists)
        {
            foreach (var item in list)
                result.Add(item);
        }

        return [.. result];
    }

    /// <summary>
    /// Merges role policies additively: later (more specific) scopes can define additional
    /// roles or extend existing ones. All deny lists are unioned.
    /// </summary>
    private static RolePolicy? MergeRolePolicy(List<RolePolicy> specs)
    {
        if (specs.Count == 0)
            return null;

        var merged = new RolePolicy();
        foreach (var spec in specs)
        {
            foreach (var (roleName, def) in spec.Definitions)
            {
                if (!merged.Definitions.TryGetValue(roleName, out var existing))
                {
                    existing = new RoleDefinition();
                    merged.Definitions[roleName] = existing;
                }

                foreach (var item in def.AllowTools) existing.AllowTools.Add(item);
                foreach (var item in def.DenyTools) existing.DenyTools.Add(item);
                foreach (var item in def.AllowProviders) existing.AllowProviders.Add(item);
                foreach (var item in def.DenyProviders) existing.DenyProviders.Add(item);
                foreach (var item in def.AllowModels) existing.AllowModels.Add(item);
                foreach (var item in def.DenyModels) existing.DenyModels.Add(item);
                foreach (var item in def.Inherits) existing.Inherits.Add(item);
            }
        }

        return merged;
    }
}
