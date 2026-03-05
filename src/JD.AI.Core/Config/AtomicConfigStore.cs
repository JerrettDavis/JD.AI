using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Config;

/// <summary>
/// Multi-process-safe JSON configuration file manager for JD.AI.
/// Uses file-level locking for cross-process safety and a <see cref="SemaphoreSlim"/>
/// for in-process concurrency. Writes are atomic: data is written to a temporary file
/// then moved into place.
/// </summary>
public sealed class AtomicConfigStore : IDisposable
{
    private const int MaxRetries = 5;
    private const int InitialBackoffMs = 50;

    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    private readonly string _configPath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Creates a new <see cref="AtomicConfigStore"/>.
    /// </summary>
    /// <param name="configPath">
    /// Full path to the JSON config file, or <c>null</c> to use the default
    /// <c>~/.jdai/config.json</c>.
    /// </param>
    public AtomicConfigStore(string? configPath = null)
    {
        _configPath = configPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".jdai",
                "config.json");
    }

    /// <summary>Reads the current configuration, returning an empty config when the file does not exist.</summary>
    public async Task<JdAiConfig> ReadAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ReadCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Atomically mutates the configuration. The <paramref name="mutate"/> action receives
    /// the current config (read under lock) and should apply changes in-place.
    /// </summary>
    public async Task WriteAsync(Action<JdAiConfig> mutate, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await WithFileLockAsync(async () =>
            {
                var config = await ReadCoreAsync(ct).ConfigureAwait(false);
                mutate(config);

                var json = JsonSerializer.Serialize(config, JsonOptions);

                // Validate: round-trip must produce valid JSON
                try
                {
                    JsonSerializer.Deserialize<JdAiConfig>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Mutation produced invalid JSON.", ex);
                }

                var dir = Path.GetDirectoryName(_configPath)!;
                Directory.CreateDirectory(dir);

                // Backup current file
                if (File.Exists(_configPath))
                {
                    File.Copy(_configPath, _configPath + ".bak", overwrite: true);
                }

                // Atomic write via temp file + rename
                var tmpPath = _configPath + ".tmp";
                await File.WriteAllTextAsync(tmpPath, json, ct).ConfigureAwait(false);
                File.Move(tmpPath, _configPath, overwrite: true);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Gets the default provider, optionally scoped to a project path.</summary>
    public async Task<string?> GetDefaultProviderAsync(string? projectPath = null, CancellationToken ct = default)
    {
        var config = await ReadAsync(ct).ConfigureAwait(false);
        if (projectPath is not null
            && config.ProjectDefaults.TryGetValue(projectPath, out var proj)
            && proj.Provider is not null)
        {
            return proj.Provider;
        }

        return config.Defaults.Provider;
    }

    /// <summary>Gets the default model, optionally scoped to a project path.</summary>
    public async Task<string?> GetDefaultModelAsync(string? projectPath = null, CancellationToken ct = default)
    {
        var config = await ReadAsync(ct).ConfigureAwait(false);
        if (projectPath is not null
            && config.ProjectDefaults.TryGetValue(projectPath, out var proj)
            && proj.Model is not null)
        {
            return proj.Model;
        }

        return config.Defaults.Model;
    }

    /// <summary>Sets the default provider, optionally scoped to a project path.</summary>
    public async Task SetDefaultProviderAsync(string provider, string? projectPath = null, CancellationToken ct = default)
    {
        await WriteAsync(cfg =>
        {
            if (projectPath is null)
            {
                cfg.Defaults.Provider = provider;
            }
            else
            {
                if (!cfg.ProjectDefaults.TryGetValue(projectPath, out var proj))
                {
                    proj = new DefaultsConfig();
                    cfg.ProjectDefaults[projectPath] = proj;
                }

                proj.Provider = provider;
            }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Sets the default model, optionally scoped to a project path.</summary>
    public async Task SetDefaultModelAsync(string model, string? projectPath = null, CancellationToken ct = default)
    {
        await WriteAsync(cfg =>
        {
            if (projectPath is null)
            {
                cfg.Defaults.Model = model;
            }
            else
            {
                if (!cfg.ProjectDefaults.TryGetValue(projectPath, out var proj))
                {
                    proj = new DefaultsConfig();
                    cfg.ProjectDefaults[projectPath] = proj;
                }

                proj.Model = model;
            }
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<JdAiConfig> ReadCoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_configPath))
            return new JdAiConfig();

        var json = await File.ReadAllTextAsync(_configPath, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return new JdAiConfig();

        return JsonSerializer.Deserialize<JdAiConfig>(json, JsonOptions) ?? new JdAiConfig();
    }

    /// <summary>
    /// Acquires an exclusive file-system lock with exponential backoff, executes the
    /// action, then releases the lock.
    /// </summary>
    private async Task WithFileLockAsync(Func<Task> action, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_configPath)!;
        Directory.CreateDirectory(dir);

        var lockPath = _configPath + ".lock";
        var backoffMs = InitialBackoffMs;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            FileStream? lockStream = null;
            try
            {
                lockStream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                await action().ConfigureAwait(false);
                return;
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                lockStream?.Dispose();
                await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                backoffMs *= 2;
            }
            finally
            {
                lockStream?.Dispose();
            }
        }

        throw new IOException(
            $"Failed to acquire config file lock after {MaxRetries} attempts: {lockPath}");
    }
}

/// <summary>Root configuration object for JD.AI.</summary>
public sealed class JdAiConfig
{
    /// <summary>Global default provider/model settings.</summary>
    public DefaultsConfig Defaults { get; set; } = new();

    /// <summary>Per-project default overrides keyed by absolute project path.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON deserialization")]
    public IDictionary<string, DefaultsConfig> ProjectDefaults { get; set; } = new Dictionary<string, DefaultsConfig>();
}

/// <summary>Provider and model defaults.</summary>
public sealed class DefaultsConfig
{
    /// <summary>Provider identifier (e.g. "openai", "azure").</summary>
    public string? Provider { get; set; }

    /// <summary>Model identifier (e.g. "gpt-4o").</summary>
    public string? Model { get; set; }
}
