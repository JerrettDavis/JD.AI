using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class PolicySpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public PolicyMetadata Metadata { get; set; } = new();
    public string PolicyType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public IList<string> Scope { get; init; } = [];
    public IList<PolicyRule> Rules { get; init; } = [];
    public IList<PolicyException> Exceptions { get; init; } = [];
    public PolicyEnforcement Enforcement { get; set; } = new();
    public PolicyTraceability Trace { get; set; } = new();
}

public sealed class PolicyMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class PolicyRule
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix -- domain model, not a System.Exception
public sealed class PolicyException
#pragma warning restore CA1711
{
    public string RuleId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ExpiresAt { get; set; } = string.Empty;
}

public sealed class PolicyEnforcement
{
    public string Mode { get; set; } = string.Empty;
}

public sealed class PolicyTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public PolicyDownstreamTrace Downstream { get; set; } = new();
}

public sealed class PolicyDownstreamTrace
{
    public IList<string> Ci { get; init; } = [];
    public IList<string> Enforcement { get; init; } = [];
    public IList<string> Operations { get; init; } = [];
}

public sealed class PolicySpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<PolicySpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class PolicySpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class PolicySpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static PolicySpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<PolicySpecification>(yaml);
    }

    public static PolicySpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static PolicySpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<PolicySpecificationIndex>(yaml);
    }

    public static PolicySpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class PolicySpecificationValidator
{
    private static readonly Regex PolicyIdPattern = new(
        "^policy\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedPolicyTypes =
    [
        "security",
        "compliance",
        "quality",
        "operational",
    ];

    private static readonly HashSet<string> AllowedSeverities =
    [
        "critical",
        "high",
        "medium",
        "low",
    ];

    private static readonly HashSet<string> AllowedEnforcementModes =
    [
        "enforce",
        "warn",
        "audit",
    ];

    public static IReadOnlyList<string> Validate(PolicySpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new PolicyMetadata();
        var enforcement = document.Enforcement ?? new PolicyEnforcement();
        var trace = document.Trace ?? new PolicyTraceability();
        var downstream = trace.Downstream ?? new PolicyDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Policy", StringComparison.Ordinal), "kind must be 'Policy'.", errors);
        Require(PolicyIdPattern.IsMatch(document.Id), "id must match policy.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(AllowedPolicyTypes.Contains(document.PolicyType), "policyType must be one of: security, compliance, quality, operational.", errors);
        Require(AllowedSeverities.Contains(document.Severity), "severity must be one of: critical, high, medium, low.", errors);
        RequireHasValues(document.Scope, "scope must contain at least one item.", errors);
        Require(document.Rules.Count > 0, "rules must contain at least one rule.", errors);

        for (var i = 0; i < document.Rules.Count; i++)
        {
            var rule = document.Rules[i] ?? new PolicyRule();
            var prefix = $"rules[{i}]";

            Require(!string.IsNullOrWhiteSpace(rule.Id), $"{prefix}.id is required.", errors);
            Require(!string.IsNullOrWhiteSpace(rule.Description), $"{prefix}.description is required.", errors);
        }

        Require(AllowedEnforcementModes.Contains(enforcement.Mode), "enforcement.mode must be one of: enforce, warn, audit.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.Ci.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.ci entries must not be blank.", errors);
        Require(downstream.Enforcement.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.enforcement entries must not be blank.", errors);
        Require(downstream.Operations.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.operations entries must not be blank.", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "policies", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "policies", "schema", "policies.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/policies/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/policies/schema/policies.schema.json.");

        PolicySpecificationIndex index;
        try
        {
            index = PolicySpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/policies/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Policy index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "PolicyIndex", StringComparison.Ordinal), "Policy index kind must be 'PolicyIndex'.", errors);
        Require(index.Entries.Count > 0, "Policy index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Policy spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            PolicySpecification spec;
            try
            {
                spec = PolicySpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse policy spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Ci ?? [], entry.Path, "trace.downstream.ci", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Enforcement ?? [], entry.Path, "trace.downstream.enforcement", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Operations ?? [], entry.Path, "trace.downstream.operations", errors);
        }

        return errors;
    }

    private static void ValidateFileReferences(string repoRoot, IList<string> paths, string specPath, string fieldName, List<string> errors)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errors.Add($"{specPath}: {fieldName} entries must not be blank.");
                continue;
            }

            var fullPath = Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                errors.Add($"{specPath}: {fieldName} reference '{path}' does not resolve to a repository file.");
        }
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition)
            errors.Add(message);
    }

    private static void RequireHasValues(IList<string>? values, string message, List<string> errors)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
            errors.Add(message);
    }
}
