using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Usage;

/// <summary>
/// Configurable cost rate lookup for provider/model combinations.
/// Supports exact match, glob patterns, provider defaults, and a global fallback.
/// </summary>
public sealed class CostRateProvider
{
    private readonly List<CostRateEntry> _rates = [];
    private const decimal DefaultInputRate = 0.0m;
    private const decimal DefaultOutputRate = 0.0m;

    public CostRateProvider()
    {
        LoadDefaults();
    }

    /// <summary>Gets cost per token (input, output) for a provider/model.</summary>
    public (decimal InputPerToken, decimal OutputPerToken) GetRate(string provider, string model)
    {
        // Try exact match first
        var exact = _rates.FirstOrDefault(r =>
            string.Equals(r.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Model, model, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
            return (exact.InputCostPerMillionTokens / 1_000_000m, exact.OutputCostPerMillionTokens / 1_000_000m);

        // Try provider wildcard (model = "*")
        var providerDefault = _rates.FirstOrDefault(r =>
            string.Equals(r.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            r.Model is "*");

        if (providerDefault is not null)
            return (providerDefault.InputCostPerMillionTokens / 1_000_000m, providerDefault.OutputCostPerMillionTokens / 1_000_000m);

        // Try glob match on model name
        var glob = _rates.FirstOrDefault(r =>
            string.Equals(r.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            r.Model.Contains('*') &&
            GlobMatch(model, r.Model));

        if (glob is not null)
            return (glob.InputCostPerMillionTokens / 1_000_000m, glob.OutputCostPerMillionTokens / 1_000_000m);

        // Global fallback
        return (DefaultInputRate, DefaultOutputRate);
    }

    /// <summary>Calculates estimated cost for a turn.</summary>
    public decimal CalculateCost(string provider, string model, long promptTokens, long completionTokens)
    {
        var (inputRate, outputRate) = GetRate(provider, model);
        return (promptTokens * inputRate) + (completionTokens * outputRate);
    }

    /// <summary>Adds or updates a rate entry.</summary>
    public void SetRate(string provider, string model, decimal inputPerMillion, decimal outputPerMillion)
    {
        var existing = _rates.FindIndex(r =>
            string.Equals(r.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Model, model, StringComparison.OrdinalIgnoreCase));

        var entry = new CostRateEntry
        {
            Provider = provider,
            Model = model,
            InputCostPerMillionTokens = inputPerMillion,
            OutputCostPerMillionTokens = outputPerMillion,
        };

        if (existing >= 0)
            _rates[existing] = entry;
        else
            _rates.Add(entry);
    }

    private static readonly JsonSerializerOptions s_jsonOptions = JsonDefaults.Options;

    /// <summary>Loads rate overrides from a JSON file.</summary>
    public async Task LoadFromFileAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return;

        var json = await File.ReadAllTextAsync(path, ct);
        var entries = JsonSerializer.Deserialize<List<CostRateEntry>>(json, s_jsonOptions);

        if (entries is null) return;

        foreach (var entry in entries)
        {
            SetRate(entry.Provider, entry.Model, entry.InputCostPerMillionTokens, entry.OutputCostPerMillionTokens);
        }
    }

    private static bool GlobMatch(string text, string pattern)
    {
        // Simple glob: only supports trailing wildcard "prefix*" or leading "*suffix"
        if (pattern.EndsWith('*'))
            return text.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        if (pattern.StartsWith('*'))
            return text.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadDefaults()
    {
        // Claude (via Claude Code / Anthropic)
        SetRate("Claude Code", "claude-opus-4.6", 15.00m, 75.00m);
        SetRate("Claude Code", "claude-sonnet-4.6", 3.00m, 15.00m);
        SetRate("Claude Code", "claude-haiku-4.5", 0.80m, 4.00m);
        SetRate("Anthropic", "claude-opus*", 15.00m, 75.00m);
        SetRate("Anthropic", "claude-sonnet*", 3.00m, 15.00m);
        SetRate("Anthropic", "claude-haiku*", 0.80m, 4.00m);

        // OpenAI / GitHub Copilot
        SetRate("OpenAI", "gpt-4o", 2.50m, 10.00m);
        SetRate("OpenAI", "gpt-4o-mini", 0.15m, 0.60m);
        SetRate("OpenAI", "gpt-4.1", 2.00m, 8.00m);
        SetRate("OpenAI", "gpt-4.1-mini", 0.40m, 1.60m);
        SetRate("OpenAI", "gpt-4.1-nano", 0.10m, 0.40m);
        SetRate("OpenAI", "o3", 2.00m, 8.00m);
        SetRate("OpenAI", "o3-mini", 1.10m, 4.40m);
        SetRate("GitHub Copilot", "*", 0.00m, 0.00m); // Included in subscription
        SetRate("OpenAI Codex", "*", 2.50m, 10.00m);

        // Google
        SetRate("Google Gemini", "gemini-2.5-pro*", 1.25m, 10.00m);
        SetRate("Google Gemini", "gemini-2.5-flash*", 0.15m, 0.60m);

        // Mistral
        SetRate("Mistral", "mistral-large*", 2.00m, 6.00m);
        SetRate("Mistral", "mistral-small*", 0.20m, 0.60m);

        // Local models (free)
        SetRate("Ollama", "*", 0.00m, 0.00m);
        SetRate("Foundry Local", "*", 0.00m, 0.00m);
        SetRate("Local", "*", 0.00m, 0.00m);
        SetRate("LLamaSharp", "*", 0.00m, 0.00m);
    }
}

/// <summary>A cost rate entry for a provider/model combination.</summary>
public sealed record CostRateEntry
{
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public decimal InputCostPerMillionTokens { get; init; }
    public decimal OutputCostPerMillionTokens { get; init; }
}
