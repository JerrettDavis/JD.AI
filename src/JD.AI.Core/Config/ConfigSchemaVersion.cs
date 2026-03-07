using System.Text.Json;
using JD.AI.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Config;

/// <summary>
/// Tracks configuration schema versions and provides migration support.
/// Schema versions are stored alongside config files and checked on load
/// to detect when migration is needed.
/// </summary>
public sealed class ConfigSchemaVersion
{
    /// <summary>Current schema version for JD.AI configuration.</summary>
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    private readonly string _versionFilePath;
    private readonly ILogger? _logger;

    /// <param name="dataDir">Directory where version metadata is stored (typically ~/.jdai/).</param>
    /// <param name="logger">Optional logger.</param>
    public ConfigSchemaVersion(string dataDir, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dataDir);
        _versionFilePath = Path.Combine(dataDir, "schema-version.json");
        _logger = logger;
    }

    /// <summary>
    /// Reads the stored schema version, or returns 0 if no version file exists
    /// (indicating a pre-versioning installation).
    /// </summary>
    public int GetStoredVersion()
    {
        if (!File.Exists(_versionFilePath))
            return 0;

        try
        {
            var json = File.ReadAllText(_versionFilePath);
            var meta = JsonSerializer.Deserialize<SchemaVersionMeta>(json, JsonOptions);
            return meta?.Version ?? 0;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger?.LogWarning(ex, "Failed to read schema version file");
            return 0;
        }
    }

    /// <summary>
    /// Checks if migration is needed by comparing stored version to current.
    /// </summary>
    public bool NeedsMigration() => GetStoredVersion() < CurrentVersion;

    /// <summary>
    /// Stamps the current schema version after a successful migration or fresh install.
    /// </summary>
    public void StampCurrentVersion()
    {
        var dir = Path.GetDirectoryName(_versionFilePath);
        if (dir is not null) Directory.CreateDirectory(dir);

        var meta = new SchemaVersionMeta
        {
            Version = CurrentVersion,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = Environment.UserName,
        };

        var json = JsonSerializer.Serialize(meta, JsonOptions);
        File.WriteAllText(_versionFilePath, json);

        _logger?.LogInformation(
            "Config schema version stamped: v{Version}", CurrentVersion);
    }

    /// <summary>
    /// Runs all pending migrations from stored version to current version.
    /// Returns the number of migrations applied.
    /// </summary>
    public int ApplyMigrations()
    {
        var stored = GetStoredVersion();
        if (stored >= CurrentVersion)
            return 0;

        var applied = 0;

        // Migration v0 → v1: initial schema version stamp (no data changes needed)
        if (stored < 1)
        {
            _logger?.LogInformation("Applying config migration v0 → v1: initial version stamp");
            applied++;
        }

        // Future migrations would go here:
        // if (stored < 2) { MigrateV1ToV2(); applied++; }

        StampCurrentVersion();
        return applied;
    }
}

/// <summary>
/// Schema version metadata stored in <c>schema-version.json</c>.
/// </summary>
public sealed class SchemaVersionMeta
{
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
