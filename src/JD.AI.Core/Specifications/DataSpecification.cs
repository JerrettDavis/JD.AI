using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class DataSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DataMetadata Metadata { get; set; } = new();
    public string ModelType { get; set; } = string.Empty;
    public IList<DataSchema> Schemas { get; init; } = [];
    public IList<DataMigration> Migrations { get; init; } = [];
    public IList<DataIndexDefinition> Indexes { get; init; } = [];
    public IList<string> Constraints { get; init; } = [];
    public DataTraceability Trace { get; set; } = new();
}

public sealed class DataMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class DataSchema
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IList<string> Fields { get; init; } = [];
}

public sealed class DataMigration
{
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Reversible { get; set; }
}

public sealed class DataIndexDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public IList<string> Columns { get; init; } = [];
}

public sealed class DataTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public DataDownstreamTrace Downstream { get; set; } = new();
}

public sealed class DataDownstreamTrace
{
    public IList<string> Deployment { get; init; } = [];
    public IList<string> Operations { get; init; } = [];
    public IList<string> Testing { get; init; } = [];
}

public sealed class DataSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<DataSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class DataSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class DataSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static DataSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<DataSpecification>(yaml);
    }

    public static DataSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static DataSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<DataSpecificationIndex>(yaml);
    }

    public static DataSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class DataSpecificationValidator
{
    private static readonly Regex DataIdPattern = new(
        "^data\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedModelTypes =
    [
        "relational",
        "document",
        "event",
        "graph",
    ];

    public static IReadOnlyList<string> Validate(DataSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new DataMetadata();
        var trace = document.Trace ?? new DataTraceability();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Data", StringComparison.Ordinal), "kind must be 'Data'.", errors);
        Require(DataIdPattern.IsMatch(document.Id), "id must match data.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(AllowedModelTypes.Contains(document.ModelType), "modelType must be one of: relational, document, event, graph.", errors);
        Require(document.Schemas.Count > 0, "schemas must contain at least one schema.", errors);
        RequireHasValues(document.Constraints, "constraints must contain at least one constraint.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);

        for (var i = 0; i < document.Schemas.Count; i++)
        {
            var schema = document.Schemas[i] ?? new DataSchema();
            Require(!string.IsNullOrWhiteSpace(schema.Name), $"schemas[{i}].name is required.", errors);
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "data", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "data", "schema", "data.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/data/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/data/schema/data.schema.json.");

        DataSpecificationIndex index;
        try
        {
            index = DataSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/data/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Data index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "DataIndex", StringComparison.Ordinal), "Data index kind must be 'DataIndex'.", errors);
        Require(index.Entries.Count > 0, "Data index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Data spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            DataSpecification spec;
            try
            {
                spec = DataSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse data spec '{entry.Path}': {ex.Message}");
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
