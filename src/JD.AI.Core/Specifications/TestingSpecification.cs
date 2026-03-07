using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class TestingSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public BehaviorMetadata Metadata { get; set; } = new();
    public IList<string> VerificationLevels { get; init; } = [];
    public IList<string> BehaviorRefs { get; init; } = [];
    public IList<string> QualityRefs { get; init; } = [];
    public IList<TestingCoverageTarget> CoverageTargets { get; init; } = [];
    public IList<TestingGenerationRule> GenerationRules { get; init; } = [];
    public TestingTraceability Trace { get; set; } = new();
}

public sealed class TestingCoverageTarget
{
    public string Scope { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
}

public sealed class TestingGenerationRule
{
    public string Source { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
}

public sealed class TestingTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public TestingDownstreamTrace Downstream { get; set; } = new();
}

public sealed class TestingDownstreamTrace
{
    public IList<string> Ci { get; init; } = [];
    public IList<string> Release { get; init; } = [];
}

public sealed class TestingSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<BehaviorSpecificationIndexEntry> Entries { get; init; } = [];
}

public static class TestingSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static TestingSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<TestingSpecification>(yaml);
    }

    public static TestingSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static TestingSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<TestingSpecificationIndex>(yaml);
    }

    public static TestingSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class TestingSpecificationValidator
{
    private static readonly Regex TestingIdPattern = new(
        "^testing\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BehaviorIdPattern = new(
        "^behavior\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    private static readonly HashSet<string> AllowedVerificationLevels =
    [
        "unit",
        "integration",
        "acceptance",
        "performance",
        "security",
        "e2e",
    ];

    private static readonly HashSet<string> AllowedStrategies =
    [
        "generated",
        "manual",
        "hybrid",
    ];

    public static IReadOnlyList<string> Validate(TestingSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new BehaviorMetadata();
        var trace = document.Trace ?? new TestingTraceability();
        var downstream = trace.Downstream ?? new TestingDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Testing", StringComparison.Ordinal), "kind must be 'Testing'.", errors);
        Require(TestingIdPattern.IsMatch(document.Id), "id must match testing.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);

        Require(document.VerificationLevels.Count > 0, "verificationLevels must contain at least one level.", errors);
        foreach (var level in document.VerificationLevels)
            Require(AllowedVerificationLevels.Contains(level), $"verificationLevels entry '{level}' is not allowed. Must be one of: unit, integration, acceptance, performance, security, e2e.", errors);

        foreach (var behaviorRef in document.BehaviorRefs)
            Require(BehaviorIdPattern.IsMatch(behaviorRef), $"behaviorRefs entry '{behaviorRef}' must match behavior.<name> convention.", errors);

        foreach (var qualityRef in document.QualityRefs)
            Require(QualityIdPattern.IsMatch(qualityRef), $"qualityRefs entry '{qualityRef}' must match quality.<name> convention.", errors);

        Require(document.CoverageTargets.Count > 0, "coverageTargets must contain at least one target.", errors);
        for (var i = 0; i < document.CoverageTargets.Count; i++)
        {
            var target = document.CoverageTargets[i] ?? new TestingCoverageTarget();
            Require(!string.IsNullOrWhiteSpace(target.Scope), $"coverageTargets[{i}].scope is required.", errors);
            Require(!string.IsNullOrWhiteSpace(target.Target), $"coverageTargets[{i}].target is required.", errors);
        }

        foreach (var rule in document.GenerationRules)
        {
            var r = rule ?? new TestingGenerationRule();
            Require(AllowedStrategies.Contains(r.Strategy), $"generationRules entry strategy '{r.Strategy}' is not allowed. Must be one of: generated, manual, hybrid.", errors);
        }

        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.Ci.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.ci entries must not be blank.", errors);
        Require(downstream.Release.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.release entries must not be blank.", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "testing", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "testing", "schema", "testing.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/testing/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/testing/schema/testing.schema.json.");

        TestingSpecificationIndex index;
        try
        {
            index = TestingSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/testing/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Testing index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "TestingIndex", StringComparison.Ordinal), "Testing index kind must be 'TestingIndex'.", errors);
        Require(index.Entries.Count > 0, "Testing index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Testing spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            TestingSpecification spec;
            try
            {
                spec = TestingSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse testing spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Ci ?? [], entry.Path, "trace.downstream.ci", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Release ?? [], entry.Path, "trace.downstream.release", errors);
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
