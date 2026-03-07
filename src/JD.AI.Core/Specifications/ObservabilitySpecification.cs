using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class ObservabilitySpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public ObservabilityMetadata Metadata { get; set; } = new();
    public IList<string> ServiceRefs { get; init; } = [];
    public IList<ObservabilityMetric> Metrics { get; init; } = [];
    public IList<ObservabilityLog> Logs { get; init; } = [];
    public IList<ObservabilityTrace> Traces { get; init; } = [];
    public IList<ObservabilityAlert> Alerts { get; init; } = [];
    public ObservabilityTraceability Trace { get; set; } = new();
}

public sealed class ObservabilityMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class ObservabilityMetric
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class ObservabilityLog
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
}

public sealed class ObservabilityTrace
{
    public string Name { get; set; } = string.Empty;
    public string SpanKind { get; set; } = string.Empty;
    public IList<string> Attributes { get; init; } = [];
}

public sealed class ObservabilityAlert
{
    public string Name { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

public sealed class ObservabilityTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public ObservabilityDownstreamTrace Downstream { get; set; } = new();
}

public sealed class ObservabilityDownstreamTrace
{
    public IList<string> Operations { get; init; } = [];
    public IList<string> Governance { get; init; } = [];
}

public sealed class ObservabilitySpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<ObservabilitySpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class ObservabilitySpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class ObservabilitySpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static ObservabilitySpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<ObservabilitySpecification>(yaml);
    }

    public static ObservabilitySpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static ObservabilitySpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<ObservabilitySpecificationIndex>(yaml);
    }

    public static ObservabilitySpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class ObservabilitySpecificationValidator
{
    private static readonly Regex ObservabilityIdPattern = new(
        "^observability\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedMetricTypes =
    [
        "counter",
        "gauge",
        "histogram",
        "summary",
    ];

    private static readonly HashSet<string> AllowedLogLevels =
    [
        "debug",
        "info",
        "warning",
        "error",
        "critical",
    ];

    private static readonly HashSet<string> AllowedSpanKinds =
    [
        "server",
        "client",
        "producer",
        "consumer",
        "internal",
    ];

    private static readonly HashSet<string> AllowedAlertSeverities =
    [
        "critical",
        "warning",
        "info",
    ];

    public static IReadOnlyList<string> Validate(ObservabilitySpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new ObservabilityMetadata();
        var trace = document.Trace ?? new ObservabilityTraceability();
        var downstream = trace.Downstream ?? new ObservabilityDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Observability", StringComparison.Ordinal), "kind must be 'Observability'.", errors);
        Require(ObservabilityIdPattern.IsMatch(document.Id), "id must match observability.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        RequireHasValues(document.ServiceRefs, "serviceRefs must contain at least one service reference.", errors);
        Require(document.Metrics.Count > 0, "metrics must contain at least one metric.", errors);
        Require(document.Alerts.Count > 0, "alerts must contain at least one alert.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.Operations.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.operations entries must not be blank.", errors);
        Require(downstream.Governance.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.governance entries must not be blank.", errors);

        ValidateMetrics(document.Metrics, errors);
        ValidateLogs(document.Logs, errors);
        ValidateTraces(document.Traces, errors);
        ValidateAlerts(document.Alerts, errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "observability", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "observability", "schema", "observability.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/observability/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/observability/schema/observability.schema.json.");

        ObservabilitySpecificationIndex index;
        try
        {
            index = ObservabilitySpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/observability/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Observability index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "ObservabilityIndex", StringComparison.Ordinal), "Observability index kind must be 'ObservabilityIndex'.", errors);
        Require(index.Entries.Count > 0, "Observability index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Observability spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            ObservabilitySpecification spec;
            try
            {
                spec = ObservabilitySpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse observability spec '{entry.Path}': {ex.Message}");
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
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Governance ?? [], entry.Path, "trace.downstream.governance", errors);
        }

        return errors;
    }

    private static void ValidateMetrics(IList<ObservabilityMetric> metrics, List<string> errors)
    {
        for (var i = 0; i < metrics.Count; i++)
        {
            var metric = metrics[i] ?? new ObservabilityMetric();
            var prefix = $"metrics[{i}]";

            Require(!string.IsNullOrWhiteSpace(metric.Name), $"{prefix}.name is required.", errors);
            Require(AllowedMetricTypes.Contains(metric.Type), $"{prefix}.type must be one of: counter, gauge, histogram, summary.", errors);
        }
    }

    private static void ValidateLogs(IList<ObservabilityLog> logs, List<string> errors)
    {
        for (var i = 0; i < logs.Count; i++)
        {
            var log = logs[i] ?? new ObservabilityLog();
            var prefix = $"logs[{i}]";

            Require(AllowedLogLevels.Contains(log.Level), $"{prefix}.level must be one of: debug, info, warning, error, critical.", errors);
        }
    }

    private static void ValidateTraces(IList<ObservabilityTrace> traces, List<string> errors)
    {
        for (var i = 0; i < traces.Count; i++)
        {
            var trace = traces[i] ?? new ObservabilityTrace();
            var prefix = $"traces[{i}]";

            Require(AllowedSpanKinds.Contains(trace.SpanKind), $"{prefix}.spanKind must be one of: server, client, producer, consumer, internal.", errors);
        }
    }

    private static void ValidateAlerts(IList<ObservabilityAlert> alerts, List<string> errors)
    {
        for (var i = 0; i < alerts.Count; i++)
        {
            var alert = alerts[i] ?? new ObservabilityAlert();
            var prefix = $"alerts[{i}]";

            Require(!string.IsNullOrWhiteSpace(alert.Name), $"{prefix}.name is required.", errors);
            Require(!string.IsNullOrWhiteSpace(alert.Condition), $"{prefix}.condition is required.", errors);
            Require(AllowedAlertSeverities.Contains(alert.Severity), $"{prefix}.severity must be one of: critical, warning, info.", errors);
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
