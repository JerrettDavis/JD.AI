using JD.AI.Core.Providers;
using JD.SemanticKernel.Extensions.Compaction;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Core.PromptCaching;

/// <summary>
/// Applies platform-level prompt caching directives to execution settings.
/// </summary>
public static class PromptCachePolicy
{
    public const string EnabledExtensionKey = "jdai_prompt_cache_enabled";
    public const string TtlExtensionKey = "jdai_prompt_cache_ttl";

    private const int SonnetOpusMinTokens = 1024;
    private const int HaikuMinTokens = 2048;

    public static void Apply(
        PromptExecutionSettings settings,
        ProviderModelInfo? model,
        ChatHistory history,
        bool enabled,
        PromptCacheTtl ttl)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(history);

        Apply(settings, model?.ProviderName, model?.Id, history, enabled, ttl);
    }

    public static void Apply(
        PromptExecutionSettings settings,
        string? providerName,
        string? modelId,
        ChatHistory history,
        bool enabled,
        PromptCacheTtl ttl)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(history);

        if (!enabled || !IsSupportedProvider(providerName, modelId))
        {
            return;
        }

        var tokenCount = TokenEstimator.EstimateTokens(history);
        if (tokenCount < GetMinimumPromptTokens(modelId))
        {
            return;
        }

        var extensionData = CopyOrCreate(settings.ExtensionData);
        extensionData[EnabledExtensionKey] = true;
        extensionData[TtlExtensionKey] = ToToken(ttl);
        settings.ExtensionData = extensionData;
    }

    internal static bool IsSupportedProvider(string? providerName, string? modelId)
    {
        if (!string.IsNullOrWhiteSpace(providerName) &&
            (providerName.Contains("anthropic", StringComparison.OrdinalIgnoreCase) ||
             providerName.Contains("claude", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(modelId) &&
               modelId.Contains("claude", StringComparison.OrdinalIgnoreCase);
    }

    internal static int GetMinimumPromptTokens(string? modelId)
    {
        return !string.IsNullOrWhiteSpace(modelId) &&
               modelId.Contains("haiku", StringComparison.OrdinalIgnoreCase)
            ? HaikuMinTokens
            : SonnetOpusMinTokens;
    }

    internal static string ToToken(PromptCacheTtl ttl) => ttl switch
    {
        PromptCacheTtl.OneHour => "1h",
        _ => "5m",
    };

    internal static bool TryParseTtl(string? value, out PromptCacheTtl ttl)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "1h":
            case "one_hour":
            case "onehour":
            case "hour":
                ttl = PromptCacheTtl.OneHour;
                return true;

            case "5m":
            case "five_minutes":
            case "fiveminutes":
            case "default":
                ttl = PromptCacheTtl.FiveMinutes;
                return true;

            default:
                ttl = PromptCacheTtl.FiveMinutes;
                return false;
        }
    }

    private static Dictionary<string, object> CopyOrCreate(
        IDictionary<string, object>? extensionData)
    {
        return extensionData is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(extensionData, StringComparer.Ordinal);
    }
}
