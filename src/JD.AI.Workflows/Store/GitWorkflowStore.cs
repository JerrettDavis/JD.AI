using System.Text.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Config;

namespace JD.AI.Workflows.Store;

/// <summary>
/// Git-backed workflow store. Wraps a <see cref="FileWorkflowStore"/> whose base directory
/// is a cloned Git repository. Operations pull before reading and push after writing so the
/// store stays in sync across machines.
/// </summary>
/// <remarks>
/// Uses the <c>git</c> CLI via <see cref="GitHelper"/> rather than LibGit2Sharp
/// to avoid heavy native library dependencies.
/// </remarks>
public sealed class GitWorkflowStore : IWorkflowStore
{
    private readonly string _repoUrl;
    private readonly string _localCachePath;
    private readonly FileWorkflowStore _local;

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
        _local = new FileWorkflowStore(_localCachePath);
    }

    /// <inheritdoc/>
    public async Task PublishAsync(SharedWorkflow workflow, CancellationToken ct = default)
    {
        await EnsureRepoAsync(ct).ConfigureAwait(false);
        await PullAsync(ct).ConfigureAwait(false);

        await _local.PublishAsync(workflow, ct).ConfigureAwait(false);

        var relativePath = Path.Combine(
            FileWorkflowStore.Sanitize(workflow.Name),
            $"{FileWorkflowStore.Sanitize(workflow.Version)}.json");

        var (addExit, _, addErr) = await GitHelper.RunAsync(
            _localCachePath, $"add \"{relativePath}\"", ct).ConfigureAwait(false);
        if (addExit != 0)
            throw new InvalidOperationException($"Git add failed (exit {addExit}): {addErr}");

        var (commitExit, _, commitErr) = await GitHelper.RunAsync(
            _localCachePath,
            $"commit -m \"publish: {FileWorkflowStore.Sanitize(workflow.Name)} v{FileWorkflowStore.Sanitize(workflow.Version)}\"",
            ct).ConfigureAwait(false);
        if (commitExit != 0)
            throw new InvalidOperationException($"Git commit failed (exit {commitExit}): {commitErr}");

        var (pushExit, _, pushErr) = await GitHelper.RunAsync(
            _localCachePath, "push", ct).ConfigureAwait(false);
        if (pushExit != 0)
            throw new InvalidOperationException($"Git push failed (exit {pushExit}): {pushErr}");
    }

    /// <inheritdoc/>
    public async Task<SharedWorkflow?> GetAsync(
        string nameOrId, string? version = null, CancellationToken ct = default)
    {
        await SyncAsync(ct).ConfigureAwait(false);
        return await _local.GetAsync(nameOrId, version, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedWorkflow>> CatalogAsync(
        string? tag = null, string? author = null, CancellationToken ct = default)
    {
        await SyncAsync(ct).ConfigureAwait(false);
        return await _local.CatalogAsync(tag, author, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedWorkflow>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        await SyncAsync(ct).ConfigureAwait(false);
        return await _local.SearchAsync(query, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedWorkflow>> VersionsAsync(
        string name, CancellationToken ct = default)
    {
        await SyncAsync(ct).ConfigureAwait(false);
        return await _local.VersionsAsync(name, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> InstallAsync(
        string nameOrId, string? version, string localDirectory, CancellationToken ct = default)
    {
        await SyncAsync(ct).ConfigureAwait(false);
        return await _local.InstallAsync(nameOrId, version, localDirectory, ct).ConfigureAwait(false);
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        await EnsureRepoAsync(ct).ConfigureAwait(false);
        await PullAsync(ct).ConfigureAwait(false);
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
}
