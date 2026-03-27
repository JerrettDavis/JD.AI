namespace JD.AI.Gateway.Client.Models;

public sealed record ProviderInfo(
    string Name,
    bool IsAvailable,
    string? StatusMessage,
    ProviderModelInfo[] Models
);

public sealed record ProviderModelInfo(
    string Id,
    string DisplayName,
    string ProviderName
);
