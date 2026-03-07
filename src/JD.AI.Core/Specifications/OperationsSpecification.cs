using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class OperationsSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public BehaviorMetadata Metadata { get; set; } = new();
    public string Service { get; set; } = string.Empty;
    public IList<OperationsRunbook> Runbooks { get; init; } = [];
    public IList<OperationsIncidentLevel> IncidentLevels { get; init; } = [];
    public IList<OperationsResponseSlo> ResponseSlos { get; init; } = [];
    public IList<OperationsEscalationPath> EscalationPaths { get; init; } = [];
    public OperationsTraceability Trace { get; set; } = new();
}

public sealed class OperationsRunbook
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerCondition { get; set; } = string.Empty;
    public IList<string> Steps { get; init; } = [];
}

public sealed class OperationsIncidentLevel
{
    public string Level { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResponseTime { get; set; } = string.Empty;
}

public sealed class OperationsResponseSlo
{
    public string Level { get; set; } = string.Empty;
    public string AcknowledgeWithin { get; set; } = string.Empty;
    public string ResolveWithin { get; set; } = string.Empty;
}

public sealed class OperationsEscalationPath
{
    public string Level { get; set; } = string.Empty;
    public IList<string> Contacts { get; init; } = [];
}

public sealed class OperationsTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public OperationsDownstreamTrace Downstream { get; set; } = new();
}

public sealed class OperationsDownstreamTrace
{
    public IList<string> Governance { get; init; } = [];
    public IList<string> Audits { get; init; } = [];
}

public sealed class OperationsSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<BehaviorSpecificationIndexEntry> Entries { get; init; } = [];
}

public static class OperationsSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static OperationsSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<OperationsSpecification>(yaml);
    }

    public static OperationsSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static OperationsSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<OperationsSpecificationIndex>(yaml);
    }

    public static OperationsSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class OperationsSpecificationValidator
{
    private static readonly Regex OperationsIdPattern = new(
        "^operations\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedIncidentLevels =
    [
        "sev1",
        "sev2",
        "sev3",
        "sev4",
    ];

    public static IReadOnlyList<string> Validate(OperationsSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new BehaviorMetadata();
        var trace = document.Trace ?? new OperationsTraceability();
        var downstream = trace.Downstream ?? new OperationsDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Operations", StringComparison.Ordinal), "kind must be 'Operations'.", errors);
        Require(OperationsIdPattern.IsMatch(document.Id), "id must match operations.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(!string.IsNullOrWhiteSpace(document.Service), "service is required.", errors);
        Require(document.Runbooks.Count > 0, "runbooks must contain at least one runbook.", errors);
        Require(document.IncidentLevels.Count > 0, "incidentLevels must contain at least one incident level.", errors);
        Require(document.EscalationPaths.Count > 0, "escalationPaths must contain at least one escalation path.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.Governance.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.governance entries must not be blank.", errors);
        Require(downstream.Audits.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.audits entries must not be blank.", errors);

        ValidateRunbooks(document.Runbooks, errors);
        ValidateIncidentLevels(document.IncidentLevels, errors);
        ValidateEscalationPaths(document.EscalationPaths, errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "operations", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "operations", "schema", "operations.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/operations/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/operations/schema/operations.schema.json.");

        OperationsSpecificationIndex index;
        try
        {
            index = OperationsSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/operations/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Operations index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "OperationsIndex", StringComparison.Ordinal), "Operations index kind must be 'OperationsIndex'.", errors);
        Require(index.Entries.Count > 0, "Operations index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Operations spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            OperationsSpecification spec;
            try
            {
                spec = OperationsSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse operations spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Governance ?? [], entry.Path, "trace.downstream.governance", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Audits ?? [], entry.Path, "trace.downstream.audits", errors);
        }

        return errors;
    }

    private static void ValidateRunbooks(IList<OperationsRunbook> runbooks, List<string> errors)
    {
        for (var i = 0; i < runbooks.Count; i++)
        {
            var runbook = runbooks[i] ?? new OperationsRunbook();
            var prefix = $"runbooks[{i}]";

            Require(!string.IsNullOrWhiteSpace(runbook.Name), $"{prefix}.name is required.", errors);
            Require(!string.IsNullOrWhiteSpace(runbook.TriggerCondition), $"{prefix}.triggerCondition is required.", errors);
            RequireHasValues(runbook.Steps, $"{prefix}.steps must contain at least one step.", errors);
        }
    }

    private static void ValidateIncidentLevels(IList<OperationsIncidentLevel> incidentLevels, List<string> errors)
    {
        for (var i = 0; i < incidentLevels.Count; i++)
        {
            var level = incidentLevels[i] ?? new OperationsIncidentLevel();
            var prefix = $"incidentLevels[{i}]";

            Require(AllowedIncidentLevels.Contains(level.Level), $"{prefix}.level must be one of: sev1, sev2, sev3, sev4.", errors);
            Require(!string.IsNullOrWhiteSpace(level.Description), $"{prefix}.description is required.", errors);
            Require(!string.IsNullOrWhiteSpace(level.ResponseTime), $"{prefix}.responseTime is required.", errors);
        }
    }

    private static void ValidateEscalationPaths(IList<OperationsEscalationPath> escalationPaths, List<string> errors)
    {
        for (var i = 0; i < escalationPaths.Count; i++)
        {
            var path = escalationPaths[i] ?? new OperationsEscalationPath();
            var prefix = $"escalationPaths[{i}]";

            Require(!string.IsNullOrWhiteSpace(path.Level), $"{prefix}.level is required.", errors);
            RequireHasValues(path.Contacts, $"{prefix}.contacts must contain at least one contact.", errors);
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
