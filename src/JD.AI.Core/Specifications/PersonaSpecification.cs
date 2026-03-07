using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class PersonaSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public PersonaMetadata Metadata { get; set; } = new();
    public string ActorType { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PersonaPermissions Permissions { get; set; } = new();
    public IList<PersonaTrustBoundary> TrustBoundaries { get; init; } = [];
    public IList<string> Responsibilities { get; init; } = [];
    public PersonaTraceability Trace { get; set; } = new();
}

public sealed class PersonaMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class PersonaPermissions
{
    public IList<string> Allowed { get; init; } = [];
    public IList<string> Denied { get; init; } = [];
}

public sealed class PersonaTrustBoundary
{
    public string Boundary { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
}

public sealed class PersonaTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public PersonaDownstreamTrace Downstream { get; set; } = new();
}

public sealed class PersonaDownstreamTrace
{
    public IList<string> Capabilities { get; init; } = [];
    public IList<string> Policies { get; init; } = [];
    public IList<string> Security { get; init; } = [];
    public IList<string> UseCases { get; init; } = [];
}

public sealed class PersonaSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<PersonaSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class PersonaSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class PersonaSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static PersonaSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<PersonaSpecification>(yaml);
    }

    public static PersonaSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static PersonaSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<PersonaSpecificationIndex>(yaml);
    }

    public static PersonaSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class PersonaSpecificationValidator
{
    private static readonly Regex PersonaIdPattern = new(
        "^persona\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CapabilityIdPattern = new(
        "^capability\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PolicyIdPattern = new(
        "^policy\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SecurityIdPattern = new(
        "^security\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UseCaseIdPattern = new(
        "^usecase\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedActorTypes =
    [
        "user",
        "administrator",
        "external_system",
        "automated_agent",
    ];

    private static readonly HashSet<string> AllowedAccessLevels =
    [
        "standard",
        "elevated",
        "restricted",
        "read_only",
    ];

    public static IReadOnlyList<string> Validate(PersonaSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new PersonaMetadata();
        var permissions = document.Permissions ?? new PersonaPermissions();
        var trustBoundaries = document.TrustBoundaries ?? [];
        var responsibilities = document.Responsibilities ?? [];
        var trace = document.Trace ?? new PersonaTraceability();
        var downstream = trace.Downstream ?? new PersonaDownstreamTrace();

        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Persona", StringComparison.Ordinal), "kind must be 'Persona'.", errors);
        Require(PersonaIdPattern.IsMatch(document.Id), "id must match persona.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(IsIsoDate(metadata.LastReviewed), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(AllowedActorTypes.Contains(document.ActorType), "actorType must be one of: user, administrator, external_system, automated_agent.", errors);
        Require(!string.IsNullOrWhiteSpace(document.RoleName), "roleName is required.", errors);
        Require(!string.IsNullOrWhiteSpace(document.Description), "description is required.", errors);
        RequireHasValues(permissions.Allowed, "permissions.allowed must contain at least one permission.", errors);
        Require(permissions.Denied.All(value => !string.IsNullOrWhiteSpace(value)), "permissions.denied entries must not be blank.", errors);
        Require(trustBoundaries.Count > 0, "trustBoundaries must contain at least one trust boundary.", errors);
        RequireHasValues(responsibilities, "responsibilities must contain at least one responsibility.", errors);

        for (var i = 0; i < trustBoundaries.Count; i++)
        {
            var boundary = trustBoundaries[i];
            Require(!string.IsNullOrWhiteSpace(boundary.Boundary), $"trustBoundaries[{i}].boundary is required.", errors);
            Require(AllowedAccessLevels.Contains(boundary.AccessLevel), $"trustBoundaries[{i}].accessLevel must be one of: standard, elevated, restricted, read_only.", errors);
            Require(!string.IsNullOrWhiteSpace(boundary.Justification), $"trustBoundaries[{i}].justification is required.", errors);
        }

        ValidateIds(downstream.Capabilities, CapabilityIdPattern, "trace.downstream.capabilities", errors);
        ValidateIds(downstream.Policies, PolicyIdPattern, "trace.downstream.policies", errors);
        ValidateIds(downstream.Security, SecurityIdPattern, "trace.downstream.security", errors);
        ValidateIds(downstream.UseCases, UseCaseIdPattern, "trace.downstream.useCases", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "personas", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "personas", "schema", "personas.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/personas/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/personas/schema/personas.schema.json.");

        PersonaSpecificationIndex index;
        try
        {
            index = PersonaSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/personas/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Persona index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "PersonaIndex", StringComparison.Ordinal), "Persona index kind must be 'PersonaIndex'.", errors);
        Require(index.Entries.Count > 0, "Persona index must contain at least one entry.", errors);

        var capabilityIds = LoadIds(repoRoot, "specs", "capabilities", "index.yaml");
        var policyIds = LoadIds(repoRoot, "specs", "policies", "index.yaml");
        var securityIds = LoadIds(repoRoot, "specs", "security", "index.yaml");
        var useCaseIds = LoadIds(repoRoot, "specs", "usecases", "index.yaml");

        foreach (var entry in index.Entries)
        {
            if (!PersonaIdPattern.IsMatch(entry.Id))
            {
                errors.Add($"Persona index entry id '{entry.Id}' does not match persona.<name> convention.");
                continue;
            }

            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Persona spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            PersonaSpecification spec;
            try
            {
                spec = PersonaSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse persona spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateUpstreamReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, errors);
            ValidateDownstreamReferences(capabilityIds, spec.Trace?.Downstream?.Capabilities ?? [], entry.Path, "specs/capabilities/index.yaml", "capability", errors);
            ValidateDownstreamReferences(policyIds, spec.Trace?.Downstream?.Policies ?? [], entry.Path, "specs/policies/index.yaml", "policy", errors);
            ValidateDownstreamReferences(securityIds, spec.Trace?.Downstream?.Security ?? [], entry.Path, "specs/security/index.yaml", "security", errors);
            ValidateDownstreamReferences(useCaseIds, spec.Trace?.Downstream?.UseCases ?? [], entry.Path, "specs/usecases/index.yaml", "use case", errors);
        }

        return errors;
    }

    private static HashSet<string> LoadIds(string repoRoot, params string[] relativePathParts)
    {
        var path = Path.Combine([repoRoot, .. relativePathParts]);
        if (!File.Exists(path))
            return [];

        try
        {
            var index = VisionSpecificationParser.ParseCapabilityIndexFile(path);
            return index.Entries
                .Select(entry => entry.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }

    private static void ValidateUpstreamReferences(string repoRoot, IList<string> references, string specPath, List<string> errors)
    {
        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                errors.Add($"{specPath}: trace.upstream entries must not be blank.");
                continue;
            }

            var fullPath = Path.Combine(repoRoot, reference.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                errors.Add($"{specPath}: trace.upstream reference '{reference}' does not resolve to a repository file.");
        }
    }

    private static void ValidateDownstreamReferences(
        HashSet<string> knownIds,
        IList<string> references,
        string specPath,
        string indexPath,
        string label,
        List<string> errors)
    {
        if (references.Count == 0)
            return;

        if (knownIds.Count == 0)
        {
            errors.Add($"{specPath}: trace.downstream.{label} references declared but {indexPath} is missing.");
            return;
        }

        foreach (var reference in references)
        {
            if (!knownIds.Contains(reference))
                errors.Add($"{specPath}: {label} reference '{reference}' was not found in {indexPath}.");
        }
    }

    private static void ValidateIds(IList<string> ids, Regex pattern, string fieldName, List<string> errors)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (!pattern.IsMatch(ids[i]))
                errors.Add($"{fieldName}[{i}] is invalid.");
        }
    }

    private static bool IsIsoDate(string value) =>
        DateOnly.TryParse(value, out _);

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
