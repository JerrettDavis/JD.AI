using JD.AI.Core.Agents;

namespace JD.AI.Rendering;

/// <summary>
/// Collects and manages the mutable state used to build an immutable <see cref="FooterState"/> snapshot.
/// All mutations are thread-safe; the class is safe to call from both the main thread and background
/// refresh tasks.
/// </summary>
public sealed class FooterStateProvider
{
    private readonly Lock _lock = new();
    private readonly string _workingDirectory;

    // Session-level state
    private string _provider = string.Empty;
    private string _model = string.Empty;
    private long _tokensUsed;
    private long _contextWindow;
    private int _turnCount;
    private PermissionMode _mode = PermissionMode.Normal;
    private double _warnThresholdPercent;

    // Git state
    private string? _gitBranch;
    private string? _prLink;

    // Plugin segments
    private readonly List<PluginSegment> _pluginSegments = [];

    /// <summary>
    /// Initialises a new <see cref="FooterStateProvider"/> bound to the given working directory.
    /// </summary>
    /// <param name="workingDirectory">The current working directory to display in the footer.</param>
    public FooterStateProvider(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    /// <summary>
    /// Updates all session-level footer data in a single, atomic operation.
    /// </summary>
    public void Update(
        string provider,
        string model,
        long tokensUsed,
        long contextWindow,
        int turnCount,
        PermissionMode mode,
        double warnThresholdPercent)
    {
        lock (_lock)
        {
            _provider = provider;
            _model = model;
            _tokensUsed = tokensUsed;
            _contextWindow = contextWindow;
            _turnCount = turnCount;
            _mode = mode;
            _warnThresholdPercent = warnThresholdPercent;
        }
    }

    /// <summary>
    /// Updates the git branch and optional PR link. Pass <see langword="null"/> to clear either value.
    /// </summary>
    public void SetGitInfo(string? branch, string? prLink)
    {
        lock (_lock)
        {
            _gitBranch = branch;
            _prLink = prLink;
        }
    }

    /// <summary>
    /// Adds or replaces a plugin-provided segment identified by <paramref name="key"/>.
    /// If a segment with the same key already exists it is removed before the new one is added
    /// (upsert semantics).
    /// </summary>
    public void AddPluginSegment(string key, string value, int priority = 0)
    {
        lock (_lock)
        {
            _pluginSegments.RemoveAll(s => string.Equals(s.Key, key, StringComparison.Ordinal));
            _pluginSegments.Add(new PluginSegment(key, value, priority));
        }
    }

    /// <summary>
    /// Builds and returns an immutable snapshot of the current footer state.
    /// </summary>
    public FooterState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return new FooterState(
                    WorkingDirectory: _workingDirectory,
                    GitBranch: _gitBranch,
                    PrLink: _prLink,
                    ContextTokensUsed: _tokensUsed,
                    ContextWindowSize: _contextWindow,
                    Provider: _provider,
                    Model: _model,
                    TurnCount: _turnCount,
                    Mode: _mode,
                    WarnThresholdPercent: _warnThresholdPercent,
                    PluginSegments: _pluginSegments.ToList());
            }
        }
    }
}
