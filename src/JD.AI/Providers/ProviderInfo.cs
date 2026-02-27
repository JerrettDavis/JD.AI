namespace JD.AI.Tui.Providers;

/// <summary>
/// Describes a detected AI provider and its available models.
/// </summary>
public sealed record ProviderInfo(
    string Name,
    bool IsAvailable,
    string? StatusMessage,
    IReadOnlyList<ProviderModelInfo> Models);

/// <summary>
/// A model available through a provider.
/// </summary>
public sealed record ProviderModelInfo(
    string Id,
    string DisplayName,
    string ProviderName);
