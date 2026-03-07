using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class DomainSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DomainMetadata Metadata { get; set; } = new();
    public string BoundedContext { get; set; } = string.Empty;
    public IList<DomainEntity> Entities { get; init; } = [];
    public IList<DomainValueObject> ValueObjects { get; init; } = [];
    public IList<DomainAggregate> Aggregates { get; init; } = [];
    public IList<string> Invariants { get; init; } = [];
    public DomainTraceability Trace { get; set; } = new();
}

public sealed class DomainMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class DomainEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IList<string> Properties { get; init; } = [];
}

public sealed class DomainValueObject
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IList<string> Properties { get; init; } = [];
}

public sealed class DomainAggregate
{
    public string Name { get; set; } = string.Empty;
    public string RootEntity { get; set; } = string.Empty;
    public IList<string> Members { get; init; } = [];
}

public sealed class DomainTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public DomainDownstreamTrace Downstream { get; set; } = new();
}

public sealed class DomainDownstreamTrace
{
    public IList<string> Data { get; init; } = [];
    public IList<string> Interfaces { get; init; } = [];
    public IList<string> Architecture { get; init; } = [];
}

public sealed class DomainSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<DomainSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class DomainSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class DomainSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static DomainSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<DomainSpecification>(yaml);
    }

    public static DomainSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static DomainSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<DomainSpecificationIndex>(yaml);
    }

    public static DomainSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class DomainSpecificationValidator
{
    private static readonly Regex DomainIdPattern = new(
        @"^domain\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    public static IReadOnlyList<string> Validate(DomainSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new DomainMetadata();
        var trace = document.Trace ?? new DomainTraceability();
        var downstream = trace.Downstream ?? new DomainDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Domain", StringComparison.Ordinal), "kind must be 'Domain'.", errors);
        Require(DomainIdPattern.IsMatch(document.Id), "id must match domain.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(!string.IsNullOrWhiteSpace(document.BoundedContext), "boundedContext is required.", errors);
        Require(document.Entities.Count > 0, "entities must contain at least one entity.", errors);
        RequireHasValues(document.Invariants, "invariants must contain at least one invariant.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.Interfaces.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.interfaces entries must not be blank.", errors);

        for (var i = 0; i < document.Entities.Count; i++)
        {
            var entity = document.Entities[i] ?? new DomainEntity();
            Require(!string.IsNullOrWhiteSpace(entity.Name), $"entities[{i}].name is required.", errors);
        }

        for (var i = 0; i < document.Aggregates.Count; i++)
        {
            var aggregate = document.Aggregates[i] ?? new DomainAggregate();
            Require(!string.IsNullOrWhiteSpace(aggregate.Name), $"aggregates[{i}].name is required.", errors);
            Require(!string.IsNullOrWhiteSpace(aggregate.RootEntity), $"aggregates[{i}].rootEntity is required.", errors);
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "domain", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "domain", "schema", "domain.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/domain/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/domain/schema/domain.schema.json.");

        DomainSpecificationIndex index;
        try
        {
            index = DomainSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/domain/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Domain index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "DomainIndex", StringComparison.Ordinal), "Domain index kind must be 'DomainIndex'.", errors);
        Require(index.Entries.Count > 0, "Domain index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Domain spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            DomainSpecification spec;
            try
            {
                spec = DomainSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse domain spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Data ?? [], entry.Path, "trace.downstream.data", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Interfaces ?? [], entry.Path, "trace.downstream.interfaces", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Architecture ?? [], entry.Path, "trace.downstream.architecture", errors);
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
