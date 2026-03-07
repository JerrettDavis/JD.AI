using System.IO.Compression;
using System.Text.Json;
using JD.AI.Core.Config;

namespace JD.AI.Core.Providers.Metadata;

/// <summary>
/// Fetches, caches, parses, and applies model metadata from LiteLLM's catalog.
/// Fallback chain: fetched data -> cached data -> hardcoded record defaults.
/// </summary>
public sealed class ModelMetadataProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);

    private readonly IModelMetadataSource _source;
    private Dictionary<string, ModelMetadataEntry>? _entries;

    public ModelMetadataProvider(IModelMetadataSource? source = null)
    {
        _source = source ?? new LiteLlmMetadataSource();
    }

    /// <summary>Number of parsed catalog entries.</summary>
    public int EntryCount => _entries?.Count ?? 0;

    /// <summary>Whether the catalog has been loaded.</summary>
    public bool IsLoaded => _entries is not null;

    /// <summary>When the catalog was last fetched (from meta file).</summary>
    public DateTime? LastFetched { get; private set; }

    private static string CacheFile => Path.Combine(DataDirectories.Root, "model-metadata.json.gz");
    private static string MetaFile => Path.Combine(DataDirectories.Root, "model-metadata-meta.json");

    /// <summary>
    /// Loads model metadata. Tries cache first (within TTL), then fetches,
    /// then falls back to stale cache. Never throws.
    /// </summary>
    public async Task LoadAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        try
        {
            // Try cache if within TTL
            if (!forceRefresh)
            {
                var meta = ReadMeta();
                if (meta is not null && DateTime.UtcNow - meta.FetchedAt < CacheTtl)
                {
                    var cached = await ReadCacheAsync(ct).ConfigureAwait(false);
                    if (cached is not null)
                    {
                        _entries = cached;
                        LastFetched = meta.FetchedAt;
                        return;
                    }
                }
            }

            // Fetch from source
            var json = await _source.FetchAsync(ct).ConfigureAwait(false);
            if (json is not null)
            {
                _entries = ParseLiteLlmJson(json);
                LastFetched = DateTime.UtcNow;

                // Cache on background thread
                _ = Task.Run(() => WriteCacheAsync(json), CancellationToken.None);
                return;
            }

            // Fall back to stale cache
            var stale = await ReadCacheAsync(ct).ConfigureAwait(false);
            if (stale is not null)
            {
                _entries = stale;
                var staleMeta = ReadMeta();
                LastFetched = staleMeta?.FetchedAt;
            }
        }
#pragma warning disable CA1031 // Never throws — graceful degradation to empty entries
        catch
