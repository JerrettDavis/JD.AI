namespace JD.AI.Core.Governance;

public sealed class PolicyDocument
{
    public string ApiVersion { get; set; } = "jdai/v1";
    public string Kind { get; set; } = "Policy";
    public PolicyMetadata Metadata { get; set; } = new();
    public PolicySpec Spec { get; set; } = new();
}

public sealed class PolicyMetadata
{
    public string Name { get; set; } = string.Empty;
    public PolicyScope Scope { get; set; } = PolicyScope.User;

#pragma warning disable CA1805 // Explicitly initialized to default — intentional for clarity
    public int Priority { get; set; } = 0;
#pragma warning restore CA1805
}

public enum PolicyScope { Global, Organization, Team, Project, User }

public sealed class PolicySpec
{
    public ToolPolicy? Tools { get; set; }
    public ProviderPolicy? Providers { get; set; }
    public ModelPolicy? Models { get; set; }
    public BudgetPolicy? Budget { get; set; }
    public DataPolicy? Data { get; set; }
    public SessionPolicy? Sessions { get; set; }
    public AuditPolicy? Audit { get; set; }
    public WorkflowPolicy? Workflows { get; set; }
}

#pragma warning disable CA2227 // Settable collection properties required for YAML deserialization
public sealed class ToolPolicy
{
    public IList<string> Allowed { get; set; } = [];
    public IList<string> Denied { get; set; } = [];
}

public sealed class ProviderPolicy
{
    public IList<string> Allowed { get; set; } = [];
    public IList<string> Denied { get; set; } = [];
}

public sealed class ModelPolicy
{
    public int? MaxContextWindow { get; set; }
    public IList<string> Denied { get; set; } = [];
}

public sealed class BudgetPolicy
{
    public decimal? MaxDailyUsd { get; set; }
    public decimal? MaxMonthlyUsd { get; set; }

    /// <summary>Per-session budget limit set via <c>--max-budget-usd</c>.</summary>
    public decimal? MaxSessionUsd { get; set; }

    public int AlertThresholdPercent { get; set; } = 80;
}

public sealed class DataPolicy
{
    public IList<string> NoExternalProviders { get; set; } = [];
    public IList<string> RedactPatterns { get; set; } = [];
}
#pragma warning restore CA2227

public sealed class SessionPolicy
{
    public int? RetentionDays { get; set; }
    public bool RequireProjectTag { get; set; }
}

public sealed class AuditPolicy
{
    public bool Enabled { get; set; }
    public string Sink { get; set; } = "file";
    public string? Endpoint { get; set; }
    public string? Index { get; set; }
    public string? Token { get; set; }
    public string? Url { get; set; }
    public string? ConnectionString { get; set; }
    public string? Server { get; set; }
}

/// <summary>
/// Controls who can publish, install, and manage shared workflows.
/// Roles are compared against the current user identity (e.g. from Environment.UserName
/// or an org identity provider).
/// </summary>
public sealed class WorkflowPolicy
{
    /// <summary>
    /// Roles or usernames permitted to publish workflows.
    /// Empty means anyone can publish (no restriction).
    /// </summary>
    public IList<string> PublishAllowed { get; set; } = [];

    /// <summary>
    /// Roles or usernames explicitly denied from publishing.
    /// Deny takes precedence over allow.
    /// </summary>
    public IList<string> PublishDenied { get; set; } = [];
}
