using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class InterfaceSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public InterfaceMetadata Metadata { get; set; } = new();
    public string InterfaceType { get; set; } = string.Empty;
    public IList<InterfaceOperation> Operations { get; init; } = [];
    public IList<InterfaceMessageSchema> MessageSchemas { get; init; } = [];
    public IList<string> CompatibilityRules { get; init; } = [];
    public InterfaceTraceability Trace { get; set; } = new();
}

public sealed class InterfaceMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class InterfaceOperation
{
    public string Name { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class InterfaceMessageSchema
{
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class InterfaceTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public InterfaceDownstreamTrace Downstream { get; set; } = new();
}

public sealed class InterfaceDownstreamTrace
{
    public IList<string> Code { get; init; } = [];
    public IList<string> Testing { get; init; } = [];
    public IList<string> Deployment { get; init; } = [];
}

public sealed class InterfaceSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<InterfaceSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class InterfaceSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class InterfaceSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static InterfaceSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<InterfaceSpecification>(yaml);
    }

    public static InterfaceSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static InterfaceSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<InterfaceSpecificationIndex>(yaml);
    }

    public static InterfaceSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class InterfaceSpecificationValidator
{
    private static readonly Regex InterfaceIdPattern = new(
        @"^interface\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedInterfaceTypes =
    [
        "rest",
        "graphql",
        "grpc",
        "event",
        "websocket",
    ];

    public static IReadOnlyList<string> Validate(InterfaceSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new InterfaceMetadata();
        var trace = document.Trace ?? new InterfaceTraceability();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Interface", StringComparison.Ordinal), "kind must be 'Interface'.", errors);
        Require(InterfaceIdPattern.IsMatch(document.Id), "id must match interface.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(AllowedInterfaceTypes.Contains(document.InterfaceType), "interfaceType must be one of: rest, graphql, grpc, event, websocket.", errors);
        Require(document.Operations.Count > 0, "operations must contain at least one operation.", errors);

        for (var i = 0; i < document.Operations.Count; i++)
        {
            var operation = document.Operations[i] ?? new InterfaceOperation();
            Require(!string.IsNullOrWhiteSpace(operation.Name), $"operations[{i}].name is required.", errors);
        }

        RequireHasValues(document.CompatibilityRules, "compatibilityRules must contain at least one rule.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "interfaces", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "interfaces", "schema", "interfaces.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/interfaces/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/interfaces/schema/interfaces.schema.json.");

        InterfaceSpecificationIndex index;
        try
        {
            index = InterfaceSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/interfaces/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Interface index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "InterfaceIndex", StringComparison.Ordinal), "Interface index kind must be 'InterfaceIndex'.", errors);
        Require(index.Entries.Count > 0, "Interface index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Interface spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            InterfaceSpecification spec;
            try
            {
                spec = InterfaceSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse interface spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Code ?? [], entry.Path, "trace.downstream.code", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Testing ?? [], entry.Path, "trace.downstream.testing", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Deployment ?? [], entry.Path, "trace.downstream.deployment", errors);
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
