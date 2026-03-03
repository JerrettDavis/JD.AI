namespace JD.AI.Core.Mcp;

/// <summary>
/// Aggregates MCP server definitions from all registered discovery providers,
/// applies scope-based merge precedence (Project &gt; User &gt; BuiltIn), and
/// tracks per-server runtime status.
/// </summary>
public sealed class McpManager
{
    private readonly IReadOnlyList<IMcpDiscoveryProvider> _providers;
    private readonly JdAiMcpDiscoveryProvider? _jdAiProvider;
    private readonly Dictionary<string, McpServerStatus> _statusCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _statusLock = new();

    /// <summary>
    /// Creates an <see cref="McpManager"/> using the default provider set:
    /// Claude Code user + project configs and the JD.AI-managed config.
    /// </summary>
    public McpManager()
        : this(CreateDefaultProviders(out var jdAiProvider), jdAiProvider)
    {
    }

    private static IReadOnlyList<IMcpDiscoveryProvider> CreateDefaultProviders(
        out JdAiMcpDiscoveryProvider jdAiProvider)
    {
        jdAiProvider = new JdAiMcpDiscoveryProvider();
        return
        [
            new ClaudeCodeUserMcpDiscoveryProvider(),
            jdAiProvider,
            new ClaudeCodeProjectMcpDiscoveryProvider(),
        ];
    }

    /// <summary>Creates an <see cref="McpManager"/> with a custom provider list.</summary>
    public McpManager(
        IReadOnlyList<IMcpDiscoveryProvider> providers,
        JdAiMcpDiscoveryProvider? jdAiProvider = null)
    {
        _providers = providers;
        _jdAiProvider = jdAiProvider;
    }

    /// <summary>
    /// Discovers and merges servers from all providers.
    /// Precedence: Project &gt; User &gt; BuiltIn.
    /// Within the same scope, later providers (higher index) override earlier ones.
    /// </summary>
    public async Task<IReadOnlyList<McpServerDefinition>> GetAllServersAsync(
        CancellationToken ct = default)
    {
        var all = new List<McpServerDefinition>();

        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();
            var discovered = await provider.DiscoverAsync(ct).ConfigureAwait(false);
            all.AddRange(discovered);
        }

        return Merge(all);
    }

    /// <summary>
    /// Returns the cached status for a server, or <see cref="McpServerStatus.Default"/>
    /// if no check has been performed yet.
    /// </summary>
    public McpServerStatus GetStatus(string serverName)
    {
        lock (_statusLock)
        {
            return _statusCache.TryGetValue(serverName, out var status)
                ? status
                : McpServerStatus.Default;
        }
    }

    /// <summary>Updates the cached status for a server.</summary>
    public void SetStatus(string serverName, McpServerStatus status)
    {
        lock (_statusLock)
            _statusCache[serverName] = status;
    }

    /// <summary>
    /// Adds or updates a server in the JD.AI-managed config.
    /// Throws <see cref="InvalidOperationException"/> if no writable provider is available.
    /// </summary>
    public async Task AddOrUpdateAsync(McpServerDefinition server, CancellationToken ct = default)
    {
        EnsureJdAiProvider();
        await _jdAiProvider!.AddOrUpdateAsync(server, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a server from the JD.AI-managed config.
    /// Throws <see cref="InvalidOperationException"/> if no writable provider is available.
    /// </summary>
    public async Task RemoveAsync(string name, CancellationToken ct = default)
    {
        EnsureJdAiProvider();
        await _jdAiProvider!.RemoveAsync(name, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Enables or disables a server in the JD.AI-managed config.
    /// Throws <see cref="InvalidOperationException"/> if no writable provider is available.
    /// </summary>
    public async Task SetEnabledAsync(string name, bool enabled, CancellationToken ct = default)
    {
        EnsureJdAiProvider();
        await _jdAiProvider!.SetEnabledAsync(name, enabled, ct).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Merges definitions applying scope precedence: Project &gt; User &gt; BuiltIn.
    /// Within the same scope the last definition seen wins (later providers override earlier ones).
    /// </summary>
    internal static IReadOnlyList<McpServerDefinition> Merge(
        IEnumerable<McpServerDefinition> all)
    {
        var byName = new Dictionary<string, McpServerDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in all)
        {
            if (!byName.TryGetValue(def.Name, out var existing) ||
                ScopePriority(def.Scope) >= ScopePriority(existing.Scope))
            {
                byName[def.Name] = def;
            }
        }

        return [.. byName.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static int ScopePriority(McpScope scope) => scope switch
    {
        McpScope.BuiltIn => 0,
        McpScope.User    => 1,
        McpScope.Project => 2,
        _                => -1,
    };

    private void EnsureJdAiProvider()
    {
        if (_jdAiProvider is null)
            throw new InvalidOperationException(
                "No writable MCP provider is configured. " +
                "Use McpManager() constructor or supply a JdAiMcpDiscoveryProvider.");
    }
}
