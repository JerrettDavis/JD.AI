using System.Text.Json;
using JD.AI.Core.Config;

namespace JD.AI.Core.Plugins;

/// <summary>
/// Persists installed plugin metadata to a JSON registry.
/// </summary>
public sealed class PluginRegistryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _registryPath;
    private readonly Lock _lock = new();

    public PluginRegistryStore(string? registryPath = null)
    {
        _registryPath = registryPath ?? Path.Combine(DataDirectories.Root, "plugins", "registry.json");
    }

    public string RegistryPath => _registryPath;

    public async Task<IReadOnlyList<InstalledPluginRecord>> ListAsync(CancellationToken ct = default)
    {
        var doc = await ReadAsync(ct).ConfigureAwait(false);
        return doc.Plugins;
    }

    public async Task<InstalledPluginRecord?> FindAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var doc = await ReadAsync(ct).ConfigureAwait(false);
        return doc.Plugins.FirstOrDefault(p =>
            string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertAsync(InstalledPluginRecord entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var doc = await ReadAsync(ct).ConfigureAwait(false);
        var index = doc.Plugins.FindIndex(p =>
            string.Equals(p.Id, entry.Id, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            doc.Plugins[index] = entry;
        }
        else
        {
            doc.Plugins.Add(entry);
        }

        await WriteAsync(doc, ct).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var doc = await ReadAsync(ct).ConfigureAwait(false);
        var removed = doc.Plugins.RemoveAll(p =>
            string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            await WriteAsync(doc, ct).ConfigureAwait(false);
        }

        return removed;
    }

    private async Task<PluginRegistryDocument> ReadAsync(CancellationToken ct)
    {
        string path;
        lock (_lock)
        {
            path = _registryPath;
        }

        if (!File.Exists(path))
        {
            return new PluginRegistryDocument([]);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var doc = await JsonSerializer
                .DeserializeAsync<PluginRegistryDocument>(stream, JsonOptions, ct)
                .ConfigureAwait(false);
            return doc ?? new PluginRegistryDocument([]);
        }
        catch (JsonException)
        {
            return new PluginRegistryDocument([]);
        }
    }

    private async Task WriteAsync(PluginRegistryDocument doc, CancellationToken ct)
    {
        string path;
        lock (_lock)
        {
            path = _registryPath;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, doc, JsonOptions, ct).ConfigureAwait(false);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }
}
