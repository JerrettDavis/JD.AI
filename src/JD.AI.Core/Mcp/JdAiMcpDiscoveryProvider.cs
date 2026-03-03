using System.Text.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Config;

namespace JD.AI.Core.Mcp;

/// <summary>
/// Reads and writes MCP server definitions managed by JD.AI itself,
/// persisted in <c>~/.jdai/mcp.json</c> (or the resolved data-directory equivalent).
/// </summary>
public sealed class JdAiMcpDiscoveryProvider : IMcpDiscoveryProvider
{
    private readonly string _configPath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public JdAiMcpDiscoveryProvider()
        : this(Path.Combine(DataDirectories.Root, "mcp.json")) { }

    internal JdAiMcpDiscoveryProvider(string configPath)
    {
        _configPath = configPath;
    }

    public string SourceLabel => $"JD.AI user config ({_configPath})";

    /// <inheritdoc />
    public Task<IReadOnlyList<McpServerDefinition>> DiscoverAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_configPath))
            return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);

        try
        {
            var json = File.ReadAllText(_configPath);
            var file = JsonSerializer.Deserialize<JdAiMcpFile>(json, JsonOpts);
            if (file?.Servers is null)
                return Task.FromResult<IReadOnlyList<McpServerDefinition>>([]);

            var results = file.Servers
                .Select(s => s with
                {
                    Scope = McpScope.User,
                    SourceProvider = "JD.AI",
                    SourcePath = _configPath,
                })
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

        var existing = file.Servers.ToList();
        var idx = existing.FindIndex(s =>
            string.Equals(s.Name, server.Name, StringComparison.OrdinalIgnoreCase));

        var toStore = server with
        {
            Scope = McpScope.User,
            SourceProvider = "JD.AI",
            SourcePath = _configPath,
        };

        if (idx >= 0)
            existing[idx] = toStore;
        else
            existing.Add(toStore);

        file = file with { Servers = existing };
        await SaveFileAsync(file, ct).ConfigureAwait(false);
    }

    /// <summary>Removes a server by name. No-op if not found.</summary>
    public async Task RemoveAsync(string name, CancellationToken ct = default)
    {
        var file = await LoadFileAsync(ct).ConfigureAwait(false);
        var updated = file.Servers
            .Where(s => !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (updated.Count == file.Servers.Count)
            return; // not found — nothing to do

        await SaveFileAsync(file with { Servers = updated }, ct).ConfigureAwait(false);
    }

    /// <summary>Enables or disables a server by name. No-op if not found.</summary>
    public async Task SetEnabledAsync(string name, bool enabled, CancellationToken ct = default)
    {
        var file = await LoadFileAsync(ct).ConfigureAwait(false);
        var updated = file.Servers
            .Select(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)
                ? s with { IsEnabled = enabled }
                : s)
            .ToList();

        await SaveFileAsync(file with { Servers = updated }, ct).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<JdAiMcpFile> LoadFileAsync(CancellationToken ct)
    {
        if (!File.Exists(_configPath))
            return new JdAiMcpFile();

        try
        {
            var json = await File.ReadAllTextAsync(_configPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<JdAiMcpFile>(json, JsonOpts) ?? new JdAiMcpFile();
        }
#pragma warning disable CA1031
        catch
        {
            return new JdAiMcpFile();
        }
#pragma warning restore CA1031
    }

    private async Task SaveFileAsync(JdAiMcpFile file, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

        // Write to a uniquely-named temp file first, then atomic-rename.
        // Using a GUID suffix prevents concurrent jdai processes from colliding
        // on the same temp file and losing updates.
        var tmp = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var json = JsonSerializer.Serialize(file, JsonOpts);
        await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
        File.Move(tmp, _configPath, overwrite: true);
    }

    // ── Internal JSON schema ──────────────────────────────────────────────────

    private sealed record JdAiMcpFile
    {
        [JsonPropertyName("servers")]
        public List<McpServerDefinition> Servers { get; init; } = [];
    }
}
