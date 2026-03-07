using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class CapabilitySpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public CapabilityMetadata Metadata { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Maturity { get; set; } = string.Empty;
    public List<string> Actors { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
    public List<string> RelatedUseCases { get; set; } = [];
    public CapabilityTraceability Trace { get; set; } = new();
}

public sealed class CapabilityMetadata
{
    public List<string> Owners { get; set; } = [];
    public List<string> Reviewers { get; set; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class CapabilityTraceability
{
    public List<string> VisionRefs { get; set; } = [];
    public List<string> Upstream { get; set; } = [];
    public CapabilityDownstreamTrace Downstream { get; set; } = new();
}

public sealed class CapabilityDownstreamTrace
{
    public List<string> UseCases { get; set; } = [];
    public List<string> Architecture { get; set; } = [];
    public List<string> Testing { get; set; } = [];
}

public static class CapabilitySpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static CapabilitySpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<CapabilitySpecification>(yaml);
    }

    public static CapabilitySpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static CapabilitySpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<CapabilitySpecificationIndex>(yaml);
    }

    public static CapabilitySpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class CapabilitySpecificationValidator
{
    private static readonly Regex CapabilityIdPattern = new(
        "^capability\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PersonaIdPattern = new(
        "^persona\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
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

    private static readonly HashSet<string> AllowedMaturity =
    [
        "emerging",
        "beta",
        "ga",
        "deprecated",
    ];

    public static IReadOnlyList<string> Validate(CapabilitySpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new CapabilityMetadata();
        var trace = document.Trace ?? new CapabilityTraceability();
        var downstream = trace.Downstream ?? new CapabilityDownstreamTrace();

        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Capability", StringComparison.Ordinal), "kind must be 'Capability'.", errors);
        Require(CapabilityIdPattern.IsMatch(document.Id), "id must match capability.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(!string.IsNullOrWhiteSpace(document.Name), "name is required.", errors);
        Require(!string.IsNullOrWhiteSpace(document.Description), "description is required.", errors);
        Require(AllowedMaturity.Contains(document.Maturity), "maturity must be one of: emerging, beta, ga, deprecated.", errors);
        RequireHasValues(document.Actors, "actors must contain at least one actor.", errors);

        ValidateIds(document.Actors, PersonaIdPattern, "actors", errors);
        ValidateIds(document.Dependencies, CapabilityIdPattern, "dependencies", errors);
        ValidateIds(document.RelatedUseCases, UseCaseIdPattern, "relatedUseCases", errors);
        ValidateIds(trace.VisionRefs, new Regex("^vision\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant), "trace.visionRefs", errors);
        ValidateIds(downstream.UseCases, UseCaseIdPattern, "trace.downstream.useCases", errors);

        Require(downstream.Architecture.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.architecture entries must not be blank.", errors);
        Require(downstream.Testing.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.testing entries must not be blank.", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "capabilities", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "capabilities", "schema", "capabilities.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/capabilities/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/capabilities/schema/capabilities.schema.json.");

        CapabilitySpecificationIndex index;
        try
        {
            index = CapabilitySpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/capabilities/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Capability index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "CapabilityIndex", StringComparison.Ordinal), "Capability index kind must be 'CapabilityIndex'.", errors);
        Require(index.Entries.Count > 0, "Capability index must contain at least one entry.", errors);

        var knownCapabilityIds = index.Entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var knownVisionIds = LoadVisionIds(repoRoot, errors);
        var knownUseCaseIds = LoadIds(repoRoot, Path.Combine("specs", "usecases", "index.yaml"));

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Capability spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            CapabilitySpecification spec;
            try
            {
                spec = CapabilitySpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse capability spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateVisionReferences(knownVisionIds, spec.Trace?.VisionRefs ?? [], entry.Path, errors);
            ValidateCapabilityReferences(knownCapabilityIds, spec.Dependencies ?? [], spec.Id, entry.Path, errors);
            ValidateUseCaseReferences(knownUseCaseIds, spec.RelatedUseCases ?? [], entry.Path, "relatedUseCases", errors);
            ValidateUseCaseReferences(knownUseCaseIds, spec.Trace?.Downstream?.UseCases ?? [], entry.Path, "trace.downstream.useCases", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Architecture ?? [], entry.Path, "trace.downstream.architecture", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Testing ?? [], entry.Path, "trace.downstream.testing", errors);
        }

        return errors;
    }

    private static HashSet<string> LoadVisionIds(string repoRoot, List<string> errors)
    {
        var path = Path.Combine(repoRoot, "specs", "vision", "index.yaml");
        if (!File.Exists(path))
        {
            errors.Add("Missing specs/vision/index.yaml required for capability trace validation.");
            return [];
        }

        try
        {
            var index = VisionSpecificationParser.ParseIndexFile(path);
            return index.Entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/vision/index.yaml: {ex.Message}");
            return [];
        }
    }

    private static HashSet<string> LoadIds(string repoRoot, string relativePath)
    {
        var path = Path.Combine(repoRoot, relativePath);
        if (!File.Exists(path))
            return [];

        try
        {
            var index = CapabilitySpecificationParser.ParseIndexFile(path);
            return index.Entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }

    private static void ValidateIds(List<string> ids, Regex pattern, string fieldName, List<string> errors)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (!pattern.IsMatch(ids[i]))
                errors.Add($"{fieldName}[{i}] is invalid.");
        }
    }

    private static void ValidateFileReferences(string repoRoot, List<string> paths, string specPath, string fieldName, List<string> errors)
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

    private static void ValidateVisionReferences(HashSet<string> knownIds, List<string> ids, string specPath, List<string> errors)
    {
        foreach (var id in ids)
        {
            if (!knownIds.Contains(id))
                errors.Add($"{specPath}: vision reference '{id}' was not found in specs/vision/index.yaml.");
        }
    }

    private static void ValidateCapabilityReferences(HashSet<string> knownIds, List<string> ids, string selfId, string specPath, List<string> errors)
    {
        foreach (var id in ids)
        {
            if (string.Equals(id, selfId, StringComparison.Ordinal))
            {
                errors.Add($"{specPath}: capability cannot depend on itself.");
                continue;
            }

            if (!knownIds.Contains(id))
                errors.Add($"{specPath}: capability dependency '{id}' was not found in specs/capabilities/index.yaml.");
        }
    }

    private static void ValidateUseCaseReferences(HashSet<string> knownIds, List<string> ids, string specPath, string fieldName, List<string> errors)
    {
        if (ids.Count == 0)
            return;

        if (knownIds.Count == 0)
        {
            errors.Add($"{specPath}: {fieldName} declared but specs/usecases/index.yaml is missing.");
            return;
        }

        foreach (var id in ids)
        {
            if (!knownIds.Contains(id))
                errors.Add($"{specPath}: use case reference '{id}' was not found in specs/usecases/index.yaml.");
        }
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition)
            errors.Add(message);
    }

    private static void RequireHasValues(List<string>? values, string message, List<string> errors)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
            errors.Add(message);
    }
}
