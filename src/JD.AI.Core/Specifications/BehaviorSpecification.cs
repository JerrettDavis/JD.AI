using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JD.AI.Core.Specifications;

public sealed class BehaviorSpecification
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public BehaviorMetadata Metadata { get; set; } = new();
    public string UseCaseRef { get; set; } = string.Empty;
    public IList<BehaviorScenario> BddScenarios { get; init; } = [];
    public BehaviorStateMachine StateMachine { get; set; } = new();
    public IList<string> Assertions { get; init; } = [];
    public BehaviorTraceability Trace { get; set; } = new();
}

public sealed class BehaviorMetadata
{
    public IList<string> Owners { get; init; } = [];
    public IList<string> Reviewers { get; init; } = [];
    public string LastReviewed { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
}

public sealed class BehaviorScenario
{
    public string Title { get; set; } = string.Empty;
    public IList<string> Given { get; init; } = [];
    public IList<string> When { get; init; } = [];
    public IList<string> Then { get; init; } = [];
}

public sealed class BehaviorStateMachine
{
    public string InitialState { get; set; } = string.Empty;
    public IList<BehaviorState> States { get; init; } = [];
    public IList<BehaviorTransition> Transitions { get; init; } = [];
}

public sealed class BehaviorState
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Terminal { get; set; }
}

public sealed class BehaviorTransition
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string On { get; set; } = string.Empty;
    public IList<string> Guards { get; init; } = [];
    public IList<string> Actions { get; init; } = [];
}

public sealed class BehaviorTraceability
{
    public IList<string> Upstream { get; init; } = [];
    public BehaviorDownstreamTrace Downstream { get; set; } = new();
}

public sealed class BehaviorDownstreamTrace
{
    public IList<string> Testing { get; init; } = [];
    public IList<string> Interfaces { get; init; } = [];
    public IList<string> Code { get; init; } = [];
}

public sealed class BehaviorSpecificationIndex
{
    public string ApiVersion { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public IList<BehaviorSpecificationIndexEntry> Entries { get; init; } = [];
}

public sealed class BehaviorSpecificationIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public static class BehaviorSpecificationParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static BehaviorSpecification Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<BehaviorSpecification>(yaml);
    }

    public static BehaviorSpecification ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static BehaviorSpecificationIndex ParseIndex(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Deserializer.Deserialize<BehaviorSpecificationIndex>(yaml);
    }

    public static BehaviorSpecificationIndex ParseIndexFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ParseIndex(File.ReadAllText(path));
    }
}

public static class BehaviorSpecificationValidator
{
    private static readonly Regex BehaviorIdPattern = new(
        "^behavior\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UseCaseIdPattern = new(
        "^usecase\\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft",
        "active",
        "deprecated",
        "retired",
    ];

