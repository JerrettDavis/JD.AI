namespace JD.AI.Core.Providers.Metadata;

/// <summary>
/// Multi-strategy model ID matching between our <see cref="ProviderModelInfo.Id"/>
/// values and LiteLLM catalog keys.
/// </summary>
public static class ModelIdMatcher
{
    private static readonly Dictionary<string, string[]> ProviderPrefixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Anthropic"] = ["anthropic/"],
        ["AWS Bedrock"] = ["bedrock/", "bedrock_converse/"],
        ["OpenAI"] = ["openai/"],
        ["Azure OpenAI"] = ["azure/", "azure_ai/"],
        ["Google Gemini"] = ["gemini/", "vertex_ai/"],
        ["Mistral"] = ["mistral/"],
        ["GitHub Copilot"] = ["anthropic/", "openai/"],
        ["Claude Code"] = ["anthropic/"],
        ["HuggingFace"] = ["huggingface/"],
    };

    /// <summary>
    /// Finds the best matching <see cref="ModelMetadataEntry"/> for a given model ID
    /// using a ranked set of strategies: exact, provider-prefixed, bare suffix, normalized stem.
    /// </summary>
    public static ModelMetadataEntry? FindBestMatch(
        string modelId,
        string providerName,
        IReadOnlyDictionary<string, ModelMetadataEntry> entries)
    {
        // 1. Exact key match
        if (entries.TryGetValue(modelId, out var exact))
            return exact;

        // 2. Provider-prefixed match
        if (ProviderPrefixMap.TryGetValue(providerName, out var prefixes))
        {
            foreach (var prefix in prefixes)
            {
                if (entries.TryGetValue(prefix + modelId, out var prefixed))
                    return prefixed;
            }
        }

        // 3. Bare suffix match — strip prefix after last '/', compare to our modelId
        foreach (var (key, entry) in entries)
        {
            var slashIdx = key.LastIndexOf('/');
            if (slashIdx >= 0)
            {
                var suffix = key[(slashIdx + 1)..];
                if (string.Equals(suffix, modelId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
        }

        // 4. Normalized stem match — strip date suffixes, version suffixes
        var normalizedId = NormalizeStem(modelId);
        foreach (var (key, entry) in entries)
        {
            var slashIdx = key.LastIndexOf('/');
            var bare = slashIdx >= 0 ? key[(slashIdx + 1)..] : key;
            if (string.Equals(NormalizeStem(bare), normalizedId, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Strips date suffixes (-20250514), version suffixes (:0, -v1), and trailing
    /// whitespace for fuzzy comparison.
    /// </summary>
    internal static string NormalizeStem(string id)
    {
        var span = id.AsSpan().Trim();

        // Strip version-colon suffix (e.g. ":0", ":1")
        var colonIdx = span.LastIndexOf(':');
        if (colonIdx >= 0 && colonIdx < span.Length - 1)
        {
            var afterColon = span[(colonIdx + 1)..];
            if (IsAllDigits(afterColon))
                span = span[..colonIdx];
        }

        // Strip date suffixes like -20250514 (hyphen + 8 digits at end)
        if (span.Length > 9 && span[^9] == '-' && IsAllDigits(span[^8..]))
            span = span[..^9];

        // Strip version suffixes like -v1, -v2
        if (span.Length > 3 && span[^3] == '-' && span[^2] == 'v' && char.IsDigit(span[^1]))
            span = span[..^3];

        return span.ToString();
    }

    private static bool IsAllDigits(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            if (!char.IsDigit(c))
                return false;
        }

        return s.Length > 0;
    }
}
