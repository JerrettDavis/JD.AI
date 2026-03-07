using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class ArchitectureSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public ArchitectureMetadata Metadata { get; set; } = new();
    public string ArchitectureStyle { get; set; } = string.Empty;
    public IList<ArchitectureSystem> Systems { get; init; } = [];
    public IList<ArchitectureContainer> Containers { get; init; } = [];
    public IList<ArchitectureComponent> Components { get; init; } = [];
    public IList<ArchitectureDependencyRule> DependencyRules { get; init; } = [];
    public ArchitectureTraceability Trace { get; set; } = new();
}

public sealed class ArchitectureMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class ArchitectureSystem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class ArchitectureContainer
{
    public string Name { get; set; } = string.Empty;
    public string Technology { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
}

public sealed class ArchitectureComponent
{
    public string Name { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public string Responsibility { get; set; } = string.Empty;
}

public sealed class ArchitectureDependencyRule
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public bool Allowed { get; set; }
}

public sealed class ArchitectureTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public ArchitectureDownstreamTrace Downstream { get; set; } = new();
}

public sealed class ArchitectureDownstreamTrace
{
    public IList<string> Deployment { get; init; } = [];
    public IList<string> Security { get; init; } = [];
    public IList<string> Operations { get; init; } = [];
}

public sealed class ArchitectureSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<ArchitectureSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class ArchitectureSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class ArchitectureSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static ArchitectureSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<ArchitectureSpecification>(yaml);
    }

    public static ArchitectureSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static ArchitectureSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<ArchitectureSpecificationIndex>(yaml);
    }

    public static ArchitectureSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class ArchitectureSpecificationValidator
{
    private static readonly Regex ArchitectureIdPattern = new(
        "^architecture\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedStyles =
    [
        "layered",
        "microservices",
        "event-driven",
        "modular-monolith",
        "hexagonal",
    ];

    public static IReadOnlyList<string> Validate(ArchitectureSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new ArchitectureMetadata();
        var trace = document.Trace ?? new ArchitectureTraceability();
        var downstream = trace.Downstream ?? new ArchitectureDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Architecture", StringComparison.Ordinal), "kind must be 'Architecture'.", errors);
        Require(ArchitectureIdPattern.IsMatch(document.Id), "id must match architecture.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(AllowedStyles.Contains(document.ArchitectureStyle), "architectureStyle must be one of: layered, microservices, event-driven, modular-monolith, hexagonal.", errors);
        Require(document.Systems.Count > 0, "systems must contain at least one system.", errors);
        Require(document.Containers.Count > 0, "containers must contain at least one container.", errors);
        Require(document.Components.Count > 0, "components must contain at least one component.", errors);
        Require(document.DependencyRules.Count > 0, "dependencyRules must contain at least one rule.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);

        ValidateSystems(document.Systems, errors);
        ValidateContainers(document.Containers, errors);
        ValidateComponents(document.Components, errors);
        ValidateDependencyRules(document.DependencyRules, errors);

        Require(downstream.Deployment.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.deployment entries must not be blank.", errors);
        Require(downstream.Security.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.security entries must not be blank.", errors);
        Require(downstream.Operations.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.operations entries must not be blank.", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "architecture", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "architecture", "schema", "architecture.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/architecture/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/architecture/schema/architecture.schema.json.");

        ArchitectureSpecificationIndex index;
        try
        {
            index = ArchitectureSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/architecture/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Architecture index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "ArchitectureIndex", StringComparison.Ordinal), "Architecture index kind must be 'ArchitectureIndex'.", errors);
        Require(index.Entries.Count > 0, "Architecture index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Architecture spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            ArchitectureSpecification spec;
            try
            {
                spec = ArchitectureSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse architecture spec '{entry.Path}': {ex.Message}");
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
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Security ?? [], entry.Path, "trace.downstream.security", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Operations ?? [], entry.Path, "trace.downstream.operations", errors);
        }

        return errors;
    }

    private static void ValidateSystems(IList<ArchitectureSystem> systems, List<string> errors)
    {
        for (var i = 0; i < systems.Count; i++)
        {
            var system = systems[i] ?? new ArchitectureSystem();
            Require(!string.IsNullOrWhiteSpace(system.Name), $"systems[{i}].name is required.", errors);
        }
    }

    private static void ValidateContainers(IList<ArchitectureContainer> containers, List<string> errors)
    {
        for (var i = 0; i < containers.Count; i++)
        {
            var container = containers[i] ?? new ArchitectureContainer();
            Require(!string.IsNullOrWhiteSpace(container.Name), $"containers[{i}].name is required.", errors);
        }
    }

    private static void ValidateComponents(IList<ArchitectureComponent> components, List<string> errors)
    {
        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i] ?? new ArchitectureComponent();
            Require(!string.IsNullOrWhiteSpace(component.Name), $"components[{i}].name is required.", errors);
        }
    }

    private static void ValidateDependencyRules(IList<ArchitectureDependencyRule> rules, List<string> errors)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i] ?? new ArchitectureDependencyRule();
            Require(!string.IsNullOrWhiteSpace(rule.From), $"dependencyRules[{i}].from is required.", errors);
            Require(!string.IsNullOrWhiteSpace(rule.To), $"dependencyRules[{i}].to is required.", errors);
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
