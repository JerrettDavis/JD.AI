namespace JD.AI.Core.Providers;

/// <summary>
/// Default in-memory capability registry populated from provider model info.
/// </summary>
public sealed class ModelCapabilityRegistry : IModelCapabilityRegistry
{
    private readonly Dictionary<string, ModelCapabilityEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public void Clear() => _entries.Clear();

    public void Register(ProviderModelInfo model)
    {
        var key = BuildKey(model.ProviderName, model.Id);
        _entries[key] = Project(model);
    }

    public void RegisterRange(IEnumerable<ProviderModelInfo> models)
    {
        foreach (var model in models)
            Register(model);
    }

    public IReadOnlyList<ModelCapabilityEntry> GetAll() => _entries.Values.ToList();

    public IReadOnlyList<ModelCapabilityEntry> FindModels(
        ModelCapability requiredCapabilities,
        string? providerName = null)
    {
        IEnumerable<ModelCapabilityEntry> query = _entries.Values;

        if (!string.IsNullOrWhiteSpace(providerName))
        {
            query = query.Where(entry =>
                string.Equals(entry.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .Where(entry => (entry.Capabilities & requiredCapabilities) == requiredCapabilities)
            .OrderByDescending(entry => entry.ContextWindowTokens)
            .ThenBy(entry => entry.CostTier)
            .ThenBy(entry => entry.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildKey(string providerName, string modelId) =>
        $"{providerName}:{modelId}";

    private static ModelCapabilityEntry Project(ProviderModelInfo model)
    {
        var capabilities = ModelCapability.ChatCompletion | ModelCapability.Streaming;

        if (model.Capabilities.HasFlag(ModelCapabilities.ToolCalling))
            capabilities |= ModelCapability.ToolCalling | ModelCapability.JsonMode;
        if (model.Capabilities.HasFlag(ModelCapabilities.Vision))
            capabilities |= ModelCapability.Vision;
        if (model.Capabilities.HasFlag(ModelCapabilities.Embeddings))
            capabilities |= ModelCapability.Embeddings;

        var tier = InferCostTier(model);
        return new ModelCapabilityEntry(
            model.Id,
            model.DisplayName,
            model.ProviderName,
            capabilities,
            model.ContextWindowTokens,
            tier);
    }

    private static ModelCostTier InferCostTier(ProviderModelInfo model)
    {
        var input = model.InputCostPerToken;
        var output = model.OutputCostPerToken;

        if (input <= 0m && output <= 0m)
            return ModelCostTier.Free;

        var averagePerMillion = ((input + output) / 2m) * 1_000_000m;
        if (averagePerMillion <= 1m)
            return ModelCostTier.Budget;
        if (averagePerMillion <= 10m)
            return ModelCostTier.Standard;
        return ModelCostTier.Premium;
    }
}
