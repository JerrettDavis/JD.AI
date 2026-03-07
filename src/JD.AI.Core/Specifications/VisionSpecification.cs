using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class VisionSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public VisionMetadata Metadata { get; set; } = new();
    public string ProblemStatement { get; set; } = string.Empty;
    public string Mission { get; set; } = string.Empty;
    public List<VisionTargetUser> TargetUsers { get; set; } = [];
    public VisionValueProposition ValueProposition { get; set; } = new();
    public List<VisionSuccessMetric> SuccessMetrics { get; set; } = [];
    public List<string> Constraints { get; set; } = [];
    public List<string> NonGoals { get; set; } = [];
    public VisionTraceability Trace { get; set; } = new();
}

public sealed class VisionMetadata
{
    public List<string> Owners { get; set; } = [];
    public List<string> Reviewers { get; set; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class VisionTargetUser
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Needs { get; set; } = [];
}

public sealed class VisionValueProposition
{
    public string Summary { get; set; } = string.Empty;
    public List<string> Differentiators { get; set; } = [];
}

public sealed class VisionSuccessMetric
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Measurement { get; set; } = string.Empty;
}

public sealed class VisionTraceability
{
    public List<string> Upstream { get; set; } = [];
    public VisionDownstreamTrace Downstream { get; set; } = new();
}

public sealed class VisionDownstreamTrace
{
    public List<string> Capabilities { get; set; } = [];
}

public sealed class VisionSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public List<VisionSpecificationIndexEntry> Entries { get; set; } = [];
}

public sealed class VisionSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class CapabilitySpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public List<CapabilitySpecificationIndexEntry> Entries { get; set; } = [];
}

public sealed class CapabilitySpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class VisionSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static VisionSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<VisionSpecification>(yaml);
    }

    public static VisionSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static VisionSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<VisionSpecificationIndex>(yaml);
    }

    public static VisionSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }

    public static CapabilitySpecificationIndex ParseCapabilityIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<CapabilitySpecificationIndex>(yaml);
    }

    public static CapabilitySpecificationIndex ParseCapabilityIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseCapabilityIndex(File.ReadAllText(path));
    }
}

