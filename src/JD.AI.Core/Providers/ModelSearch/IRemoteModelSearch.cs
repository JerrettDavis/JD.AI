namespace JD.AI.Core.Providers.ModelSearch;

/// <summary>
/// Searches a provider's model catalog and optionally pulls models locally.
/// </summary>
public interface IRemoteModelSearch
{
    string ProviderName { get; }
    Task<IReadOnlyList<RemoteModelResult>> SearchAsync(string query, CancellationToken ct = default);
    Task<bool> PullAsync(RemoteModelResult model, IProgress<string>? progress = null, CancellationToken ct = default);
}

/// <summary>
/// Describes a model discovered via remote or local catalog search.
/// </summary>
public sealed record RemoteModelResult(
    string Id,
    string DisplayName,
    string ProviderName,
    string? Size,
    string Status,
    string? Description,
    ModelCapabilities Capabilities = ModelCapabilities.Chat | ModelCapabilities.ToolCalling);
