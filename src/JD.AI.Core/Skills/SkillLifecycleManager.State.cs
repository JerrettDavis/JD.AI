using System.Collections;
using System.Text;

namespace JD.AI.Core.Skills;

public sealed partial class SkillLifecycleManager
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IReadOnlyList<SkillSourceDirectory> _sources;
    private readonly string? _userConfigPath;
    private readonly string? _workspaceConfigPath;
    private readonly Func<string, bool> _binaryExists;
    private readonly Func<bool> _isWindows;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<IDictionary> _getEnvironmentVariables;
    private readonly Func<string> _platformProvider;
    private readonly Lock _lock = new();
    private readonly List<FileSystemWatcher> _watchers = [];

    private SkillSnapshot? _snapshot;
    private SkillRuntimeConfig _runtimeConfig = new();
    private bool _watchersEnabled;
    private int _watchDebounceMs = 250;
    private DateTimeOffset _lastWatchEventUtc = DateTimeOffset.MinValue;

    public SkillLifecycleManager(
        IEnumerable<SkillSourceDirectory> sources,
        string? userConfigPath = null,
        string? workspaceConfigPath = null,
        Func<string, bool>? binaryExists = null,
        Func<bool>? isWindows = null,
        Func<string, string?>? getEnvironmentVariable = null,
        Func<IDictionary>? getEnvironmentVariables = null,
        Func<string>? platformProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = sources.ToArray();
        _userConfigPath = userConfigPath;
        _workspaceConfigPath = workspaceConfigPath;
        _isWindows = isWindows ?? OperatingSystem.IsWindows;
        _binaryExists = binaryExists ?? BinaryExistsOnPath;
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        _getEnvironmentVariables = getEnvironmentVariables ?? Environment.GetEnvironmentVariables;
        _platformProvider = platformProvider ?? DetectPlatform;
    }

    /// <summary>
    /// Returns the current snapshot, refreshing if needed.
    /// </summary>
    public SkillSnapshot GetSnapshot()
    {
        TryRefresh(out var snapshot);
        return snapshot;
    }

    /// <summary>
    /// Refreshes skill state when underlying files/config/environment changed.
    /// </summary>
    /// <returns><c>true</c> when the active snapshot changed.</returns>
    public bool TryRefresh(out SkillSnapshot snapshot)
    {
        lock (_lock)
        {
            var runtimeConfig = SkillRuntimeConfigLoader.Load(_userConfigPath, _workspaceConfigPath);
            EnsureWatchers(runtimeConfig);

            var fingerprint = ComputeFingerprint(runtimeConfig);
            if (_snapshot is not null && string.Equals(_snapshot.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                _runtimeConfig = runtimeConfig;
                snapshot = _snapshot;
                return false;
            }

            var rebuilt = BuildSnapshot(runtimeConfig, fingerprint);
            var changed = _snapshot is null || !string.Equals(_snapshot.Fingerprint, rebuilt.Fingerprint, StringComparison.Ordinal);
            _snapshot = rebuilt;
            _runtimeConfig = runtimeConfig;
            snapshot = rebuilt;
            return changed;
        }
    }

    /// <summary>
    /// Creates a run-scoped environment injection context for eligible skills.
    /// </summary>
    public IDisposable BeginRunScope()
    {
        var snapshot = GetSnapshot();
        var applied = new List<AppliedEnvironmentValue>();

        foreach (var skill in snapshot.ActiveSkills)
        {
            var entry = _runtimeConfig.GetEntry(skill.SkillKey);

            foreach (var (name, value) in entry.Env)
            {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                    continue;

                if (!string.IsNullOrEmpty(_getEnvironmentVariable(name)))
                    continue;

                if (applied.Any(a => string.Equals(a.Name, name, StringComparison.Ordinal)))
                    continue;

                var original = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
                applied.Add(new AppliedEnvironmentValue(name, original));
            }

            var primaryEnv = skill.Metadata.PrimaryEnv;
            if (!string.IsNullOrWhiteSpace(primaryEnv) &&
                !string.IsNullOrWhiteSpace(entry.ApiKey) &&
                string.IsNullOrEmpty(_getEnvironmentVariable(primaryEnv)) &&
                applied.All(a => !string.Equals(a.Name, primaryEnv, StringComparison.Ordinal)))
            {
                var original = Environment.GetEnvironmentVariable(primaryEnv);
                Environment.SetEnvironmentVariable(primaryEnv, entry.ApiKey);
                applied.Add(new AppliedEnvironmentValue(primaryEnv, original));
            }
        }

        return applied.Count == 0 ? NoopScope.Instance : new EnvironmentScope(applied);
    }

    /// <summary>
    /// Renders a deterministic, explainable skills status report.
    /// </summary>
    public string FormatStatusReport()
    {
        var snapshot = GetSnapshot();
        var sb = new StringBuilder();

        var active = snapshot.Statuses.Count(s => s.State == SkillEligibilityState.Active);
        sb.AppendLine($"Skills: {active} active / {snapshot.Statuses.Count} discovered");

        foreach (var status in snapshot.Statuses
                     .OrderBy(s => s.Name, KeyComparer)
                     .ThenByDescending(s => GetTier(s.Source.Kind))
                     .ThenByDescending(s => s.Source.Order))
        {
            var label = status.State switch
            {
                SkillEligibilityState.Active => "active",
                SkillEligibilityState.Excluded => "excluded",
                SkillEligibilityState.Shadowed => "shadowed",
                SkillEligibilityState.Invalid => "invalid",
                _ => "unknown",
            };

            sb.Append($"- [{label}] {status.Name} ({status.Source.Kind}:{status.Source.Name})");
            if (!string.Equals(status.ReasonCode, SkillReasonCodes.None, StringComparison.Ordinal))
            {
                sb.Append($" reason={status.ReasonCode}");
                if (!string.IsNullOrWhiteSpace(status.ReasonDetail))
                    sb.Append($" ({status.ReasonDetail})");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();
            _watchersEnabled = false;
        }
    }
}
