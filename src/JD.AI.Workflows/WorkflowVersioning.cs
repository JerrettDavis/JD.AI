using System.Globalization;
using System.Text.RegularExpressions;

namespace JD.AI.Workflows;

internal static partial class WorkflowVersioning
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    internal static WorkflowSemVersion ParseVersionOrThrow(string version)
    {
        if (!WorkflowSemVersion.TryParse(version, out var parsed))
            throw new InvalidDataException(
                $"Workflow version '{version}' is not valid SemVer. Expected: major.minor[.patch][-prerelease][+build].");

        return parsed;
    }

    internal static AgentWorkflowDefinition? SelectVersion(
        IEnumerable<AgentWorkflowDefinition> definitions,
        string? selector)
    {
        var versioned = definitions
            .Select(def =>
            {
                var ok = WorkflowSemVersion.TryParse(def.Version, out var parsed);
                return (Definition: def, Parsed: parsed, Valid: ok);
            })
            .Where(x => x.Valid)
            .ToList();

        if (versioned.Count == 0)
            return null;

        var parsedSelector = WorkflowVersionSelector.Parse(selector);

        var candidates = versioned
            .Where(v => parsedSelector.Matches(v.Parsed))
            .OrderByDescending(v => v.Parsed)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var nonDeprecated = candidates.FirstOrDefault(v => !v.Definition.IsDeprecated);
        return nonDeprecated.Definition ?? candidates[0].Definition;
    }

    internal static IReadOnlyList<string> DetectBreakingChanges(
        AgentWorkflowDefinition previous,
        AgentWorkflowDefinition current)
    {
        var previousIndex = FlattenStepSignatures(previous.Steps)
            .ToDictionary(x => x.Path, x => x, NameComparer);
        var currentIndex = FlattenStepSignatures(current.Steps)
            .ToDictionary(x => x.Path, x => x, NameComparer);

        var changes = new List<string>();
        foreach (var (path, prior) in previousIndex)
        {
            if (!currentIndex.TryGetValue(path, out var next))
            {
                changes.Add($"Removed step '{prior.Name}' at '{path}'.");
                continue;
            }

            if (prior.Kind != next.Kind)
                changes.Add($"Changed step kind at '{path}' ({prior.Kind} -> {next.Kind}).");

            if (!string.Equals(prior.Target, next.Target, StringComparison.Ordinal))
                changes.Add($"Changed step target at '{path}' ('{prior.Target}' -> '{next.Target}').");

            if (!string.Equals(prior.Condition, next.Condition, StringComparison.Ordinal))
                changes.Add($"Changed step condition at '{path}'.");

            if (!prior.AllowedPlugins.SetEquals(next.AllowedPlugins))
                changes.Add($"Changed allowed plugins at '{path}'.");
        }

        return changes;
    }

    private static IEnumerable<StepSignature> FlattenStepSignatures(
        IEnumerable<AgentStepDefinition> steps,
        string pathPrefix = "")
    {
        var index = 0;
        foreach (var step in steps)
        {
            var path = string.IsNullOrEmpty(pathPrefix)
                ? $"{index}:{step.Name}"
                : $"{pathPrefix}/{index}:{step.Name}";

            yield return new StepSignature(
                path,
                step.Name,
                step.Kind,
                step.Target ?? string.Empty,
                step.Condition ?? string.Empty,
                step.AllowedPlugins.ToHashSet(StringComparer.OrdinalIgnoreCase));

            foreach (var child in FlattenStepSignatures(step.SubSteps, path))
                yield return child;

            index++;
        }
    }

    private sealed record StepSignature(
        string Path,
        string Name,
        AgentStepKind Kind,
        string Target,
        string Condition,
        HashSet<string> AllowedPlugins);

    internal sealed class WorkflowVersionSelector
    {
        private readonly WorkflowSemVersion? _exact;
        private readonly IReadOnlyList<Comparator> _comparators;

        private WorkflowVersionSelector(WorkflowSemVersion? exact, IReadOnlyList<Comparator> comparators)
        {
            _exact = exact;
            _comparators = comparators;
        }

        public static WorkflowVersionSelector Parse(string? selector)
        {
            if (string.IsNullOrWhiteSpace(selector) ||
                string.Equals(selector, "latest", StringComparison.OrdinalIgnoreCase))
            {
                return new WorkflowVersionSelector(exact: null, comparators: []);
            }

            var trimmed = selector.Trim();

            if (trimmed.StartsWith('^'))
            {
                var floor = ParseVersionOrThrow(trimmed[1..].Trim());
                return new WorkflowVersionSelector(
                    exact: null,
                    comparators:
                    [
                        new Comparator(ComparisonOp.GreaterThanOrEqual, floor),
                        new Comparator(ComparisonOp.LessThan, floor.CaretUpperBound()),
                    ]);
            }

            if (trimmed.StartsWith('~'))
            {
                var floor = ParseVersionOrThrow(trimmed[1..].Trim());
                return new WorkflowVersionSelector(
                    exact: null,
                    comparators:
                    [
                        new Comparator(ComparisonOp.GreaterThanOrEqual, floor),
                        new Comparator(ComparisonOp.LessThan, floor.TildeUpperBound()),
                    ]);
            }

            var tokens = trimmed
                .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length == 1 && WorkflowSemVersion.TryParse(tokens[0], out var exact))
                return new WorkflowVersionSelector(exact, []);

            var comparators = new List<Comparator>();
            foreach (var token in tokens)
                comparators.Add(Comparator.Parse(token));

            return new WorkflowVersionSelector(exact: null, comparators);
        }

        public bool Matches(WorkflowSemVersion version)
        {
            if (_exact is not null)
                return version == _exact;

            if (_comparators.Count == 0)
                return true;

            return _comparators.All(c => c.Matches(version));
        }
    }

    private enum ComparisonOp
    {
        Equal,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    private sealed record Comparator(ComparisonOp Op, WorkflowSemVersion Version)
    {
        public static Comparator Parse(string token)
        {
            var value = token.Trim();
            if (value.Length == 0)
                throw new InvalidDataException("Empty workflow version constraint token.");

            var op = value switch
            {
                _ when value.StartsWith(">=", StringComparison.Ordinal) => ComparisonOp.GreaterThanOrEqual,
                _ when value.StartsWith("<=", StringComparison.Ordinal) => ComparisonOp.LessThanOrEqual,
                _ when value.StartsWith('>') => ComparisonOp.GreaterThan,
                _ when value.StartsWith('<') => ComparisonOp.LessThan,
                _ when value.StartsWith('=') => ComparisonOp.Equal,
                _ => throw new InvalidDataException(
                    $"Unsupported workflow version constraint token '{token}'. " +
                    "Supported: ^, ~, =, >, >=, <, <="),
            };

            var start = op is ComparisonOp.GreaterThanOrEqual or ComparisonOp.LessThanOrEqual ? 2 : 1;
            var version = ParseVersionOrThrow(value[start..].Trim());
            return new Comparator(op, version);
        }

        public bool Matches(WorkflowSemVersion candidate)
        {
            var cmp = candidate.CompareTo(Version);
            return Op switch
            {
                ComparisonOp.Equal => cmp == 0,
                ComparisonOp.GreaterThan => cmp > 0,
                ComparisonOp.GreaterThanOrEqual => cmp >= 0,
                ComparisonOp.LessThan => cmp < 0,
                ComparisonOp.LessThanOrEqual => cmp <= 0,
                _ => false,
            };
        }
    }
}

