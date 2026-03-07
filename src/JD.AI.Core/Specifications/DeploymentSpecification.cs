using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class DeploymentSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DeploymentMetadata Metadata { get; set; } = new();
    public IList<DeploymentEnvironment> Environments { get; init; } = [];
    public IList<DeploymentPipelineStage> PipelineStages { get; init; } = [];
    public IList<DeploymentPromotionGate> PromotionGates { get; init; } = [];
    public IList<string> InfrastructureRefs { get; init; } = [];
    public string RollbackStrategy { get; set; } = string.Empty;
    public DeploymentTraceability Trace { get; set; } = new();
}

public sealed class DeploymentMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class DeploymentEnvironment
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
}

public sealed class DeploymentPipelineStage
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool Automated { get; set; }
}

public sealed class DeploymentPromotionGate
{
    public string FromEnv { get; set; } = string.Empty;
    public string ToEnv { get; set; } = string.Empty;
    public IList<string> Criteria { get; init; } = [];
}

public sealed class DeploymentTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public DeploymentDownstreamTrace Downstream { get; set; } = new();
}

public sealed class DeploymentDownstreamTrace
{
    public IList<string> Operations { get; init; } = [];
    public IList<string> Observability { get; init; } = [];
}

public sealed class DeploymentSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<DeploymentSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class DeploymentSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class DeploymentSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static DeploymentSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<DeploymentSpecification>(yaml);
    }

    public static DeploymentSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static DeploymentSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<DeploymentSpecificationIndex>(yaml);
    }

    public static DeploymentSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class DeploymentSpecificationValidator
{
    private static readonly Regex DeploymentIdPattern = new(
        "^deployment\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedEnvironmentTypes =
    [
        "development",
        "staging",
        "production",
        "dr",
    ];

    private static readonly HashSet<string> AllowedRollbackStrategies =
    [
        "blue-green",
        "canary",
        "rolling",
        "recreate",
        "manual",
    ];

    public static IReadOnlyList<string> Validate(DeploymentSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new DeploymentMetadata();
        var trace = document.Trace ?? new DeploymentTraceability();
        var downstream = trace.Downstream ?? new DeploymentDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Deployment", StringComparison.Ordinal), "kind must be 'Deployment'.", errors);
        Require(DeploymentIdPattern.IsMatch(document.Id), "id must match deployment.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(document.Environments.Count > 0, "environments must contain at least one environment.", errors);
        Require(document.PipelineStages.Count > 0, "pipelineStages must contain at least one stage.", errors);
        Require(AllowedRollbackStrategies.Contains(document.RollbackStrategy), "rollbackStrategy must be one of: blue-green, canary, rolling, recreate, manual.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.Operations.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.operations entries must not be blank.", errors);
        Require(downstream.Observability.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.observability entries must not be blank.", errors);

        ValidateEnvironments(document.Environments, errors);
        ValidatePipelineStages(document.PipelineStages, errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "deployment", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "deployment", "schema", "deployment.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/deployment/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/deployment/schema/deployment.schema.json.");

        DeploymentSpecificationIndex index;
        try
        {
            index = DeploymentSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/deployment/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Deployment index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "DeploymentIndex", StringComparison.Ordinal), "Deployment index kind must be 'DeploymentIndex'.", errors);
        Require(index.Entries.Count > 0, "Deployment index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Deployment spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            DeploymentSpecification spec;
            try
            {
                spec = DeploymentSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse deployment spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Operations ?? [], entry.Path, "trace.downstream.operations", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Observability ?? [], entry.Path, "trace.downstream.observability", errors);
        }

        return errors;
    }

    private static void ValidateEnvironments(IList<DeploymentEnvironment> environments, List<string> errors)
    {
        for (var i = 0; i < environments.Count; i++)
        {
            var env = environments[i] ?? new DeploymentEnvironment();
            var prefix = $"environments[{i}]";

            Require(!string.IsNullOrWhiteSpace(env.Name), $"{prefix}.name is required.", errors);
            Require(AllowedEnvironmentTypes.Contains(env.Type), $"{prefix}.type must be one of: development, staging, production, dr.", errors);
        }
    }

    private static void ValidatePipelineStages(IList<DeploymentPipelineStage> stages, List<string> errors)
    {
        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i] ?? new DeploymentPipelineStage();
            var prefix = $"pipelineStages[{i}]";

            Require(!string.IsNullOrWhiteSpace(stage.Name), $"{prefix}.name is required.", errors);
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
