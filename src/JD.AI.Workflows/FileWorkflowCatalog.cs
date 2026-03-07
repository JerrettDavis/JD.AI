using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Workflows;

/// <summary>
/// File-based workflow catalog that persists definitions as JSON files.
/// Uses a flat directory structure: <c>{baseDir}/{name}-{version}.json</c>.
/// </summary>
public sealed class FileWorkflowCatalog : IWorkflowCatalog
{
    private readonly string _baseDirectory;

    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    public FileWorkflowCatalog(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        Directory.CreateDirectory(baseDirectory);
    }

    public async Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken ct = default)
    {
        var nextVersion = WorkflowVersioning.ParseVersionOrThrow(definition.Version);

        var priorVersions = await LoadByNameAsync(definition.Name, ct).ConfigureAwait(false);
        var previous = WorkflowVersioning.SelectVersion(priorVersions, "latest");
        if (previous is not null &&
            !string.Equals(previous.Version, definition.Version, StringComparison.Ordinal))
        {
            var previousVersion = WorkflowVersioning.ParseVersionOrThrow(previous.Version);
            var breaking = WorkflowVersioning.DetectBreakingChanges(previous, definition);

            definition.BreakingChanges.Clear();
            foreach (var change in breaking)
                definition.BreakingChanges.Add(change);

            if (breaking.Count > 0 && nextVersion.Major <= previousVersion.Major)
            {
                throw new InvalidDataException(
                    $"Breaking changes detected between versions {previous.Version} and {definition.Version}. " +
                    "Increment major version for breaking workflow changes.");
            }
        }
        else
        {
            definition.BreakingChanges.Clear();
        }

        definition.UpdatedAt = DateTime.UtcNow;
        var path = GetPath(definition.Name, definition.Version);
        var json = JsonSerializer.Serialize(definition, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    public async Task<AgentWorkflowDefinition?> GetAsync(
        string name, string? version = null, CancellationToken ct = default)
    {
        var versions = await LoadByNameAsync(name, ct).ConfigureAwait(false);
        return WorkflowVersioning.SelectVersion(versions, version);
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
        if (version is null)
        {
            var files = Directory.GetFiles(_baseDirectory, $"{Sanitize(name)}-*.json");
            if (files.Length == 0)
                return Task.FromResult(false);

            foreach (var file in files)
                File.Delete(file);
            return Task.FromResult(true);
        }

        var path = GetPath(name, version);
        if (!File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        return Task.FromResult(true);
    }

    private string GetPath(string name, string version) =>
        Path.Combine(_baseDirectory, $"{Sanitize(name)}-{Sanitize(version)}.json");

    private static string Sanitize(string input) =>
        string.Concat(input.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' or '+' ? c : '_'));

    private static async Task<AgentWorkflowDefinition?> ReadAsync(string path, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<AgentWorkflowDefinition>(json, JsonOptions);
    }

    private async Task<IReadOnlyList<AgentWorkflowDefinition>> LoadByNameAsync(
        string name,
        CancellationToken ct)
    {
        var files = Directory.GetFiles(_baseDirectory, $"{Sanitize(name)}-*.json");
        if (files.Length == 0)
            return [];

        var results = new List<AgentWorkflowDefinition>(files.Length);
        foreach (var file in files)
        {
            var def = await ReadAsync(file, ct).ConfigureAwait(false);
            if (def is not null &&
                string.Equals(def.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(def);
            }
        }

        return results;
    }
}
