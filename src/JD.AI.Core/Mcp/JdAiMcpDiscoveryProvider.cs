using System.Text.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Config;
using JD.SemanticKernel.Extensions.Mcp;

namespace JD.AI.Core.Mcp;

/// <summary>
/// Reads and writes MCP server definitions managed by JD.AI itself,
/// persisted in <c>~/.jdai/jdai.mcp.json</c> (or the resolved data-directory equivalent).
/// </summary>
public sealed class JdAiMcpDiscoveryProvider : IMcpDiscoveryProvider
{
    private readonly string _configPath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public JdAiMcpDiscoveryProvider()
        : this(Path.Combine(DataDirectories.Root, "jdai.mcp.json")) { }

    internal JdAiMcpDiscoveryProvider(string configPath)
    {
        _configPath = configPath;
    }

    public string ProviderId => "jd-ai";

    /// <inheritdoc />
    public Task<IReadOnlyList<McpServerDefinition>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configPath))
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);

        try
        {
            var json = File.ReadAllText(_configPath);
            var file = JsonSerializer.Deserialize<McpFile>(json, JsonOpts);
            if (file?.McpServers is null)
                return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);

            var results = file.McpServers
                .Select(kvp => EntryToDefinition(kvp.Key, kvp.Value))
                .ToList();

            return Task.FromResult<IReadOnlyList<McpServerDefinition>>(results);
        }
#pragma warning disable CA1031 // non-fatal
        catch
        {
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Persists an <see cref="McpServerDefinition"/> to the JD.AI-managed config file.
    /// Existing entries with the same name are replaced.
    /// </summary>
    public async Task AddOrUpdateAsync(McpServerDefinition server, CancellationToken ct = default)
    {
        var file = await LoadFileAsync(ct).ConfigureAwait(false);
        file.McpServers[server.Name] = DefinitionToEntry(server);
        await SaveFileAsync(file, ct).ConfigureAwait(false);
    }

    /// <summary>Removes a server by name. No-op if not found.</summary>
    public async Task RemoveAsync(string name, CancellationToken ct = default)
    {
        var file = await LoadFileAsync(ct).ConfigureAwait(false);
        var key = file.McpServers.Keys
            .FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        if (key is null)
            return;

        file.McpServers.Remove(key);
        await SaveFileAsync(file, ct).ConfigureAwait(false);
    }

    /// <summary>Enables or disables a server by name. No-op if not found or already in the desired state.</summary>
    public async Task SetEnabledAsync(string name, bool enabled, CancellationToken ct = default)
    {
        var file = await LoadFileAsync(ct).ConfigureAwait(false);
        var key = file.McpServers.Keys
            .FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        if (key is null)
            return;

        var entry = file.McpServers[key];
        var desiredDisabled = !enabled;

        if (entry.Disabled == desiredDisabled || (!entry.Disabled.HasValue && !desiredDisabled))
            return;

        entry.Disabled = desiredDisabled ? true : null;
        await SaveFileAsync(file, ct).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<McpFile> LoadFileAsync(CancellationToken ct)
    {
        if (!File.Exists(_configPath))
            return new McpFile();

        try
        {
            var json = await File.ReadAllTextAsync(_configPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<McpFile>(json, JsonOpts) ?? new McpFile();
        }
#pragma warning disable CA1031
        catch
        {
            return new McpFile();
        }
#pragma warning restore CA1031
    }

    private async Task SaveFileAsync(McpFile file, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Write to a uniquely-named temp file first, then atomic-rename.
        // Using a GUID suffix prevents concurrent jdai processes from colliding
        // on the same temp file and losing updates.
        var tmp = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var json = JsonSerializer.Serialize(file, JsonOpts);
        await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
        File.Move(tmp, _configPath, overwrite: true);
    }

    private McpServerDefinition EntryToDefinition(string name, McpServerEntry entry)
    {
        Uri? url = entry.Url is not null && Uri.TryCreate(entry.Url, UriKind.Absolute, out var u) ? u : null;
        var transport = url is not null ? McpTransportType.Http : McpTransportType.Stdio;
        IReadOnlyList<string>? args = entry.Args is { Count: > 0 } ? entry.Args : null;
        IReadOnlyDictionary<string, string>? env = entry.Env is { Count: > 0 } ? entry.Env : null;

        return new McpServerDefinition(
            name: name,
            displayName: name,
            transport: transport,
            scope: McpScope.User,
            sourceProvider: "jd-ai",
            sourcePath: _configPath,
            url: url,
            command: entry.Command,
            args: args,
            env: env,
            isEnabled: entry.Disabled != true);
    }

    private static McpServerEntry DefinitionToEntry(McpServerDefinition server)
    {
        return new McpServerEntry
        {
            Command = server.Command,
            Args = server.Args is { Count: > 0 } ? [.. server.Args] : null,
            Env = server.Env is { Count: > 0 } ? new Dictionary<string, string>(server.Env) : null,
            Url = server.Url?.ToString(),
            Disabled = server.IsEnabled ? null : true,
        };
    }

    // ── Internal JSON schema ──────────────────────────────────────────────────

    private sealed class McpFile
    {
        [JsonPropertyName("mcpServers")]
        public Dictionary<string, McpServerEntry> McpServers { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class McpServerEntry
    {
        [JsonPropertyName("command")]
        public string? Command { get; set; }

        [JsonPropertyName("args")]
        public List<string>? Args { get; set; }

        [JsonPropertyName("env")]
        public Dictionary<string, string>? Env { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("disabled")]
        public bool? Disabled { get; set; }
    }
}
