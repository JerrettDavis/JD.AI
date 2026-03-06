using JD.SemanticKernel.Extensions.Mcp;
using JD.SemanticKernel.Extensions.Mcp.Discovery;
using JD.SemanticKernel.Extensions.Mcp.Registry;

namespace JD.AI.Core.Mcp;

/// <summary>
/// Aggregates MCP server definitions from all registered discovery providers,
/// applies scope-based merge precedence (Project &gt; User &gt; BuiltIn), and
/// tracks per-server runtime status.
/// </summary>
public sealed class McpManager
{
    private readonly IMcpRegistry _registry;
    private readonly JdAiMcpDiscoveryProvider? _jdAiProvider;
    private readonly Dictionary<string, McpServerStatus> _statusCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _statusLock = new();

    /// <summary>
    /// Creates an <see cref="McpManager"/> using the default provider set:
    /// Claude Code, Claude Desktop, VS Code, Codex, Copilot, and the JD.AI-managed config.
    /// </summary>
    public McpManager()
        : this(CreateDefaultRegistry(out var jdAiProvider), jdAiProvider)
    {
    }

    /// <summary>Creates an <see cref="McpManager"/> with a custom registry and optional write provider.</summary>
    public McpManager(IMcpRegistry registry, JdAiMcpDiscoveryProvider? jdAiProvider = null)
    {
        _registry = registry;
        _jdAiProvider = jdAiProvider;
    }

    private static McpRegistry CreateDefaultRegistry(out JdAiMcpDiscoveryProvider jdAiProvider)
    {
        jdAiProvider = new JdAiMcpDiscoveryProvider();
        var cwd = Directory.GetCurrentDirectory();
        IReadOnlyList<IMcpDiscoveryProvider> providers =
        [
            new ClaudeCodeMcpDiscoveryProvider(cwd),
            new ClaudeDesktopMcpDiscoveryProvider(),
            new VsCodeMcpDiscoveryProvider(cwd),
            new CodexMcpDiscoveryProvider(cwd),
            new CopilotMcpDiscoveryProvider(),
            jdAiProvider,
        ];
        return new McpRegistry(providers);
    }

    private static McpRegistry CreateExternalRegistry()
    {
        var cwd = Directory.GetCurrentDirectory();
        IReadOnlyList<IMcpDiscoveryProvider> providers =
        [
            new ClaudeCodeMcpDiscoveryProvider(cwd),
            new ClaudeDesktopMcpDiscoveryProvider(),
            new VsCodeMcpDiscoveryProvider(cwd),
            new CodexMcpDiscoveryProvider(cwd),
            new CopilotMcpDiscoveryProvider(),
        ];
        return new McpRegistry(providers);
    }

    /// <summary>Discovers and merges servers from all providers via the registry.</summary>
    public Task<IReadOnlyList<McpServerDefinition>> GetAllServersAsync(CancellationToken ct = default)
        => _registry.GetAllAsync(ct);

    /// <summary>
    /// Returns servers discovered from external tool configs (Claude Code, Claude Desktop,
    /// VS Code, Codex, Copilot) that are not already present in the JD.AI-managed config.
    /// </summary>
    public Task<IReadOnlyList<McpServerDefinition>> GetImportCandidatesAsync(
        CancellationToken ct = default)
        => GetImportCandidatesAsync(CreateExternalRegistry(), ct);

    /// <summary>
    /// Returns import candidates using the provided external registry (for testing).
    /// </summary>
    internal async Task<IReadOnlyList<McpServerDefinition>> GetImportCandidatesAsync(
        IMcpRegistry externalRegistry,
        CancellationToken ct = default)
    {
        var external = await externalRegistry.GetAllAsync(ct).ConfigureAwait(false);

        IReadOnlyList<McpServerDefinition> jdAiServers =
            _jdAiProvider is not null
                ? await _jdAiProvider.DiscoverAsync(ct).ConfigureAwait(false)
                : [];

        var jdAiNames = jdAiServers
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return external
            .Where(s => !jdAiNames.Contains(s.Name))
            .ToList();
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

    private void EnsureJdAiProvider()
    {
        if (_jdAiProvider is null)
            throw new InvalidOperationException(
                "No writable MCP provider is configured. " +
                "Use McpManager() constructor or supply a JdAiMcpDiscoveryProvider.");
    }
}
