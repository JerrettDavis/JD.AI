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

    /// <summary>Version identifier for this policy document (e.g., "1.0.0").</summary>
    public string? Version { get; set; }

    /// <summary>
    /// When <c>true</c>, this policy's HMAC-SHA256 signature must be verified
    /// before the policy is applied. Unsigned or tampered policies are rejected.
    /// </summary>
    public bool RequireSignature { get; set; }
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
    public CircuitBreakerPolicy? CircuitBreaker { get; set; }
    public RolePolicy? Roles { get; set; }
}

#pragma warning disable CA2227 // Settable collection properties required for YAML deserialization
public sealed class ToolPolicy
{
    public IList<string> Allowed { get; set; } = [];
    public IList<string> Denied { get; set; } = [];

    /// <summary>
    /// Tool names that require explicit approval via <see cref="IApprovalService"/>
    /// before invocation, regardless of safety tier.
    /// </summary>
    public IList<string> RequireApprovalFor { get; set; } = [];
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
#pragma warning disable CA2227 // Settable collection properties required for YAML deserialization

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

    /// <summary>
    /// When <c>true</c>, workflow execution requires explicit approval from
    /// <see cref="IApprovalService"/> before any workflow step runs.
    /// </summary>
    public bool RequireApprovalGate { get; set; }

#pragma warning restore CA2227
}

/// <summary>
/// Configures tool loop detection and circuit breaker thresholds.
/// When <see cref="Hardened"/> is true, circuit breakers cannot be reset by users.
/// </summary>
public sealed class CircuitBreakerPolicy
{
    /// <summary>Number of identical tool+args calls that trigger a warning.</summary>
    public int RepetitionWarningThreshold { get; set; } = 3;

    /// <summary>Number of identical tool+args calls that trigger a hard stop.</summary>
    public int RepetitionHardStopThreshold { get; set; } = 5;

    /// <summary>Number of alternating A→B→A→B calls that constitutes a ping-pong loop.</summary>
    public int PingPongThreshold { get; set; } = 4;

    /// <summary>Rolling window of recent calls to inspect.</summary>
    public int WindowSize { get; set; } = 50;

    /// <summary>Seconds the circuit stays open before half-open probe.</summary>
    public int CooldownSeconds { get; set; } = 30;

    /// <summary>When true, circuit breakers cannot be disabled or reset by users.</summary>
    public bool Hardened { get; set; }
}

/// <summary>
/// RBAC role definitions. Maps role names to the additional policies they grant.
/// </summary>
public sealed class RolePolicy
{
#pragma warning disable CA2227
    /// <summary>
    /// Defines roles and which additional tool/provider/model permissions they confer.
    /// Key is the role name; value describes what the role permits.
    /// </summary>
    public IDictionary<string, RoleDefinition> Definitions { get; set; } = new Dictionary<string, RoleDefinition>();
#pragma warning restore CA2227
}

/// <summary>
/// Defines what a named role is allowed or denied beyond the base policy.
/// </summary>
public sealed class RoleDefinition
{
#pragma warning disable CA2227 // Settable collection properties required for YAML deserialization
    /// <summary>Optional roles this role inherits from (additive).</summary>
    public IList<string> Inherits { get; set; } = [];

    /// <summary>Additional tools allowed for this role (on top of base policy).</summary>
    public IList<string> AllowTools { get; set; } = [];

    /// <summary>Tools explicitly denied for this role regardless of base policy.</summary>
    public IList<string> DenyTools { get; set; } = [];

    /// <summary>Additional providers allowed for this role.</summary>
    public IList<string> AllowProviders { get; set; } = [];

    /// <summary>Providers denied for this role.</summary>
    public IList<string> DenyProviders { get; set; } = [];

    /// <summary>Additional models allowed for this role.</summary>
    public IList<string> AllowModels { get; set; } = [];

    /// <summary>Models denied for this role.</summary>
    public IList<string> DenyModels { get; set; } = [];
#pragma warning restore CA2227
}