    public static IReadOnlyList<string> Validate(BehaviorSpecification document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = document.Metadata ?? new BehaviorMetadata();
        var stateMachine = document.StateMachine ?? new BehaviorStateMachine();
        var trace = document.Trace ?? new BehaviorTraceability();
        var downstream = trace.Downstream ?? new BehaviorDownstreamTrace();
        var errors = new List<string>();

        Require(string.Equals(document.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(document.Kind, "Behavior", StringComparison.Ordinal), "kind must be 'Behavior'.", errors);
        Require(BehaviorIdPattern.IsMatch(document.Id), "id must match behavior.<name> convention.", errors);
        Require(document.Version >= 1, "version must be greater than or equal to 1.", errors);
        Require(AllowedStatuses.Contains(document.Status), "status must be one of: draft, active, deprecated, retired.", errors);
        RequireHasValues(metadata.Owners, "metadata.owners must contain at least one owner.", errors);
        RequireHasValues(metadata.Reviewers, "metadata.reviewers must contain at least one reviewer.", errors);
        Require(DateOnly.TryParse(metadata.LastReviewed, out _), "metadata.lastReviewed must be a valid ISO-8601 date.", errors);
        Require(!string.IsNullOrWhiteSpace(metadata.ChangeReason), "metadata.changeReason is required.", errors);
        Require(UseCaseIdPattern.IsMatch(document.UseCaseRef), "useCaseRef must match usecase.<name> convention.", errors);
        Require(document.BddScenarios.Count > 0, "bddScenarios must contain at least one scenario.", errors);
        RequireHasValues(document.Assertions, "assertions must contain at least one assertion.", errors);
        RequireHasValues(trace.Upstream, "trace.upstream must contain at least one upstream artifact.", errors);
        RequireHasValues(downstream.Testing, "trace.downstream.testing must contain at least one testing artifact.", errors);
        Require(downstream.Interfaces.All(value => !string.IsNullOrWhiteSpace(value)), "trace.downstream.interfaces entries must not be blank.", errors);
        RequireHasValues(downstream.Code, "trace.downstream.code must contain at least one code artifact.", errors);

        ValidateScenarios(document.BddScenarios, errors);
        ValidateStateMachine(stateMachine, errors);

        return errors;
    }

    public static IReadOnlyList<string> ValidateRepository(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);

        var errors = new List<string>();
        var indexPath = Path.Combine(repoRoot, "specs", "behavior", "index.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "behavior", "schema", "behavior.schema.json");

        if (!File.Exists(indexPath))
        {
            errors.Add("Missing specs/behavior/index.yaml.");
            return errors;
        }

        if (!File.Exists(schemaPath))
            errors.Add("Missing specs/behavior/schema/behavior.schema.json.");

        BehaviorSpecificationIndex index;
        try
        {
            index = BehaviorSpecificationParser.ParseIndexFile(indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/behavior/index.yaml: {ex.Message}");
            return errors;
        }

        Require(string.Equals(index.ApiVersion, "jdai.upss/v1", StringComparison.Ordinal), "Behavior index apiVersion must be 'jdai.upss/v1'.", errors);
        Require(string.Equals(index.Kind, "BehaviorIndex", StringComparison.Ordinal), "Behavior index kind must be 'BehaviorIndex'.", errors);
        Require(index.Entries.Count > 0, "Behavior index must contain at least one entry.", errors);

        var useCaseIds = LoadUseCaseIds(repoRoot, errors);

        foreach (var entry in index.Entries)
        {
            var specPath = Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(specPath))
            {
                errors.Add($"Behavior spec file not found for '{entry.Id}': {entry.Path}");
                continue;
            }

            BehaviorSpecification spec;
            try
            {
                spec = BehaviorSpecificationParser.ParseFile(specPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors.Add($"Unable to parse behavior spec '{entry.Path}': {ex.Message}");
                continue;
            }

            foreach (var validationError in Validate(spec))
                errors.Add($"{entry.Path}: {validationError}");

            if (!string.Equals(spec.Id, entry.Id, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec id '{spec.Id}' does not match index id '{entry.Id}'.");

            if (!string.Equals(spec.Status, entry.Status, StringComparison.Ordinal))
                errors.Add($"{entry.Path}: spec status '{spec.Status}' does not match index status '{entry.Status}'.");

            if (!useCaseIds.Contains(spec.UseCaseRef))
                errors.Add($"{entry.Path}: useCaseRef '{spec.UseCaseRef}' was not found in specs/usecases/index.yaml.");

            ValidateFileReferences(repoRoot, spec.Trace?.Upstream ?? [], entry.Path, "trace.upstream", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Testing ?? [], entry.Path, "trace.downstream.testing", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Interfaces ?? [], entry.Path, "trace.downstream.interfaces", errors);
            ValidateFileReferences(repoRoot, spec.Trace?.Downstream?.Code ?? [], entry.Path, "trace.downstream.code", errors);
        }

        return errors;
    }

    private static HashSet<string> LoadUseCaseIds(string repoRoot, List<string> errors)
    {
        var path = Path.Combine(repoRoot, "specs", "usecases", "index.yaml");
        if (!File.Exists(path))
        {
            errors.Add("Missing specs/usecases/index.yaml required for behavior validation.");
            return [];
        }

        try
        {
            var index = UseCaseSpecificationParser.ParseIndexFile(path);
            return index.Entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            errors.Add($"Unable to parse specs/usecases/index.yaml: {ex.Message}");
            return [];
        }
    }

    private static void ValidateScenarios(IList<BehaviorScenario> scenarios, List<string> errors)
    {
        for (var i = 0; i < scenarios.Count; i++)
        {
            var scenario = scenarios[i] ?? new BehaviorScenario();
            var prefix = $"bddScenarios[{i}]";

            Require(!string.IsNullOrWhiteSpace(scenario.Title), $"{prefix}.title is required.", errors);
            RequireHasValues(scenario.Given, $"{prefix}.given must contain at least one clause.", errors);
            RequireHasValues(scenario.When, $"{prefix}.when must contain at least one clause.", errors);
            RequireHasValues(scenario.Then, $"{prefix}.then must contain at least one clause.", errors);
        }
    }

    private static void ValidateStateMachine(BehaviorStateMachine stateMachine, List<string> errors)
    {
        Require(!string.IsNullOrWhiteSpace(stateMachine.InitialState), "stateMachine.initialState is required.", errors);
        Require(stateMachine.States.Count > 0, "stateMachine.states must contain at least one state.", errors);
        Require(stateMachine.Transitions.Count > 0, "stateMachine.transitions must contain at least one transition.", errors);

        var stateIds = stateMachine.States
            .Where(state => state is not null && !string.IsNullOrWhiteSpace(state.Id))
            .Select(state => state.Id)
            .ToList();

        Require(stateIds.Count == stateIds.Distinct(StringComparer.Ordinal).Count(), "stateMachine.states must have unique ids.", errors);
        Require(stateIds.Contains(stateMachine.InitialState, StringComparer.Ordinal), "stateMachine.initialState must match a defined state id.", errors);

        for (var i = 0; i < stateMachine.States.Count; i++)
        {
            var state = stateMachine.States[i] ?? new BehaviorState();
            Require(!string.IsNullOrWhiteSpace(state.Id), $"stateMachine.states[{i}].id is required.", errors);
        }

        for (var i = 0; i < stateMachine.Transitions.Count; i++)
        {
            var transition = stateMachine.Transitions[i] ?? new BehaviorTransition();
            var prefix = $"stateMachine.transitions[{i}]";

            Require(!string.IsNullOrWhiteSpace(transition.From), $"{prefix}.from is required.", errors);
            Require(!string.IsNullOrWhiteSpace(transition.To), $"{prefix}.to is required.", errors);
            Require(!string.IsNullOrWhiteSpace(transition.On), $"{prefix}.on is required.", errors);
            Require(stateIds.Contains(transition.From, StringComparer.Ordinal), $"{prefix}.from must reference a defined state.", errors);
            Require(stateIds.Contains(transition.To, StringComparer.Ordinal), $"{prefix}.to must reference a defined state.", errors);
            Require(transition.Guards.All(value => !string.IsNullOrWhiteSpace(value)), $"{prefix}.guards entries must not be blank.", errors);
            Require(transition.Actions.All(value => !string.IsNullOrWhiteSpace(value)), $"{prefix}.actions entries must not be blank.", errors);
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
