using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace JD.AI.Workflows;

/// <summary>
/// Semantic version for workflow definitions. Supports parsing, comparison,
/// and range constraints (<c>^1.0</c>, <c>~1.2</c>, <c>&gt;=1.0.0</c>).
/// </summary>
public readonly partial struct WorkflowVersion : IComparable<WorkflowVersion>, IEquatable<WorkflowVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }

    public WorkflowVersion(int major, int minor = 0, int patch = 0, string? preRelease = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        ArgumentOutOfRangeException.ThrowIfNegative(patch);
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrWhiteSpace(preRelease) ? null : preRelease;
    }

    /// <summary>Parses a version string like "1.2.3" or "1.2.3-beta.1".</summary>
    public static WorkflowVersion Parse(string version)
    {
        if (!TryParse(version, out var result))
            throw new FormatException($"Invalid semantic version: '{version}'");
        return result;
    }

    /// <summary>Attempts to parse a version string.</summary>
    public static bool TryParse(string? version, [NotNullWhen(true)] out WorkflowVersion result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(version)) return false;

        var match = SemVerRegex().Match(version.Trim());
        if (!match.Success) return false;

        var major = int.Parse(match.Groups["major"].Value);
        var minor = match.Groups["minor"].Success ? int.Parse(match.Groups["minor"].Value) : 0;
        var patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;
        var pre = match.Groups["pre"].Success ? match.Groups["pre"].Value : null;

        result = new WorkflowVersion(major, minor, patch, pre);
        return true;
    }

    public int CompareTo(WorkflowVersion other)
    {
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;

        // Pre-release versions have lower precedence than release
        if (PreRelease is null && other.PreRelease is null) return 0;
        if (PreRelease is null) return 1;  // release > pre-release
        if (other.PreRelease is null) return -1;

        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);
    }

    public bool Equals(WorkflowVersion other) =>
        Major == other.Major && Minor == other.Minor && Patch == other.Patch
        && string.Equals(PreRelease, other.PreRelease, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is WorkflowVersion v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);
    public override string ToString() =>
        PreRelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";

    public static bool operator ==(WorkflowVersion left, WorkflowVersion right) => left.Equals(right);
    public static bool operator !=(WorkflowVersion left, WorkflowVersion right) => !left.Equals(right);
    public static bool operator <(WorkflowVersion left, WorkflowVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(WorkflowVersion left, WorkflowVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(WorkflowVersion left, WorkflowVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(WorkflowVersion left, WorkflowVersion right) => left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^(?<major>0|[1-9]\d*)(?:\.(?<minor>0|[1-9]\d*))?(?:\.(?<patch>0|[1-9]\d*))?(?:-(?<pre>[0-9A-Za-z\-]+(?:\.[0-9A-Za-z\-]+)*))?$")]
    private static partial Regex SemVerRegex();
}

/// <summary>
/// Represents a version constraint like <c>^1.0</c>, <c>~1.2</c>,
/// <c>&gt;=1.0.0 &lt;2.0.0</c>, or an exact version.
/// </summary>
public sealed class VersionConstraint
{
    private readonly Func<WorkflowVersion, bool> _predicate;
    private readonly string _expression;

    private VersionConstraint(string expression, Func<WorkflowVersion, bool> predicate)
    {
        _expression = expression;
        _predicate = predicate;
    }

    /// <summary>Returns true if the given version satisfies this constraint.</summary>
    public bool IsSatisfiedBy(WorkflowVersion version) => _predicate(version);

    public override string ToString() => _expression;

    /// <summary>
    /// Parses a constraint expression. Supported forms:
    /// <list type="bullet">
    /// <item><c>1.2.3</c> — exact match</item>
    /// <item><c>^1.2.3</c> — compatible (same major, >= minor.patch)</item>
    /// <item><c>~1.2.3</c> — approximately (same major.minor, >= patch)</item>
    /// <item><c>&gt;=1.0.0</c>, <c>&lt;2.0.0</c> — comparison operators</item>
    /// <item><c>&gt;=1.0.0 &lt;2.0.0</c> — combined range (space-separated, all must match)</item>
    /// <item><c>*</c> — matches any version</item>
    /// </list>
    /// </summary>
    public static VersionConstraint Parse(string expression)
    {
        expression = expression.Trim();

        if (string.Equals(expression, "*", StringComparison.Ordinal))
            return new VersionConstraint(expression, _ => true);

        // Combined range (space-separated constraints)
        if (expression.Contains(' '))
        {
            var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var constraints = parts.Select(Parse).ToArray();
            return new VersionConstraint(expression, v => constraints.All(c => c.IsSatisfiedBy(v)));
        }

        // Caret: ^1.2.3 — compatible with 1.x.x (same major)
        if (expression.StartsWith('^'))
        {
            var baseVersion = WorkflowVersion.Parse(expression[1..]);
            return new VersionConstraint(expression, v =>
                v.Major == baseVersion.Major && v >= baseVersion);
        }

        // Tilde: ~1.2.3 — approximately 1.2.x (same major.minor)
        if (expression.StartsWith('~'))
        {
            var baseVersion = WorkflowVersion.Parse(expression[1..]);
            return new VersionConstraint(expression, v =>
                v.Major == baseVersion.Major && v.Minor == baseVersion.Minor && v >= baseVersion);
        }

        // Comparison operators
        if (expression.StartsWith(">="))
        {
            var ver = WorkflowVersion.Parse(expression[2..]);
            return new VersionConstraint(expression, v => v >= ver);
        }
        if (expression.StartsWith("<="))
        {
            var ver = WorkflowVersion.Parse(expression[2..]);
            return new VersionConstraint(expression, v => v <= ver);
        }
        if (expression.StartsWith('>'))
        {
            var ver = WorkflowVersion.Parse(expression[1..]);
            return new VersionConstraint(expression, v => v > ver);
        }
        if (expression.StartsWith('<'))
        {
            var ver = WorkflowVersion.Parse(expression[1..]);
            return new VersionConstraint(expression, v => v < ver);
        }

        // Exact match
        var exact = WorkflowVersion.Parse(expression);
        return new VersionConstraint(expression, v => v == exact);
    }
}
