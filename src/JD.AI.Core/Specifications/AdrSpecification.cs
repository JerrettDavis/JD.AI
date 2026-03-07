using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class AdrSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public AdrMetadata Metadata { get; set; } = new();
    public string Date { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public IList<AdrAlternative> Alternatives { get; init; } = [];
    public IList<string> Consequences { get; init; } = [];
    public IList<string> Supersedes { get; init; } = [];
    public IList<string> ConflictsWith { get; init; } = [];
    public AdrTraceability Trace { get; set; } = new();
}

public sealed class AdrMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class AdrAlternative
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IList<string> Pros { get; init; } = [];
    public IList<string> Cons { get; init; } = [];
}

public sealed class AdrTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public AdrDownstreamTrace Downstream { get; set; } = new();
}

public sealed class AdrDownstreamTrace
{
    public IList<string> Implementation { get; init; } = [];
    public IList<string> Governance { get; init; } = [];
}

public sealed class AdrSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<AdrSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class AdrSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class AdrSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static AdrSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<AdrSpecification>(yaml);
    }

    public static AdrSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static AdrSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<AdrSpecificationIndex>(yaml);
    }

    public static AdrSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class AdrSpecificationValidator
{
    private static readonly Regex AdrIdPattern = new(
        @"^adr\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    public static IReadOnlyList<string> Validate(AdrSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new AdrMetadata();
        var trace = document.Trace ?? new AdrTraceability();
        var downstream = trace.Downstream ?? new AdrDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Adr", StringComparison.Ordinal), "kind must be 'Adr'.", errors);
        Require(AdrIdPattern.IsMatch(document.Id), "id must match adr.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(DateOnly.TryParse(document.Date, out _), "date must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(document.Context), "context is required.", errors);
        Require(!string.IsNullOrWhiteSpace(document.Decision), "decision is required.", errors);
        Require(document.Alternatives.Count > 0, "alternatives must contain at least one alternative.", errors);
        RequireHasValues(document.Consequences, "consequences must contain at least one consequence.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.Implementation.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.implementation entries must not be blank.", errors);
        Require(downstream.Governance.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.governance entries must not be blank.", errors);

        ValidateAlternatives(document.Alternatives, errors);
        ValidateAdrIdReferences(document.Supersedes, "supersedes", errors);
        ValidateAdrIdReferences(document.ConflictsWith, "conflictsWith", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "adrs", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "adrs", "schema", "adrs.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/adrs/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/adrs/schema/adrs.schema.json.");

        AdrSpecificationIndex index;
        try
        {
            index = AdrSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/adrs/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Adr index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "AdrIndex", StringComparison.Ordinal), "Adr index kind must be 'AdrIndex'.", errors);
        Require(index.Entries.Count > 0, "Adr index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Adr spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            AdrSpecification spec;
            try
            {
                spec = AdrSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse adr spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Implementation ?? [], entry.Path, "trace.downstream.implementation", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Governance ?? [], entry.Path, "trace.downstream.governance", errors);
        }

        return errors;
    }

    private static void ValidateAlternatives(IList<AdrAlternative> alternatives, List<string> errors)
    {
        for (var i = 0; i < alternatives.Count; i++)
        {
            var alternative = alternatives[i] ?? new AdrAlternative();
            var prefix = $"alternatives[{i}]";

            Require(!string.IsNullOrWhiteSpace(alternative.Title), $"{prefix}.title is required.", errors);
        }
    }

    private static void ValidateAdrIdReferences(IList<string> ids, string fieldName, List<string> errors)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            Require(AdrIdPattern.IsMatch(id ?? string.Empty), $"{fieldName}[{i}] must match adr.<name> convention.", errors);
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
