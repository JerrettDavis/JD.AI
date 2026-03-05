using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Workflows;

/// <summary>
/// File-based workflow catalog that persists definitions as JSON files.
/// Uses a flat directory structure: <c>{baseDir}/{name}-{version}.json</c>.
/// </summary>
public sealed class FileWorkflowCatalog : IWorkflowCatalog
{
    private readonly string _baseDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public FileWorkflowCatalog(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        Directory.CreateDirectory(baseDirectory);
    }

    public async Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken ct = default)
    {
        definition.UpdatedAt = DateTime.UtcNow;
        var path = GetPath(definition.Name, definition.Version);
        var json = JsonSerializer.Serialize(definition, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    public async Task<AgentWorkflowDefinition?> GetAsync(
        string name, string? version = null, CancellationToken ct = default)
    {
        if (version is not null)
        {
            var path = GetPath(name, version);
            return File.Exists(path)
                ? await ReadAsync(path, ct).ConfigureAwait(false)
                : null;
        }

        // Return latest non-deprecated version (semantic ordering)
        var files = Directory.GetFiles(_baseDirectory, $"{Sanitize(name)}-*.json");
        if (files.Length == 0) return null;

        var latest = files.OrderByDescending(ParseVersionFromPath).First();
        return await ReadAsync(latest, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_baseDirectory))
            return [];

        var files = Directory.GetFiles(_baseDirectory, "*.json");
        var results = new List<AgentWorkflowDefinition>(files.Length);

        foreach (var file in files)
        {
            var def = await ReadAsync(file, ct).ConfigureAwait(false);
            if (def is not null)
                results.Add(def);
        }

        return results;
    }

    public Task<bool> DeleteAsync(string name, string? version = null, CancellationToken ct = default)
    {
        var path = GetPath(name, version ?? "1.0");
        if (!File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        return Task.FromResult(true);
    }

    private string GetPath(string name, string version) =>
        Path.Combine(_baseDirectory, $"{Sanitize(name)}-{Sanitize(version)}.json");

    private static string Sanitize(string input) =>
        string.Concat(input.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_'));

    private static WorkflowVersion ParseVersionFromPath(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        // File format: {name}-{version}.json — extract version after last dash-delimited semver
        var dashIdx = fileName.LastIndexOf('-');
        if (dashIdx >= 0)
        {
            var versionPart = fileName[(dashIdx + 1)..];
            if (WorkflowVersion.TryParse(versionPart, out var v))
                return v;
        }

        return new WorkflowVersion(0, 0, 0);
    }

    private static async Task<AgentWorkflowDefinition?> ReadAsync(string path, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<AgentWorkflowDefinition>(json, JsonOptions);
    }
}
