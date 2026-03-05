namespace JD.AI.Core.Config;

/// <summary>
/// Abstraction for retrieving configuration from a remote source.
/// Implementations may connect to Consul, Azure App Configuration,
/// Kubernetes ConfigMaps, or any HTTP-based config service.
/// </summary>
public interface IRemoteConfigSource
{
    /// <summary>Human-readable name of this config source (e.g. "consul", "http", "k8s-configmap").</summary>
    string Name { get; }

    /// <summary>
    /// Fetches the current configuration content from the remote source.
    /// Returns <c>null</c> if the source is unreachable or has no content.
    /// </summary>
    Task<RemoteConfigResult?> FetchAsync(CancellationToken ct = default);
}

/// <summary>
/// Result of fetching configuration from a remote source.
/// </summary>
public sealed class RemoteConfigResult
{
    /// <summary>The raw configuration content (YAML or JSON).</summary>
    public required string Content { get; init; }

    /// <summary>
    /// An opaque version identifier (e.g. HTTP ETag, Consul ModifyIndex, file hash).
    /// Used for change detection without full content comparison.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>Content type hint: "yaml", "json", or null for auto-detect.</summary>
    public string? ContentType { get; init; }

    /// <summary>When this content was last modified at the source.</summary>
    public DateTimeOffset? LastModified { get; init; }
}
