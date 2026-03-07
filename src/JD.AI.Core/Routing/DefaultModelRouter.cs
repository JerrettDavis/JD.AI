using JD.AI.Core.Providers;
using JD.AI.Core.Tracing;

namespace JD.AI.Core.Routing;

/// <summary>
/// Default provider/model router using weighted scoring across routing strategies.
/// </summary>
public sealed class DefaultModelRouter : IModelRouter
{
    public ModelRouteDecision Route(
        IReadOnlyList<ProviderModelInfo> candidates,
        RoutingPolicy policy)
    {
        if (candidates.Count == 0)
            return ModelRouteDecision.None;

        var filtered = FilterByCapabilities(candidates, policy.RequiredCapabilities);
        if (filtered.Count == 0)
            filtered = candidates.ToList();

        var scores = filtered
            .Select(model => Score(model, policy))
            .OrderByDescending(score => score.Score)
            .ThenByDescending(score => score.Model.ContextWindowTokens)
            .ToList();

        if (scores.Count == 0)
            return ModelRouteDecision.None;

        var selected = scores[0].Model;
        var fallback = BuildFallbackChain(scores.Skip(1).Select(s => s.Model).ToList(), policy);

        DebugLogger.Log(
            DebugCategory.Providers,
            "router strategy={0} selected={1}/{2} fallbacks={3}",
            policy.Strategy,
            selected.ProviderName,
            selected.Id,
            fallback.Count);

        return new ModelRouteDecision(
            SelectedModel: selected,
            FallbackModels: fallback,
            Scores: scores,
            Strategy: policy.Strategy.ToString());
    }

    private static List<ProviderModelInfo> FilterByCapabilities(
        IReadOnlyList<ProviderModelInfo> candidates,
        ModelCapabilities requiredCapabilities)
    {
        if (requiredCapabilities == ModelCapabilities.None)
            return candidates.ToList();

        return candidates
            .Where(model => model.Capabilities.HasFlag(requiredCapabilities))
            .ToList();
    }

    private static ProviderScore Score(ProviderModelInfo model, RoutingPolicy policy)
    {
        var score = policy.Strategy switch
        {
            RoutingStrategy.LocalFirst => ScoreLocalFirst(model),
            RoutingStrategy.CostOptimized => ScoreCost(model),
            RoutingStrategy.CapabilityDriven => ScoreCapability(model, policy.RequiredCapabilities),
            RoutingStrategy.LatencyOptimized => ScoreLatency(model),
            _ => 0d,
        };

        score += ProviderPreferenceBoost(model, policy.PreferredProviders);

        var reason = $"strategy={policy.Strategy}, provider={model.ProviderName}, caps={model.Capabilities}";
        return new ProviderScore(model, score, reason);
    }

    private static double ScoreLocalFirst(ProviderModelInfo model)
    {
        var localBoost = IsLocalProvider(model.ProviderName) ? 100d : 10d;
        var contextBoost = model.ContextWindowTokens / 10000d;
        return localBoost + contextBoost - CostPenalty(model);
    }

    private static double ScoreLatency(ProviderModelInfo model)
    {
        var baseScore = IsLocalProvider(model.ProviderName) ? 120d : 20d;
        return baseScore + (model.ContextWindowTokens / 20000d) - CostPenalty(model);
    }

    private static double ScoreCost(ProviderModelInfo model)
    {
        var cost = Math.Max(0m, model.InputCostPerToken + model.OutputCostPerToken);
        if (cost == 0m)
            return 1000d;

        var perMillion = (double)(cost * 1_000_000m);
        return 100d - perMillion;
    }

    private static double ScoreCapability(ProviderModelInfo model, ModelCapabilities requiredCapabilities)
    {
        var hasRequired = requiredCapabilities == ModelCapabilities.None ||
                          model.Capabilities.HasFlag(requiredCapabilities);
        var baseScore = hasRequired ? 100d : 0d;

        var richness = CountCapabilities(model.Capabilities) * 10d;
        return baseScore + richness + (model.ContextWindowTokens / 20000d) - CostPenalty(model);
    }

    private static int CountCapabilities(ModelCapabilities capabilities)
    {
        var count = 0;
        if (capabilities.HasFlag(ModelCapabilities.Chat)) count++;
        if (capabilities.HasFlag(ModelCapabilities.ToolCalling)) count++;
        if (capabilities.HasFlag(ModelCapabilities.Vision)) count++;
        if (capabilities.HasFlag(ModelCapabilities.Embeddings)) count++;
        return count;
    }

    private static double ProviderPreferenceBoost(
        ProviderModelInfo model,
        IReadOnlyList<string> preferredProviders)
    {
        for (var i = 0; i < preferredProviders.Count; i++)
        {
            if (model.ProviderName.Contains(preferredProviders[i], StringComparison.OrdinalIgnoreCase))
                return (preferredProviders.Count - i) * 15d;
        }

        return 0d;
    }

    private static List<ProviderModelInfo> BuildFallbackChain(
        List<ProviderModelInfo> candidates,
        RoutingPolicy policy)
    {
        if (candidates.Count == 0)
            return [];

        if (policy.FallbackProviders.Count == 0)
            return candidates.ToList();

        var ordered = new List<ProviderModelInfo>(candidates.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in policy.FallbackProviders)
        {
            foreach (var candidate in candidates.Where(c =>
                         c.ProviderName.Contains(provider, StringComparison.OrdinalIgnoreCase)))
            {
                var key = BuildModelKey(candidate);
                if (seen.Add(key))
                    ordered.Add(candidate);
            }
        }

        foreach (var candidate in candidates)
        {
            var key = BuildModelKey(candidate);
            if (seen.Add(key))
                ordered.Add(candidate);
        }

        return ordered;
    }

    private static bool IsLocalProvider(string providerName) =>
        providerName.Contains("ollama", StringComparison.OrdinalIgnoreCase) ||
        providerName.Contains("foundry", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(providerName, "Local", StringComparison.OrdinalIgnoreCase);

    private static double CostPenalty(ProviderModelInfo model)
    {
        var cost = Math.Max(0m, model.InputCostPerToken + model.OutputCostPerToken);
        return (double)(cost * 1_000_000m / 10m);
    }

    private static string BuildModelKey(ProviderModelInfo model) =>
        $"{model.ProviderName}:{model.Id}";
}
