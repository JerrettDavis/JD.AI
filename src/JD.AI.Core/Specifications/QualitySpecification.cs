using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class QualitySpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public QualityMetadata Metadata { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public IList<QualitySlo> Slos { get; init; } = [];
    public IList<QualitySli> Slis { get; init; } = [];
    public IList<QualityErrorBudget> ErrorBudgets { get; init; } = [];
    public IList<QualityScalabilityExpectation> ScalabilityExpectations { get; init; } = [];
    public QualityTraceability Trace { get; set; } = new();
}

public sealed class QualityMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class QualitySlo
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class QualitySli
{
    public string Name { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

public sealed class QualityErrorBudget
{
    public string SloRef { get; set; } = string.Empty;
    public string Budget { get; set; } = string.Empty;
    public string Window { get; set; } = string.Empty;
}

public sealed class QualityScalabilityExpectation
{
    public string Dimension { get; set; } = string.Empty;
    public string Current { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}

public sealed class QualityTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public QualityDownstreamTrace Downstream { get; set; } = new();
}

public sealed class QualityDownstreamTrace
{
    public IList<string> Testing { get; init; } = [];
    public IList<string> Observability { get; init; } = [];
    public IList<string> Operations { get; init; } = [];
}

public sealed class QualitySpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<QualitySpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class QualitySpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class QualitySpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static QualitySpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<QualitySpecification>(yaml);
    }

    public static QualitySpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static QualitySpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<QualitySpecificationIndex>(yaml);
    }

    public static QualitySpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class QualitySpecificationValidator
{
    private static readonly Regex QualityIdPattern = new(
        "^quality\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedCategories =
    [
        "performance",
        "availability",
        "reliability",
        "scalability",
        "security",
    ];

    public static IReadOnlyList<string> Validate(QualitySpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new QualityMetadata();
        var trace = document.Trace ?? new QualityTraceability();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Quality", StringComparison.Ordinal), "kind must be 'Quality'.", errors);
        Require(QualityIdPattern.IsMatch(document.Id), "id must match quality.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(AllowedCategories.Contains(document.Category), "category must be one of: performance, availability, reliability, scalability, security.", errors);

        Require(document.Slos.Count > 0, "slos must contain at least one SLO.", errors);
        for (var i = 0; i < document.Slos.Count; i++)
        {
            var slo = document.Slos[i] ?? new QualitySlo();
            Require(!string.IsNullOrWhiteSpace(slo.Name), $"slos[{i}].name is required.", errors);
            Require(!string.IsNullOrWhiteSpace(slo.Target), $"slos[{i}].target is required.", errors);
        }

        Require(document.Slis.Count > 0, "slis must contain at least one SLI.", errors);
        for (var i = 0; i < document.Slis.Count; i++)
        {
            var sli = document.Slis[i] ?? new QualitySli();
            Require(!string.IsNullOrWhiteSpace(sli.Name), $"slis[{i}].name is required.", errors);
            Require(!string.IsNullOrWhiteSpace(sli.Metric), $"slis[{i}].metric is required.", errors);
        }

        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "quality", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "quality", "schema", "quality.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/quality/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/quality/schema/quality.schema.json.");

        QualitySpecificationIndex index;
        try
        {
            index = QualitySpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/quality/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Quality index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "QualityIndex", StringComparison.Ordinal), "Quality index kind must be 'QualityIndex'.", errors);
        Require(index.Entries.Count > 0, "Quality index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Quality spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            QualitySpecification spec;
            try
            {
                spec = QualitySpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse quality spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Testing ?? [], entry.Path, "trace.downstream.testing", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Observability ?? [], entry.Path, "trace.downstream.observability", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Operations ?? [], entry.Path, "trace.downstream.operations", errors);
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
