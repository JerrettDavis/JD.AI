using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Workflows.Store;

/// <summary>
/// File-based workflow store that persists shared workflows as JSON files.
/// Uses a hierarchical directory structure: <c>{baseDir}/{name}/{version}.json</c>.
/// Serves as a local-only fallback when no Git repository is configured.
/// </summary>
public sealed class FileWorkflowStore : IWorkflowStore
{
    private readonly string _baseDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public FileWorkflowStore(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        Directory.CreateDirectory(baseDirectory);
    }

    /// <inheritdoc/>
    public async Task PublishAsync(SharedWorkflow workflow, CancellationToken ct = default)
    {
        var dir = GetWorkflowDirectory(workflow.Name);
        Directory.CreateDirectory(dir);

        var path = GetVersionPath(workflow.Name, workflow.Version);
        var json = JsonSerializer.Serialize(workflow, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SharedWorkflow?> GetAsync(
        string nameOrId, string? version = null, CancellationToken ct = default)
    {
        // First try by name/version
        if (version is not null)
        {
            var path = GetVersionPath(nameOrId, version);
            if (File.Exists(path))
                return await ReadAsync(path, ct).ConfigureAwait(false);
        }
        else
        {
            // Try as a name — return latest version
            var dir = GetWorkflowDirectory(nameOrId);
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.json");
                if (files.Length > 0)
                {
                    var latest = GetLatestVersionFile(files);
                    return await ReadAsync(latest, ct).ConfigureAwait(false);
                }
            }
        }

        // Fall back to searching by ID across all workflows
        var allWorkflows = await CatalogAsync(ct: ct).ConfigureAwait(false);
        var byId = allWorkflows.FirstOrDefault(w =>
            string.Equals(w.Id, nameOrId, StringComparison.OrdinalIgnoreCase));

        if (byId is not null && version is not null)
            return string.Equals(byId.Version, version, StringComparison.Ordinal) ? byId : null;

        return byId;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedWorkflow>> CatalogAsync(
        string? tag = null, string? author = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(_baseDirectory))
            return [];

        var results = new List<SharedWorkflow>();

        // Each subdirectory is a workflow name
        foreach (var dir in Directory.GetDirectories(_baseDirectory))
        {
            // Get the latest version only for the catalog
            var files = Directory.GetFiles(dir, "*.json");
            if (files.Length == 0) continue;

            var latest = GetLatestVersionFile(files);
            var workflow = await ReadAsync(latest, ct).ConfigureAwait(false);
            if (workflow is null) continue;

            if (tag is not null &&
                !workflow.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (author is not null &&
                !string.Equals(workflow.Author, author, StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(workflow);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedWorkflow>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        if (!Directory.Exists(_baseDirectory))
            return [];

        var allFiles = Directory.GetFiles(_baseDirectory, "*.json", SearchOption.AllDirectories);
        var results = new List<SharedWorkflow>();

        foreach (var file in allFiles)
        {
            var workflow = await ReadAsync(file, ct).ConfigureAwait(false);
            if (workflow is null) continue;

            var matches =
                workflow.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                workflow.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                workflow.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                workflow.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (matches)
                results.Add(workflow);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedWorkflow>> VersionsAsync(
        string name, CancellationToken ct = default)
    {
        var dir = GetWorkflowDirectory(name);
        if (!Directory.Exists(dir))
            return [];

        var files = Directory.GetFiles(dir, "*.json");
        var results = new List<SharedWorkflow>(files.Length);

        foreach (var file in files.OrderBy(ParseVersionFromPath))
        {
            var workflow = await ReadAsync(file, ct).ConfigureAwait(false);
            if (workflow is not null)
                results.Add(workflow);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<bool> InstallAsync(
        string nameOrId, string? version, string localDirectory, CancellationToken ct = default)
    {
        var workflow = await GetAsync(nameOrId, version, ct).ConfigureAwait(false);
        if (workflow is null)
            return false;

        Directory.CreateDirectory(localDirectory);

        var fileName = $"{Sanitize(workflow.Name)}-{Sanitize(workflow.Version)}.json";
        var destPath = Path.Combine(localDirectory, fileName);

        // The local CLI catalog expects AgentWorkflowDefinition JSON, stored in
        // SharedWorkflow.DefinitionJson. Write that directly if available.
        var json = !string.IsNullOrWhiteSpace(workflow.DefinitionJson)
            ? workflow.DefinitionJson!
            : JsonSerializer.Serialize(workflow, JsonOptions);
        await File.WriteAllTextAsync(destPath, json, ct).ConfigureAwait(false);

        return true;
    }

    private string GetWorkflowDirectory(string name) =>
        Path.Combine(_baseDirectory, Sanitize(name));

    private string GetVersionPath(string name, string version) =>
        Path.Combine(GetWorkflowDirectory(name), $"{Sanitize(version)}.json");

    private static string Sanitize(string input) =>
        string.Concat(input.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_'));

    /// <summary>
    /// Returns the file path with the highest semantic version from an array of version files.
    /// Falls back to lexicographic ordering if version parsing fails.
    /// </summary>
    private static string GetLatestVersionFile(string[] files) =>
        files.OrderByDescending(ParseVersionFromPath).First();

    private static Version ParseVersionFromPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return Version.TryParse(name, out var v) ? v : new Version(0, 0, 0);
    }

    private static async Task<SharedWorkflow?> ReadAsync(string path, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SharedWorkflow>(json, JsonOptions);
        }
#pragma warning disable CA1031
        catch
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
