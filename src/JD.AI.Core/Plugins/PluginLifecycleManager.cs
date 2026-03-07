using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Plugins;

/// <summary>
/// Coordinates plugin install, enable, disable, unload, and uninstall operations.
/// </summary>
public sealed class PluginLifecycleManager : IPluginLifecycleManager
{
    private readonly IPluginInstaller _installer;
    private readonly PluginRegistryStore _registry;
    private readonly IPluginRuntime _runtime;
    private readonly IPluginContextFactory _contextFactory;
    private readonly ILogger<PluginLifecycleManager> _logger;
    private readonly PluginSecurityOptions _securityOptions;

    public PluginLifecycleManager(
        IPluginInstaller installer,
        PluginRegistryStore registry,
        IPluginRuntime runtime,
        IPluginContextFactory contextFactory,
        ILogger<PluginLifecycleManager> logger,
        PluginSecurityOptions? securityOptions = null)
    {
        _installer = installer;
        _registry = registry;
        _runtime = runtime;
        _contextFactory = contextFactory;
        _logger = logger;
        _securityOptions = securityOptions ?? PluginSecurityOptions.FromEnvironment();
    }

    public async Task<IReadOnlyList<PluginStatusInfo>> ListAsync(CancellationToken ct = default)
    {
        var records = await _registry.ListAsync(ct).ConfigureAwait(false);
        var loadedIds = _runtime.GetAll()
            .Select(p => p.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return records
            .Select(r => ToStatus(r, loadedIds.Contains(r.Id)))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<PluginStatusInfo?> GetAsync(string id, CancellationToken ct = default)
    {
        var record = await _registry.FindAsync(id, ct).ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        var loaded = _runtime.GetAll().Any(p =>
            string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        return ToStatus(record, loaded);
    }

    public async Task<PluginStatusInfo> InstallAsync(
        string source,
        bool enable,
        CancellationToken ct = default)
    {
        var artifact = await _installer.InstallAsync(source, ct).ConfigureAwait(false);
        ValidateManifestSecurity(artifact.Manifest);
        var record = new InstalledPluginRecord
        {
            Id = artifact.Manifest.Id,
            Name = artifact.Manifest.Name,
            Version = artifact.Manifest.Version,
            InstallPath = artifact.InstallPath,
            EntryAssemblyPath = artifact.EntryAssemblyPath,
            ManifestPath = artifact.ManifestPath,
            Source = artifact.Source,
            Publisher = ResolvePublisher(artifact.Manifest),
            Permissions = artifact.Manifest.Permissions
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Select(static p => p.Trim())
                .ToArray(),
            Enabled = enable,
            InstalledAtUtc = DateTimeOffset.UtcNow,
        };

        await _registry.UpsertAsync(record, ct).ConfigureAwait(false);

        if (enable)
        {
            await TryLoadAsync(record, ct).ConfigureAwait(false);
        }

        var loaded = _runtime.GetAll().Any(p =>
            string.Equals(p.Id, record.Id, StringComparison.OrdinalIgnoreCase));
        return ToStatus(record, loaded);
    }

    public async Task<PluginStatusInfo> EnableAsync(string id, CancellationToken ct = default)
    {
        var record = await _registry.FindAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Plugin '{id}' is not installed.");

        record.Enabled = true;
        await TryLoadAsync(record, ct).ConfigureAwait(false);
        await _registry.UpsertAsync(record, ct).ConfigureAwait(false);

        var loaded = _runtime.GetAll().Any(p =>
            string.Equals(p.Id, record.Id, StringComparison.OrdinalIgnoreCase));
        return ToStatus(record, loaded);
    }

    public async Task<PluginStatusInfo> DisableAsync(string id, CancellationToken ct = default)
    {
        var record = await _registry.FindAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Plugin '{id}' is not installed.");

        record.Enabled = false;
        record.LastError = null;
        await _runtime.UnloadAsync(id, ct).ConfigureAwait(false);
        await _registry.UpsertAsync(record, ct).ConfigureAwait(false);
        return ToStatus(record, loaded: false);
    }

    public async Task<PluginStatusInfo> UpdateAsync(string id, CancellationToken ct = default)
    {
        var existing = await _registry.FindAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Plugin '{id}' is not installed.");

        if (string.IsNullOrWhiteSpace(existing.Source))
        {
            throw new InvalidOperationException(
                $"Plugin '{id}' has no install source metadata and cannot be updated automatically.");
        }

        await _runtime.UnloadAsync(existing.Id, ct).ConfigureAwait(false);

        var artifact = await _installer.InstallAsync(existing.Source, ct).ConfigureAwait(false);
        ValidateManifestSecurity(artifact.Manifest);
        if (!string.Equals(artifact.Manifest.Id, existing.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Update source resolved to '{artifact.Manifest.Id}', expected '{existing.Id}'.");
        }

        var updated = new InstalledPluginRecord
        {
            Id = artifact.Manifest.Id,
            Name = artifact.Manifest.Name,
            Version = artifact.Manifest.Version,
            InstallPath = artifact.InstallPath,
            EntryAssemblyPath = artifact.EntryAssemblyPath,
            ManifestPath = artifact.ManifestPath,
            Source = existing.Source,
            Publisher = ResolvePublisher(artifact.Manifest),
            Permissions = artifact.Manifest.Permissions
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Select(static p => p.Trim())
                .ToArray(),
            Enabled = existing.Enabled,
            InstalledAtUtc = existing.InstalledAtUtc,
            LastEnabledAtUtc = existing.LastEnabledAtUtc,
            LastError = null,
        };

        if (!string.Equals(existing.InstallPath, updated.InstallPath, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(existing.InstallPath))
        {
            Directory.Delete(existing.InstallPath, recursive: true);
        }

        if (updated.Enabled)
        {
            await TryLoadAsync(updated, ct).ConfigureAwait(false);
        }

        await _registry.UpsertAsync(updated, ct).ConfigureAwait(false);

        var loaded = _runtime.GetAll().Any(p =>
            string.Equals(p.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
        return ToStatus(updated, loaded);
    }

    public async Task<IReadOnlyList<PluginStatusInfo>> UpdateAllAsync(CancellationToken ct = default)
    {
        var records = await _registry.ListAsync(ct).ConfigureAwait(false);
        var updated = new List<PluginStatusInfo>(records.Count);

        foreach (var record in records)
        {
            updated.Add(await UpdateAsync(record.Id, ct).ConfigureAwait(false));
        }

        return updated;
    }

    public async Task<bool> UninstallAsync(string id, CancellationToken ct = default)
    {
        var record = await _registry.FindAsync(id, ct).ConfigureAwait(false);
        if (record is null)
        {
            return false;
        }

        await _runtime.UnloadAsync(id, ct).ConfigureAwait(false);
        if (Directory.Exists(record.InstallPath))
        {
            Directory.Delete(record.InstallPath, recursive: true);
        }

        return await _registry.RemoveAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<int> LoadEnabledAsync(CancellationToken ct = default)
    {
        var loaded = 0;
        var records = await _registry.ListAsync(ct).ConfigureAwait(false);
        foreach (var record in records.Where(r => r.Enabled))
        {
            var result = await TryLoadAsync(record, ct).ConfigureAwait(false);
            if (result)
            {
                loaded++;
            }

            await _registry.UpsertAsync(record, ct).ConfigureAwait(false);
        }

        return loaded;
    }

    private async Task<bool> TryLoadAsync(InstalledPluginRecord record, CancellationToken ct)
    {
        try
        {
            var manifest = await PluginManifestReader.ReadAsync(record.ManifestPath, ct).ConfigureAwait(false);
            ValidateManifestSecurity(manifest);
            PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, record.EntryAssemblyPath);

            var permissions = manifest.Permissions
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Select(static p => p.Trim())
                .ToArray();

            var secureContext = new PermissionEnforcedPluginContext(
                _contextFactory.CreateContext(),
                record.Id,
                permissions,
                _logger);

            await _runtime
                .LoadAssemblyAsync(record.EntryAssemblyPath, secureContext, record.Id, ct)
                .ConfigureAwait(false);
            record.LastEnabledAtUtc = DateTimeOffset.UtcNow;
            record.LastError = null;
            record.Permissions = permissions;
            record.Publisher = ResolvePublisher(manifest);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            record.LastError = ex.Message;
            _logger.LogError(ex, "Failed to enable plugin {PluginId}", record.Id);
            return false;
        }
    }

    private static PluginStatusInfo ToStatus(InstalledPluginRecord record, bool loaded)
    {
        return new PluginStatusInfo(
            Id: record.Id,
            Name: record.Name,
            Version: record.Version,
            Enabled: record.Enabled,
            Loaded: loaded,
            InstallPath: record.InstallPath,
            EntryAssemblyPath: record.EntryAssemblyPath,
            Source: record.Source,
            InstalledAtUtc: record.InstalledAtUtc,
            LastEnabledAtUtc: record.LastEnabledAtUtc,
            LastError: record.LastError);
    }

    private void ValidateManifestSecurity(PluginManifest manifest)
    {
        if (!_securityOptions.EnforceTrustedPublishers)
        {
            return;
        }

        var publisher = ResolvePublisher(manifest);
        if (string.IsNullOrWhiteSpace(publisher))
        {
            throw new InvalidDataException(
                $"Plugin '{manifest.Id}' is missing publisher metadata. " +
                "Set 'publisher' in plugin.json.");
        }

        if (!_securityOptions.TrustedPublishers.Contains(publisher))
        {
            throw new InvalidDataException(
                $"Plugin '{manifest.Id}' publisher '{publisher}' is not in trusted publishers: " +
                $"{string.Join(", ", _securityOptions.TrustedPublishers)}");
        }
    }

    private static string? ResolvePublisher(PluginManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Publisher))
        {
            return manifest.Publisher.Trim();
        }

        return string.IsNullOrWhiteSpace(manifest.Author)
            ? null
            : manifest.Author.Trim();
    }
}
