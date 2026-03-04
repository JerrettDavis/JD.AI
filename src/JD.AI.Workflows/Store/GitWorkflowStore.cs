using System.Text.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Config;

namespace JD.AI.Workflows.Store;

/// <summary>
/// Git-backed workflow store. Workflows are stored as <c>{name}/{version}.json</c>
/// files inside a cloned Git repository. Operations pull before reading and push after
/// writing so the store stays in sync across machines.
/// </summary>
/// <remarks>
/// Uses the <c>git</c> CLI via <see cref="GitHelper"/> rather than LibGit2Sharp
/// to avoid heavy native library dependencies.
/// </remarks>
public sealed class GitWorkflowStore : IWorkflowStore
{
    private readonly string _repoUrl;
    private readonly string _localCachePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <param name="repoUrl">Remote Git repository URL (HTTPS or SSH).</param>
    /// <param name="localCachePath">
    ///   Local path where the repo is cloned.
    ///   Defaults to a subdirectory of the data root (honors <c>JDAI_DATA_DIR</c>).
    /// </param>
    public GitWorkflowStore(string repoUrl, string? localCachePath = null)
    {
        _repoUrl = repoUrl;
        _localCachePath = localCachePath
            ?? Path.Combine(DataDirectories.Root, "workflow-store");
    }

    /// <inheritdoc/>
    public async Task PublishAsync(SharedWorkflow workflow, CancellationToken ct = default)
    {
        await EnsureRepoAsync(ct).ConfigureAwait(false);
        await PullAsync(ct).ConfigureAwait(false);

        var dir = GetWorkflowDirectory(workflow.Name);
        Directory.CreateDirectory(dir);

        var path = GetVersionPath(workflow.Name, workflow.Version);
        var json = JsonSerializer.Serialize(workflow, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);

        var (addExit, _, addErr) = await GitHelper.RunAsync(_localCachePath, $"add \"{path}\"", ct).ConfigureAwait(false);
        if (addExit != 0)
            throw new InvalidOperationException($"Git add failed (exit {addExit}): {addErr}");

        var (commitExit, _, commitErr) = await GitHelper.RunAsync(
            _localCachePath,
            $"commit -m \"publish: {Sanitize(workflow.Name)} v{Sanitize(workflow.Version)}\"",
            ct).ConfigureAwait(false);
        if (commitExit != 0)
            throw new InvalidOperationException($"Git commit failed (exit {commitExit}): {commitErr}");

        var (pushExit, _, pushErr) = await GitHelper.RunAsync(_localCachePath, "push", ct).ConfigureAwait(false);
        if (pushExit != 0)
            throw new InvalidOperationException($"Git push failed (exit {pushExit}): {pushErr}");
    }

    /// <inheritdoc/>
    public async Task<SharedWorkflow?> GetAsync(
        string nameOrId, string? version = null, CancellationToken ct = default)
    {
        await EnsureRepoAsync(ct).ConfigureAwait(false);
        await PullAsync(ct).ConfigureAwait(false);

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

        // Fall back to searching by ID
        var allWorkflows = await CatalogAsync(ct: ct).ConfigureAwait(false);
        return allWorkflows.FirstOrDefault(w =>
            string.Equals(w.Id, nameOrId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedWorkflow>> CatalogAsync(
        string? tag = null, string? author = null, CancellationToken ct = default)
    {
        await EnsureRepoAsync(ct).ConfigureAwait(false);
        await PullAsync(ct).ConfigureAwait(false);

        if (!Directory.Exists(_localCachePath))
            return [];

        var results = new List<SharedWorkflow>();

        foreach (var dir in Directory.GetDirectories(_localCachePath))
        {
            // Skip the .git directory
            if (string.Equals(Path.GetFileName(dir), ".git", StringComparison.Ordinal)) continue;

            var files = Directory.GetFiles(dir, "*.json");
            if (files.Length == 0) continue;

            // Return latest version only for catalog
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
        await EnsureRepoAsync(ct).ConfigureAwait(false);
        await PullAsync(ct).ConfigureAwait(false);

        var allFiles = Directory.GetFiles(_localCachePath, "*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
            .ToArray();

        var results = new List<SharedWorkflow>();
        var lowerQuery = query.ToLowerInvariant();

        foreach (var file in allFiles)
        {
            var workflow = await ReadAsync(file, ct).ConfigureAwait(false);
            if (workflow is null) continue;

            var matches =
                workflow.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                workflow.Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                workflow.Author.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                workflow.Tags.Any(t => t.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase));

            if (matches)
                results.Add(workflow);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedWorkflow>> VersionsAsync(
        string name, CancellationToken ct = default)
    {
        await EnsureRepoAsync(ct).ConfigureAwait(false);
        await PullAsync(ct).ConfigureAwait(false);

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

        // The local CLI catalog expects AgentWorkflowDefinition JSON, which is stored in
        // SharedWorkflow.DefinitionJson. Prefer writing that directly; fall back to the
        // wrapper object if it's missing.
        var json = !string.IsNullOrWhiteSpace(workflow.DefinitionJson)
            ? workflow.DefinitionJson!
            : JsonSerializer.Serialize(workflow, JsonOptions);
        await File.WriteAllTextAsync(destPath, json, ct).ConfigureAwait(false);

        return true;
    }

    private async Task EnsureRepoAsync(CancellationToken ct)
    {
        await GitHelper.EnsureGitAvailableAsync(ct).ConfigureAwait(false);

        if (Directory.Exists(Path.Combine(_localCachePath, ".git")))
            return;

        var parent = Path.GetDirectoryName(_localCachePath)!;
        Directory.CreateDirectory(parent);

        var dirName = Path.GetFileName(_localCachePath);
        await GitHelper.RunAsync(parent, $"clone \"{_repoUrl}\" \"{dirName}\"", ct)
            .ConfigureAwait(false);

        // If clone failed (e.g. empty repo or network), init a local repo as fallback
        if (!Directory.Exists(Path.Combine(_localCachePath, ".git")))
        {
            Directory.CreateDirectory(_localCachePath);
            await GitHelper.RunAsync(_localCachePath, "init", ct).ConfigureAwait(false);
            await GitHelper.RunAsync(_localCachePath, $"remote add origin \"{_repoUrl}\"", ct)
                .ConfigureAwait(false);
        }
    }

    private async Task PullAsync(CancellationToken ct)
    {
        // Best-effort pull — swallow errors (offline / empty repo scenarios)
        try
        {
            await GitHelper.RunAsync(_localCachePath, "pull --ff-only", ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch { /* non-critical — work with local cache */ }
#pragma warning restore CA1031
    }

    private string GetWorkflowDirectory(string name) =>
        Path.Combine(_localCachePath, Sanitize(name));

    private string GetVersionPath(string name, string version) =>
        Path.Combine(GetWorkflowDirectory(name), $"{Sanitize(version)}.json");

    private static string Sanitize(string input) =>
        string.Concat(input.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_'));

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
