using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class SecuritySpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public SecurityMetadata Metadata { get; set; } = new();
    public string AuthnModel { get; set; } = string.Empty;
    public string AuthzModel { get; set; } = string.Empty;
    public IList<SecurityTrustZone> TrustZones { get; init; } = [];
    public IList<SecurityThreat> Threats { get; init; } = [];
    public IList<SecurityControl> Controls { get; init; } = [];
    public IList<SecurityResidualRisk> ResidualRisks { get; init; } = [];
    public SecurityTraceability Trace { get; set; } = new();
}

public sealed class SecurityMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class SecurityTrustZone
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
}

public sealed class SecurityThreat
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public IList<string> MitigatedBy { get; init; } = [];
}

public sealed class SecurityControl
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class SecurityResidualRisk
{
    public string ThreatId { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
}

public sealed class SecurityTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public SecurityDownstreamTrace Downstream { get; set; } = new();
}

public sealed class SecurityDownstreamTrace
{
    public IList<string> Deployment { get; init; } = [];
    public IList<string> Operations { get; init; } = [];
    public IList<string> Testing { get; init; } = [];
}

public sealed class SecuritySpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<SecuritySpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class SecuritySpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class SecuritySpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static SecuritySpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<SecuritySpecification>(yaml);
    }

    public static SecuritySpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static SecuritySpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<SecuritySpecificationIndex>(yaml);
    }

    public static SecuritySpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class SecuritySpecificationValidator
{
    private static readonly Regex SecurityIdPattern = new(
        "^security\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedAuthnModels =
    [
        "oauth2",
        "oidc",
        "api-key",
        "mtls",
        "saml",
        "none",
    ];

    private static readonly HashSet<string> AllowedAuthzModels =
    [
        "rbac",
        "abac",
        "pbac",
        "acl",
        "none",
    ];

    private static readonly HashSet<string> AllowedTrustLevels =
    [
        "public",
        "dmz",
        "internal",
        "restricted",
    ];

    private static readonly HashSet<string> AllowedThreatSeverities =
    [
        "critical",
        "high",
        "medium",
        "low",
    ];

    private static readonly HashSet<string> AllowedControlTypes =
    [
        "preventive",
        "detective",
        "corrective",
    ];

    public static IReadOnlyList<string> Validate(SecuritySpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new SecurityMetadata();
        var trace = document.Trace ?? new SecurityTraceability();
        var downstream = trace.Downstream ?? new SecurityDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Security", StringComparison.Ordinal), "kind must be 'Security'.", errors);
        Require(SecurityIdPattern.IsMatch(document.Id), "id must match security.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(AllowedAuthnModels.Contains(document.AuthnModel), "authnModel must be one of: oauth2, oidc, api-key, mtls, saml, none.", errors);
        Require(AllowedAuthzModels.Contains(document.AuthzModel), "authzModel must be one of: rbac, abac, pbac, acl, none.", errors);
        Require(document.TrustZones.Count > 0, "trustZones must contain at least one trust zone.", errors);
        Require(document.Threats.Count > 0, "threats must contain at least one threat.", errors);
        Require(document.Controls.Count > 0, "controls must contain at least one control.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.Deployment.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.deployment entries must not be blank.", errors);
        Require(downstream.Operations.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.operations entries must not be blank.", errors);
        Require(downstream.Testing.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.testing entries must not be blank.", errors);

        ValidateTrustZones(document.TrustZones, errors);
        ValidateThreats(document.Threats, errors);
        ValidateControls(document.Controls, errors);
        ValidateResidualRisks(document.ResidualRisks, errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "security", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "security", "schema", "security.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/security/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/security/schema/security.schema.json.");

        SecuritySpecificationIndex index;
        try
        {
            index = SecuritySpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/security/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Security index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "SecurityIndex", StringComparison.Ordinal), "Security index kind must be 'SecurityIndex'.", errors);
        Require(index.Entries.Count > 0, "Security index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Security spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            SecuritySpecification spec;
            try
            {
                spec = SecuritySpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse security spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Deployment ?? [], entry.Path, "trace.downstream.deployment", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Operations ?? [], entry.Path, "trace.downstream.operations", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Testing ?? [], entry.Path, "trace.downstream.testing", errors);
        }

        return errors;
    }

    private static void ValidateTrustZones(IList<SecurityTrustZone> trustZones, List<string> errors)
    {
        for (var i = 0; i < trustZones.Count; i++)
        {
            var zone = trustZones[i] ?? new SecurityTrustZone();
            var prefix = $"trustZones[{i}]";

            Require(!string.IsNullOrWhiteSpace(zone.Name), $"{prefix}.name is required.", errors);
            Require(AllowedTrustLevels.Contains(zone.Level), $"{prefix}.level must be one of: public, dmz, internal, restricted.", errors);
        }
    }

    private static void ValidateThreats(IList<SecurityThreat> threats, List<string> errors)
    {
        for (var i = 0; i < threats.Count; i++)
        {
            var threat = threats[i] ?? new SecurityThreat();
            var prefix = $"threats[{i}]";

            Require(!string.IsNullOrWhiteSpace(threat.Id), $"{prefix}.id is required.", errors);
            Require(!string.IsNullOrWhiteSpace(threat.Description), $"{prefix}.description is required.", errors);
            Require(AllowedThreatSeverities.Contains(threat.Severity), $"{prefix}.severity must be one of: critical, high, medium, low.", errors);
        }
    }

    private static void ValidateControls(IList<SecurityControl> controls, List<string> errors)
    {
        for (var i = 0; i < controls.Count; i++)
        {
            var control = controls[i] ?? new SecurityControl();
            var prefix = $"controls[{i}]";

            Require(!string.IsNullOrWhiteSpace(control.Id), $"{prefix}.id is required.", errors);
            Require(!string.IsNullOrWhiteSpace(control.Description), $"{prefix}.description is required.", errors);
            Require(AllowedControlTypes.Contains(control.Type), $"{prefix}.type must be one of: preventive, detective, corrective.", errors);
        }
    }

    private static void ValidateResidualRisks(IList<SecurityResidualRisk> risks, List<string> errors)
    {
        for (var i = 0; i < risks.Count; i++)
        {
            var risk = risks[i] ?? new SecurityResidualRisk();
            var prefix = $"residualRisks[{i}]";

            Require(!string.IsNullOrWhiteSpace(risk.ThreatId), $"{prefix}.threatId is required.", errors);
            Require(!string.IsNullOrWhiteSpace(risk.Justification), $"{prefix}.justification is required.", errors);
        }
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