#pragma warning restore CA1031
        {
            // Graceful degradation: no metadata available
        }
    }

    /// <summary>
    /// Enriches a list of models with metadata from the loaded catalog.
    /// Unmatched models are returned unchanged.
    /// </summary>
    public IReadOnlyList<ProviderModelInfo> Enrich(IReadOnlyList<ProviderModelInfo> models)
    {
        if (_entries is null || _entries.Count == 0)
            return models;

        var result = new List<ProviderModelInfo>(models.Count);
        foreach (var model in models)
        {
            var match = ModelIdMatcher.FindBestMatch(model.Id, model.ProviderName, _entries);
            if (match is not null)
            {
                var capabilities = ApplyCapabilities(model.Capabilities, match);
                result.Add(model with
                {
                    ContextWindowTokens = match.MaxInputTokens ?? model.ContextWindowTokens,
                    MaxOutputTokens = match.MaxOutputTokens ?? model.MaxOutputTokens,
                    InputCostPerToken = match.InputCostPerToken ?? model.InputCostPerToken,
                    OutputCostPerToken = match.OutputCostPerToken ?? model.OutputCostPerToken,
                    Capabilities = capabilities,
                    HasMetadata = true,
                });
            }
            else
            {
                result.Add(model);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses the LiteLLM JSON catalog into a dictionary of entries.
    /// Filters to chat/completion modes, skips sample_spec.
    /// </summary>
    internal static Dictionary<string, ModelMetadataEntry> ParseLiteLlmJson(string json)
    {
        var entries = new Dictionary<string, ModelMetadataEntry>(StringComparer.Ordinal);

        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (string.Equals(prop.Name, "sample_spec", StringComparison.Ordinal))
                continue;

            if (prop.Value.ValueKind != JsonValueKind.Object)
                continue;

            var mode = GetStringProp(prop.Value, "mode");
            if (mode is not null &&
                !string.Equals(mode, "chat", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(mode, "completion", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries[prop.Name] = new ModelMetadataEntry
            {
                Key = prop.Name,
                LitellmProvider = GetStringProp(prop.Value, "litellm_provider"),
                Mode = mode,
                MaxInputTokens = GetIntProp(prop.Value, "max_input_tokens"),
                MaxOutputTokens = GetIntProp(prop.Value, "max_output_tokens") ?? GetIntProp(prop.Value, "max_tokens"),
                InputCostPerToken = GetDecimalProp(prop.Value, "input_cost_per_token"),
                OutputCostPerToken = GetDecimalProp(prop.Value, "output_cost_per_token"),
                SupportsVision = GetNullableBoolProp(prop.Value, "supports_vision"),
                SupportsFunctionCalling = GetNullableBoolProp(prop.Value, "supports_function_calling"),
                SupportsReasoning = GetNullableBoolProp(prop.Value, "supports_reasoning"),
            };
        }

        return entries;
    }

    private static string? GetStringProp(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int? GetIntProp(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v))
            return null;

        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt32(out var i) ? i : null,
            _ => null,
        };
    }

    private static decimal? GetDecimalProp(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v))
            return null;

        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetDecimal(out var d) ? d : null,
            _ => null,
        };
    }

    private static bool? GetNullableBoolProp(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static ModelCapabilities ApplyCapabilities(
        ModelCapabilities existing,
        ModelMetadataEntry entry)
    {
        var capabilities = existing | ModelCapabilities.Chat;

        if (entry.SupportsFunctionCalling is true)
            capabilities |= ModelCapabilities.ToolCalling;
        else if (entry.SupportsFunctionCalling is false)
            capabilities &= ~ModelCapabilities.ToolCalling;

        if (entry.SupportsVision is true)
            capabilities |= ModelCapabilities.Vision;
        else if (entry.SupportsVision is false)
            capabilities &= ~ModelCapabilities.Vision;

        return capabilities;
    }

    private static async Task<Dictionary<string, ModelMetadataEntry>?> ReadCacheAsync(
        CancellationToken ct)
    {
        if (!File.Exists(CacheFile))
            return null;

        try
        {
            await using var fs = File.OpenRead(CacheFile);
            await using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var reader = new StreamReader(gz);
            var json = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
            return ParseLiteLlmJson(json);
        }
#pragma warning disable CA1031 // Corrupt cache → treat as missing
        catch
#pragma warning restore CA1031
        {
            return null;
        }
    }

    private static async Task WriteCacheAsync(string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(CacheFile)!;
            Directory.CreateDirectory(dir);

            await using var fs = File.Create(CacheFile);
            await using var gz = new GZipStream(fs, CompressionLevel.Fastest);
            await using var writer = new StreamWriter(gz);
            await writer.WriteAsync(json).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);

            var meta = JsonSerializer.Serialize(new CacheMeta { FetchedAt = DateTime.UtcNow });
            await File.WriteAllTextAsync(MetaFile, meta).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Cache write failure is non-fatal
        catch
#pragma warning restore CA1031
        {
            // Best effort caching
        }
    }

    private static CacheMeta? ReadMeta()
    {
        if (!File.Exists(MetaFile))
            return null;

        try
        {
            var json = File.ReadAllText(MetaFile);
            return JsonSerializer.Deserialize<CacheMeta>(json);
        }
#pragma warning disable CA1031 // Corrupt meta → treat as missing
        catch
#pragma warning restore CA1031
        {
            return null;
        }
    }

    private sealed class CacheMeta
    {
        public DateTime FetchedAt { get; set; }
    }
}
