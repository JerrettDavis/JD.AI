using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class UseCaseSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public UseCaseMetadata Metadata { get; set; } = new();
    public string Actor { get; set; } = string.Empty;
    public string CapabilityRef { get; set; } = string.Empty;
    public List<string> Preconditions { get; set; } = [];
    public List<string> WorkflowSteps { get; set; } = [];
    public List<string> ExpectedOutcomes { get; set; } = [];
    public List<string> FailureScenarios { get; set; } = [];
    public UseCaseTraceability Trace { get; set; } = new();
}

public sealed class UseCaseMetadata
{
    public List<string> Owners { get; set; } = [];
    public List<string> Reviewers { get; set; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class UseCaseTraceability
{
    public List<string> Upstream { get; set; } = [];
    public UseCaseDownstreamTrace Downstream { get; set; } = new();
}

public sealed class UseCaseDownstreamTrace
{
    public List<string> Behavior { get; set; } = [];
    public List<string> Testing { get; set; } = [];
    public List<string> Interfaces { get; set; } = [];
}

public sealed class UseCaseSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public List<UseCaseSpecificationIndexEntry> Entries { get; set; } = [];
}

public sealed class UseCaseSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class UseCaseSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static UseCaseSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<UseCaseSpecification>(yaml);
    }

    public static UseCaseSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static UseCaseSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<UseCaseSpecificationIndex>(yaml);
    }

    public static UseCaseSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class UseCaseSpecificationValidator
{
    private static readonly Regex UseCaseIdPattern = new(
        "^usecase\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PersonaIdPattern = new(
        "^persona\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CapabilityIdPattern = new(
        "^capability\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    public static IReadOnlyList<string> Validate(UseCaseSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new UseCaseMetadata();
        var trace = document.Trace ?? new UseCaseTraceability();
        var downstream = trace.Downstream ?? new UseCaseDownstreamTrace();

        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "UseCase", StringComparison.Ordinal), "kind must be 'UseCase'.", errors);
        Require(UseCaseIdPattern.IsMatch(document.Id), "id must match usecase.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(PersonaIdPattern.IsMatch(document.Actor), "actor must match persona.<name> convention.", errors);
        Require(CapabilityIdPattern.IsMatch(document.CapabilityRef), "capabilityRef must match capability.<name> convention.", errors);
        RequireHasValues(document.Preconditions, "preconditions must contain at least one precondition.", errors);
        RequireHasValues(document.WorkflowSteps, "workflowSteps must contain at least one step.", errors);
        RequireHasValues(document.ExpectedOutcomes, "expectedOutcomes must contain at least one outcome.", errors);
        RequireHasValues(document.FailureScenarios, "failureScenarios must contain at least one scenario.", errors);
        Require(downstream.Behavior.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.behavior entries must not be blank.", errors);
        Require(downstream.Testing.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.testing entries must not be blank.", errors);
        Require(downstream.Interfaces.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.interfaces entries must not be blank.", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "usecases", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "usecases", "schema", "usecases.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/usecases/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/usecases/schema/usecases.schema.json.");

        UseCaseSpecificationIndex index;
        try
        {
            index = UseCaseSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/usecases/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Use case index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "UseCaseIndex", StringComparison.Ordinal), "Use case index kind must be 'UseCaseIndex'.", errors);
        Require(index.Entries.Count > 0, "Use case index must contain at least one entry.", errors);

        var capabilityIds = LoadCapabilityIds(repoRoot, errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Use case spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            UseCaseSpecification spec;
            try
            {
                spec = UseCaseSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse use case spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            if (!capabilityIds.Contains(spec.CapabilityRef))
                errors.Add($"{entry.Path}: capabilityRef '{spec.CapabilityRef}' was not found in specs/capabilities/index.yaml.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Behavior ?? [], entry.Path, "trace.downstream.behavior", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Testing ?? [], entry.Path, "trace.downstream.testing", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Interfaces ?? [], entry.Path, "trace.downstream.interfaces", errors);
        }

        return errors;
    }

    private static HashSet<string> LoadCapabilityIds(string repoRoot, List<string> errors)
    {
        var path = Path.Combine(repoRoot, "specs", "capabilities", "index.yaml");
        if (!File.Exists(path))
        {
            errors.Add("Missing specs/capabilities/index.yaml required for use case validation.");
            return [];
        }

        try
        {
            var index = CapabilitySpecificationParser.ParseIndexFile(path);
            return index.Entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/capabilities/index.yaml: {ex.Message}");
            return [];
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
