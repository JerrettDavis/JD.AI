using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class GovernanceSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public GovernanceMetadata Metadata { get; set; } = new();
    public string OwnershipModel { get; set; } = string.Empty;
    public IList<GovernanceChangeProcess> ChangeProcess { get; init; } = [];
    public IList<GovernanceApprovalGate> ApprovalGates { get; init; } = [];
    public GovernanceReleasePolicy ReleasePolicy { get; set; } = new();
    public IList<string> AuditRequirements { get; init; } = [];
    public GovernanceTraceability Trace { get; set; } = new();
}

public sealed class GovernanceMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class GovernanceChangeProcess
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RequiredApprovals { get; set; }
}

public sealed class GovernanceApprovalGate
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public IList<string> Criteria { get; init; } = [];
}

public sealed class GovernanceReleasePolicy
{
    public string Cadence { get; set; } = string.Empty;
    public string BranchStrategy { get; set; } = string.Empty;
    public string HotfixProcess { get; set; } = string.Empty;
}

public sealed class GovernanceTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public GovernanceDownstreamTrace Downstream { get; set; } = new();
}

public sealed class GovernanceDownstreamTrace
{
    public IList<string> AllSpecTypes { get; init; } = [];
}

public sealed class GovernanceSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<GovernanceSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class GovernanceSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class GovernanceSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static GovernanceSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<GovernanceSpecification>(yaml);
    }

    public static GovernanceSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static GovernanceSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<GovernanceSpecificationIndex>(yaml);
    }

    public static GovernanceSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class GovernanceSpecificationValidator
{
    private static readonly Regex GovernanceIdPattern = new(
        "^governance\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    private static readonly HashSet<string> AllowedOwnershipModels =
    [
        "codeowners",
        "team-based",
        "individual",
        "shared",
    ];

    private static readonly HashSet<string> AllowedGateTypes =
    [
        "automated",
        "manual",
        "hybrid",
    ];

    private static readonly HashSet<string> AllowedCadences =
    [
        "continuous",
        "scheduled",
        "manual",
    ];

    public static IReadOnlyList<string> Validate(GovernanceSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new GovernanceMetadata();
        var releasePolicy = document.ReleasePolicy ?? new GovernanceReleasePolicy();
        var trace = document.Trace ?? new GovernanceTraceability();
        var downstream = trace.Downstream ?? new GovernanceDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Governance", StringComparison.Ordinal), "kind must be 'Governance'.", errors);
        Require(GovernanceIdPattern.IsMatch(document.Id), "id must match governance.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);

        Require(AllowedOwnershipModels.Contains(document.OwnershipModel), "ownershipModel must be one of: codeowners, team-based, individual, shared.", errors);

        Require(document.ChangeProcess.Count > 0, "changeProcess must contain at least one entry.", errors);
        ValidateChangeProcesses(document.ChangeProcess, errors);

        Require(document.ApprovalGates.Count > 0, "approvalGates must contain at least one entry.", errors);
        ValidateApprovalGates(document.ApprovalGates, errors);

        Require(AllowedCadences.Contains(releasePolicy.Cadence), "releasePolicy.cadence must be one of: continuous, scheduled, manual.", errors);
        Require(!string.IsNullOrWhiteSpace(releasePolicy.BranchStrategy), "releasePolicy.branchStrategy is required.", errors);

        RequireHasValues(document.AuditRequirements, "auditRequirements must contain at least one entry.", errors);

        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        Require(downstream.AllSpecTypes.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.allSpecTypes entries must not be blank.", errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "governance", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "governance", "schema", "governance.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/governance/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/governance/schema/governance.schema.json.");

        GovernanceSpecificationIndex index;
        try
        {
            index = GovernanceSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/governance/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Governance index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "GovernanceIndex", StringComparison.Ordinal), "Governance index kind must be 'GovernanceIndex'.", errors);
        Require(index.Entries.Count > 0, "Governance index must contain at least one entry.", errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Governance spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            GovernanceSpecification spec;
            try
            {
                spec = GovernanceSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse governance spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.AllSpecTypes ?? [], entry.Path, "trace.downstream.allSpecTypes", errors);
        }

        return errors;
    }

    private static void ValidateChangeProcesses(IList<GovernanceChangeProcess> processes, List<string> errors)
    {
        for (var i = 0; i < processes.Count; i++)
        {
            var process = processes[i] ?? new GovernanceChangeProcess();
            var prefix = $"changeProcess[{i}]";

            Require(!string.IsNullOrWhiteSpace(process.Name), $"{prefix}.name is required.", errors);
            Require(!string.IsNullOrWhiteSpace(process.Description), $"{prefix}.description is required.", errors);
        }
    }

    private static void ValidateApprovalGates(IList<GovernanceApprovalGate> gates, List<string> errors)
    {
        for (var i = 0; i < gates.Count; i++)
        {
            var gate = gates[i] ?? new GovernanceApprovalGate();
            var prefix = $"approvalGates[{i}]";

            Require(!string.IsNullOrWhiteSpace(gate.Name), $"{prefix}.name is required.", errors);
            Require(AllowedGateTypes.Contains(gate.Type), $"{prefix}.type must be one of: automated, manual, hybrid.", errors);
            RequireHasValues(gate.Criteria, $"{prefix}.criteria must contain at least one entry.", errors);
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