internal readonly partial record struct WorkflowSemVersion(
    int Major,
    int Minor,
    int Patch,
    IReadOnlyList<string> PreRelease,
    string? BuildMetadata) : IComparable<WorkflowSemVersion>
{
    public static bool operator <(WorkflowSemVersion left, WorkflowSemVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator >(WorkflowSemVersion left, WorkflowSemVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator <=(WorkflowSemVersion left, WorkflowSemVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >=(WorkflowSemVersion left, WorkflowSemVersion right) =>
        left.CompareTo(right) >= 0;

    public static bool TryParse(string? value, out WorkflowSemVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var match = SemVerRegex().Match(value.Trim());
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        var patchGroup = match.Groups["patch"].Value;
        var patch = 0;
        if (patchGroup.Length > 0 &&
            !int.TryParse(patchGroup, NumberStyles.None, CultureInfo.InvariantCulture, out patch))
        {
            return false;
        }

        var preRelease = match.Groups["pre"].Success
            ? match.Groups["pre"].Value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        var build = match.Groups["build"].Success ? match.Groups["build"].Value : null;

        version = new WorkflowSemVersion(major, minor, patch, preRelease, build);
        return true;
    }

    public int CompareTo(WorkflowSemVersion other)
    {
        var majorCmp = Major.CompareTo(other.Major);
        if (majorCmp != 0) return majorCmp;

        var minorCmp = Minor.CompareTo(other.Minor);
        if (minorCmp != 0) return minorCmp;

        var patchCmp = Patch.CompareTo(other.Patch);
        if (patchCmp != 0) return patchCmp;

        var thisHasPre = PreRelease.Count > 0;
        var otherHasPre = other.PreRelease.Count > 0;
        if (!thisHasPre && !otherHasPre) return 0;
        if (!thisHasPre) return 1;
        if (!otherHasPre) return -1;

        var max = Math.Max(PreRelease.Count, other.PreRelease.Count);
        for (var i = 0; i < max; i++)
        {
            if (i >= PreRelease.Count) return -1;
            if (i >= other.PreRelease.Count) return 1;

            var left = PreRelease[i];
            var right = other.PreRelease[i];

            var leftNum = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftInt);
            var rightNum = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightInt);

            if (leftNum && rightNum)
            {
                var cmp = leftInt.CompareTo(rightInt);
                if (cmp != 0) return cmp;
                continue;
            }

            if (leftNum != rightNum)
                return leftNum ? -1 : 1;

            var lexicalCmp = string.Compare(left, right, StringComparison.Ordinal);
            if (lexicalCmp != 0) return lexicalCmp;
        }

        return 0;
    }

    public WorkflowSemVersion CaretUpperBound()
    {
        if (Major > 0)
            return new WorkflowSemVersion(Major + 1, 0, 0, [], null);

        if (Minor > 0)
            return new WorkflowSemVersion(0, Minor + 1, 0, [], null);

        return new WorkflowSemVersion(0, 0, Patch + 1, [], null);
    }

    public WorkflowSemVersion TildeUpperBound() =>
        new(Major, Minor + 1, 0, [], null);

    [GeneratedRegex(
        "^(?<major>0|[1-9]\\d*)\\.(?<minor>0|[1-9]\\d*)(?:\\.(?<patch>0|[1-9]\\d*))?(?:-(?<pre>[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?(?:\\+(?<build>[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex SemVerRegex();
}
