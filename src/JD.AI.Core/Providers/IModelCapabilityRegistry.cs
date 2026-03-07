namespace JD.AI.Core.Providers;

/// <summary>
/// Tracks model capabilities and supports capability-based lookups.
/// </summary>
public interface IModelCapabilityRegistry
{
    /// <summary>Removes all registered model capability entries.</summary>
    void Clear();

    /// <summary>Registers or updates capability metadata for a model.</summary>
    void Register(ProviderModelInfo model);

    /// <summary>Registers or updates capability metadata for a set of models.</summary>
    void RegisterRange(IEnumerable<ProviderModelInfo> models);

    /// <summary>Returns all known entries.</summary>
    IReadOnlyList<ModelCapabilityEntry> GetAll();

    /// <summary>
    /// Finds models that satisfy all required capabilities.
    /// Optional provider filter is case-insensitive.
    /// </summary>
    IReadOnlyList<ModelCapabilityEntry> FindModels(
        ModelCapability requiredCapabilities,
        string? providerName = null);
}
