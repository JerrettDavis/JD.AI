namespace JD.AI.Dashboard.Wasm.Models;

public record ProviderInfo(string Name, bool IsAvailable, string? StatusMessage, ProviderModelInfo[] Models);
public record ProviderModelInfo(string Id, string DisplayName, string ProviderName);