public static class VisionSpecificationValidator
{
    private static readonly Regex VisionIdPattern = new(
        "^vision\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TargetUserIdPattern = new(
        "^user\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MetricIdPattern = new(
        "^metric\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
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

    public static IReadOnlyList<string> Validate(VisionSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();

        var metadata = document.Metadata ?? new VisionMetadata();
        var valueProposition = document.ValueProposition ?? new VisionValueProposition();
        var trace = document.Trace ?? new VisionTraceability();
        var downstream = trace.Downstream ?? new VisionDownstreamTrace();
        var targetUsers = document.TargetUsers ?? [];
        var successMetrics = document.SuccessMetrics ?? [];
        var constraints = document.Constraints ?? [];
        var nonGoals = document.NonGoals ?? [];
        var differentiators = valueProposition.Differentiators ?? [];
        var capabilityRefs = downstream.Capabilities ?? [];

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Vision", StringComparison.Ordinal), "kind must be 'Vision'.", errors);
        Require(VisionIdPattern.IsMatch(document.Id), "id must match vision.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        Require(document.Metadata is not null, "metadata is required.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(IsIsoDate(metadata.LastReviewed), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(!string.IsNullOrWhiteSpace(document.ProblemStatement), "problemStatement is required.", errors);
        Require(!string.IsNullOrWhiteSpace(document.Mission), "mission is required.", errors);
        Require(targetUsers.Count > 0, "targetUsers must contain at least one actor.", errors);
        Require(successMetrics.Count > 0, "successMetrics must contain at least one metric.", errors);
        RequireHasValues(constraints, "constraints must contain at least one constraint.", errors);
        RequireHasValues(nonGoals, "nonGoals must contain at least one non-goal.", errors);
        Require(document.ValueProposition is not null, "valueProposition is required.", errors);
        Require(!string.IsNullOrWhiteSpace(valueProposition.Summary), "valueProposition.summary is required.", errors);
        RequireHasValues(differentiators, "valueProposition.differentiators must contain at least one item.", errors);

        for (var i = 0; i < targetUsers.Count; i++)
        {
            var user = targetUsers[i];
            Require(TargetUserIdPattern.IsMatch(user.Id), $"targetUsers[{i}].id must match user.<name> convention.", errors);
            Require(!string.IsNullOrWhiteSpace(user.Name), $"targetUsers[{i}].name is required.", errors);
            RequireHasValues(user.Needs, $"targetUsers[{i}].needs must contain at least one need.", errors);
        }

        for (var i = 0; i < successMetrics.Count; i++)
        {
            var metric = successMetrics[i];
            Require(MetricIdPattern.IsMatch(metric.Id), $"successMetrics[{i}].id must match metric.<name> convention.", errors);
            Require(!string.IsNullOrWhiteSpace(metric.Name), $"successMetrics[{i}].name is required.", errors);
            Require(!string.IsNullOrWhiteSpace(metric.Target), $"successMetrics[{i}].target is required.", errors);
            Require(!string.IsNullOrWhiteSpace(metric.Measurement), $"successMetrics[{i}].measurement is required.", errors);
        }

        for (var i = 0; i < capabilityRefs.Count; i++)
        {
            var capabilityId = capabilityRefs[i];
            Require(
                CapabilityIdPattern.IsMatch(capabilityId),
                $"trace.downstream.capabilities[{i}] must match capability.<name> convention.",
                errors);
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "vision", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "vision", "schema", "vision.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/vision/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/vision/schema/vision.schema.json.");

        VisionSpecificationIndex index;
        try
        {
            index = VisionSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/vision/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Vision index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "VisionIndex", StringComparison.Ordinal), "Vision index kind must be 'VisionIndex'.", errors);
        Require(index.Entries.Count > 0, "Vision index must contain at least one entry.", errors);

        var capabilityIds = LoadCapabilityIds(repoRoot, errors);

        foreach (var entry in index.Entries)
        {
            if (!VisionIdPattern.IsMatch(entry.Id))
            {
                errors.Add($"Vision index entry id '{entry.Id}' does not match vision.<name> convention.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                errors.Add($"Vision index entry '{entry.Id}' is missing a path.");
                continue;
            }

            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Vision spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            VisionSpecification spec;
            try
            {
                spec = VisionSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse vision spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateUpstreamReferences(repoRoot, spec, entry.Path, errors);
            ValidateCapabilityReferences(capabilityIds, spec, entry.Path, errors);
        }

        return errors;
    }

    private static HashSet<string> LoadCapabilityIds(string repoRoot, List<string> errors)
    {
        var capabilityIndexPath = Path.Combine(repoRoot, "specs", "capabilities", "index.yaml");
        if (!File.Exists(capabilityIndexPath))
            return [];

        try
        {
            var index = VisionSpecificationParser.ParseCapabilityIndexFile(capabilityIndexPath);
            return index.Entries
                .Select(entry => entry.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/capabilities/index.yaml: {ex.Message}");
            return [];
        }
    }

    private static void ValidateUpstreamReferences(
        string repoRoot,
        VisionSpecification spec,
        string specPath,
        List<string> errors)
    {
        var upstreamReferences = spec.Trace?.Upstream ?? [];

        foreach (var reference in upstreamReferences)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                errors.Add($"{specPath}: trace.upstream entries must not be blank.");
                continue;
            }

            if (Uri.TryCreate(reference, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                continue;

            var fullPath = Path.Combine(repoRoot, reference.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                errors.Add($"{specPath}: trace.upstream reference '{reference}' does not resolve to a repository file.");
        }
    }

    private static void ValidateCapabilityReferences(
        HashSet<string> capabilityIds,
        VisionSpecification spec,
        string specPath,
        List<string> errors)
    {
        var capabilityReferences = spec.Trace?.Downstream?.Capabilities ?? [];

        if (capabilityReferences.Count == 0)
            return;

        if (capabilityIds.Count == 0)
        {
            errors.Add($"{specPath}: trace.downstream.capabilities declared but specs/capabilities/index.yaml is missing.");
            return;
        }

        foreach (var capabilityId in capabilityReferences)
        {
            if (!capabilityIds.Contains(capabilityId))
                errors.Add($"{specPath}: capability reference '{capabilityId}' was not found in specs/capabilities/index.yaml.");
        }
    }

    private static bool IsIsoDate(string value) =>
        DateOnly.TryParse(value, out _);

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition)
            errors.Add(message);
    }

    private static void RequireHasValues(IReadOnlyList<string>? values, string message, List<string> errors)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
            errors.Add(message);
    }
}
