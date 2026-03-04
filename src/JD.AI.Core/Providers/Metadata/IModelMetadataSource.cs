namespace JD.AI.Core.Providers.Metadata;

/// <summary>
/// Abstraction for fetching raw model metadata JSON. Exists for testability.
/// </summary>
public interface IModelMetadataSource
{
    /// <summary>
    /// Fetches the raw JSON string, or null on any failure.
    /// </summary>
    Task<string?> FetchAsync(CancellationToken ct = default);
}
