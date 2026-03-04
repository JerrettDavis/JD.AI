namespace JD.AI.Core.Skills;

/// <summary>
/// Skill source category used for precedence resolution.
/// </summary>
public enum SkillSourceKind
{
    Bundled = 0,
    Managed = 1,
    Workspace = 2,
}

/// <summary>
/// Final eligibility status for a discovered skill.
/// </summary>
public enum SkillEligibilityState
{
    Active,
    Excluded,
    Shadowed,
    Invalid,
}

/// <summary>
/// Canonical reason codes emitted when a skill is excluded or invalid.
/// </summary>
public static class SkillReasonCodes
{
    public const string None = "none";
    public const string InvalidFrontmatter = "invalid_frontmatter";
    public const string InvalidSchema = "invalid_schema";
    public const string ShadowedByPrecedence = "shadowed_by_precedence";
    public const string DisabledByConfig = "disabled_by_config";
    public const string BundledNotAllowlisted = "bundled_not_allowlisted";
    public const string OsMismatch = "os_mismatch";
    public const string MissingBinaries = "missing_bins";
    public const string MissingAnyBinary = "missing_any_bins";
    public const string MissingEnvironment = "missing_env";
    public const string MissingConfig = "missing_config";
}

/// <summary>
/// Skill source directory and precedence metadata.
/// </summary>
/// <param name="Name">Human-friendly source name (for status output).</param>
/// <param name="RootPath">Directory containing skill subdirectories.</param>
/// <param name="Kind">Source precedence tier.</param>
/// <param name="Order">Tie-breaker within the same tier (higher wins).</param>
public sealed record SkillSourceDirectory(
    string Name,
    string RootPath,
    SkillSourceKind Kind,
    int Order = 0);

/// <summary>
/// Parsed and validated metadata from a SKILL.md frontmatter block.
/// </summary>
public sealed record SkillMetadata(
    string Name,
    string Description,
    string SkillKey,
    bool Always,
    string? PrimaryEnv,
    IReadOnlyList<string> Os,
    IReadOnlyList<string> RequiredBins,
    IReadOnlyList<string> RequiredAnyBins,
    IReadOnlyList<string> RequiredEnvironment,
    IReadOnlyList<string> RequiredConfigPaths);

/// <summary>
/// An eligible skill selected after precedence and gating.
/// </summary>
public sealed record ActiveSkill(
    string Name,
    string SkillKey,
    string DirectoryPath,
    string SkillFilePath,
    SkillSourceDirectory Source,
    SkillMetadata Metadata);

/// <summary>
/// Status row for a discovered skill.
/// </summary>
public sealed record SkillStatus(
    string Name,
    string SkillKey,
    string SkillFilePath,
    SkillSourceDirectory Source,
    SkillEligibilityState State,
    string ReasonCode,
    string? ReasonDetail);

/// <summary>
/// Immutable snapshot of discovered skill state.
/// </summary>
public sealed record SkillSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string Fingerprint,
    IReadOnlyList<ActiveSkill> ActiveSkills,
    IReadOnlyList<SkillStatus> Statuses);

/// <summary>
/// Per-skill runtime overrides from configuration.
/// </summary>
public sealed class SkillEntryConfig
{
    public bool? Enabled { get; init; }

    public string? ApiKey { get; init; }

    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public System.Text.Json.Nodes.JsonNode? Config { get; init; }

    public static SkillEntryConfig Merge(SkillEntryConfig? lower, SkillEntryConfig? higher)
    {
        if (lower is null && higher is null)
            return new SkillEntryConfig();

        if (lower is null)
            return Clone(higher!);

        if (higher is null)
            return Clone(lower);

        var mergedEnv = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in lower.Env)
            mergedEnv[kvp.Key] = kvp.Value;
        foreach (var kvp in higher.Env)
            mergedEnv[kvp.Key] = kvp.Value;

        return new SkillEntryConfig
        {
            Enabled = higher.Enabled ?? lower.Enabled,
            ApiKey = higher.ApiKey ?? lower.ApiKey,
            Env = mergedEnv,
            Config = higher.Config?.DeepClone() ?? lower.Config?.DeepClone(),
        };
    }

    private static SkillEntryConfig Clone(SkillEntryConfig source)
    {
        return new SkillEntryConfig
        {
            Enabled = source.Enabled,
            ApiKey = source.ApiKey,
            Env = new Dictionary<string, string>(source.Env, StringComparer.Ordinal),
            Config = source.Config?.DeepClone(),
        };
    }
}

/// <summary>
/// Merged runtime skill configuration.
/// </summary>
public sealed class SkillRuntimeConfig
{
    public bool Watch { get; init; } = true;

    public int WatchDebounceMs { get; init; } = 250;

    public ISet<string> AllowBundled { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SkillEntryConfig> Entries { get; init; } =
        new Dictionary<string, SkillEntryConfig>(StringComparer.OrdinalIgnoreCase);

    public System.Text.Json.Nodes.JsonNode? RootConfig { get; init; }

    public SkillEntryConfig GetEntry(string key)
    {
        if (Entries.TryGetValue(key, out var entry))
            return entry;

        return new SkillEntryConfig();
    }
}
