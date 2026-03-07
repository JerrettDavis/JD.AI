using JD.AI.Plugins.SDK;

namespace JD.AI.Core.Plugins;

/// <summary>
/// Persisted metadata for an installed plugin package.
/// </summary>
public sealed record InstalledPluginRecord
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string InstallPath { get; init; }
    public required string EntryAssemblyPath { get; init; }
    public required string ManifestPath { get; init; }
    public required string Source { get; init; }
    public string? Publisher { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public bool Enabled { get; set; } = true;
    public DateTimeOffset InstalledAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastEnabledAtUtc { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// Runtime view of plugin installation + load state.
/// </summary>
public sealed record PluginStatusInfo(
    string Id,
    string Name,
    string Version,
    bool Enabled,
    bool Loaded,
    string InstallPath,
    string EntryAssemblyPath,
    string Source,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset? LastEnabledAtUtc,
    string? LastError);

/// <summary>
/// Result produced by an installer when a source is materialized to the plugins directory.
/// </summary>
public sealed record PluginInstallArtifact(
    PluginManifest Manifest,
    string InstallPath,
    string EntryAssemblyPath,
    string ManifestPath,
    string Source);

internal sealed record PluginRegistryDocument(
    List<InstalledPluginRecord> Plugins);
