namespace JD.AI.Core.Providers;

/// <summary>
/// Registry projection for model capability-aware routing.
/// </summary>
public sealed record ModelCapabilityEntry(
    string ModelId,
    string DisplayName,
    string ProviderName,
    ModelCapability Capabilities,
    int ContextWindowTokens,
    ModelCostTier CostTier);
