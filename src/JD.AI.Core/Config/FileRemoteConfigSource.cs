using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Config;

/// <summary>
/// Reads configuration from a file on disk, designed for Kubernetes ConfigMap
/// volume mounts or any mounted config file that may be updated externally.
/// Detects changes via SHA-256 content hashing.
/// </summary>
public sealed class FileRemoteConfigSource : IRemoteConfigSource
{
    private readonly string _filePath;
    private readonly ILogger? _logger;
    private string? _lastHash;

    public string Name => "file";

    /// <param name="filePath">Path to the configuration file.</param>
    /// <param name="logger">Optional logger.</param>
    public FileRemoteConfigSource(string filePath, ILogger? logger = null)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _logger = logger;
    }

    public async Task<RemoteConfigResult?> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger?.LogDebug("Config file not found: {Path}", _filePath);
                return null;
            }

            var content = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
            var hash = ComputeHash(content);

            if (string.Equals(hash, _lastHash, StringComparison.OrdinalIgnoreCase))
                return null; // No change

            _lastHash = hash;

            var lastWrite = File.GetLastWriteTimeUtc(_filePath);
            var ext = Path.GetExtension(_filePath).ToLowerInvariant();

            return new RemoteConfigResult
            {
                Content = content,
                Version = hash,
                ContentType = ext is ".yaml" or ".yml" ? "yaml" : ext is ".json" ? "json" : null,
                LastModified = new DateTimeOffset(lastWrite, TimeSpan.Zero),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Failed to read config file: {Path}", _filePath);
            return null;
        }
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
