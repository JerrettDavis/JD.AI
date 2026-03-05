using System.Text.Json;
using JD.AI.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace JD.AI.Workflows.Security;

/// <summary>
/// Manages a registry of trusted workflow publishers. Only workflows
/// from trusted publishers can be installed and executed when trust
/// enforcement is enabled.
/// </summary>
public sealed class TrustedPublisherRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    private readonly string _registryPath;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private TrustRegistryData _data;

    /// <param name="registryPath">Path to the trust registry JSON file.</param>
    /// <param name="logger">Optional logger.</param>
    public TrustedPublisherRegistry(string registryPath, ILogger? logger = null)
    {
        _registryPath = registryPath ?? throw new ArgumentNullException(nameof(registryPath));
        _logger = logger;
        _data = Load();
    }

    /// <summary>Number of trusted publishers.</summary>
    public int Count
    {
        get { lock (_lock) return _data.Publishers.Count; }
    }

    /// <summary>
    /// Checks if a publisher (author name) is trusted.
    /// </summary>
    public bool IsTrusted(string author)
    {
        if (string.IsNullOrWhiteSpace(author)) return false;

        lock (_lock)
        {
            return _data.Publishers.Any(
                p => string.Equals(p.Name, author, StringComparison.OrdinalIgnoreCase)
                     && !p.Revoked);
        }
    }

    /// <summary>
    /// Adds a publisher to the trusted registry.
    /// </summary>
    public void Trust(string name, string? fingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_lock)
        {
            if (_data.Publishers.Any(
                    p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                _logger?.LogDebug("Publisher '{Name}' is already trusted", name);
                return;
            }

            _data.Publishers.Add(new TrustedPublisher
            {
                Name = name,
                Fingerprint = fingerprint,
                TrustedAt = DateTimeOffset.UtcNow,
            });

            Save();
            _logger?.LogInformation("Trusted publisher added: {Name}", name);
        }
    }

    /// <summary>
    /// Revokes trust for a publisher without removing the record (for audit trail).
    /// </summary>
    public void Revoke(string name)
    {
        lock (_lock)
        {
            var pub = _data.Publishers.FirstOrDefault(
                p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (pub is null)
            {
                _logger?.LogWarning("Publisher '{Name}' not found in registry", name);
                return;
            }

            pub.Revoked = true;
            pub.RevokedAt = DateTimeOffset.UtcNow;
            Save();
            _logger?.LogInformation("Publisher trust revoked: {Name}", name);
        }
    }

    /// <summary>Returns all registered publishers (including revoked).</summary>
    public IReadOnlyList<TrustedPublisher> GetAll()
    {
        lock (_lock)
        {
            return _data.Publishers.ToList().AsReadOnly();
        }
    }

    private TrustRegistryData Load()
    {
        if (!File.Exists(_registryPath))
            return new TrustRegistryData();

        try
        {
            var json = File.ReadAllText(_registryPath);
            return JsonSerializer.Deserialize<TrustRegistryData>(json, JsonOptions)
                   ?? new TrustRegistryData();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger?.LogWarning(ex, "Failed to load trust registry, starting fresh");
            return new TrustRegistryData();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_registryPath);
        if (dir is not null) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_data, JsonOptions);
        File.WriteAllText(_registryPath, json);
    }
}

/// <summary>Root container for trust registry file.</summary>
public sealed class TrustRegistryData
{
    public List<TrustedPublisher> Publishers { get; set; } = [];
}

/// <summary>A trusted workflow publisher entry.</summary>
public sealed class TrustedPublisher
{
    public string Name { get; set; } = string.Empty;
    public string? Fingerprint { get; set; }
    public DateTimeOffset TrustedAt { get; set; }
    public bool Revoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
