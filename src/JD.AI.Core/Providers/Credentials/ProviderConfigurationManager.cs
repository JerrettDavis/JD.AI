using Microsoft.Extensions.Configuration;

namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Resolves provider credentials from multiple sources:
/// 1. ICredentialStore (secure storage)
/// 2. IConfiguration (appsettings, user secrets)
/// 3. Well-known environment variables
/// </summary>
public sealed class ProviderConfigurationManager
{
    private readonly ICredentialStore _store;
    private readonly IConfiguration? _configuration;

    private static readonly Dictionary<string, Dictionary<string, string>> WellKnownEnvVars = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"] = new(StringComparer.OrdinalIgnoreCase) { ["apikey"] = "OPENAI_API_KEY" },
        ["azure-openai"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["apikey"] = "AZURE_OPENAI_API_KEY",
            ["endpoint"] = "AZURE_OPENAI_ENDPOINT",
        },
        ["anthropic"] = new(StringComparer.OrdinalIgnoreCase) { ["apikey"] = "ANTHROPIC_API_KEY" },
        ["google-gemini"] = new(StringComparer.OrdinalIgnoreCase) { ["apikey"] = "GOOGLE_AI_API_KEY" },
        ["mistral"] = new(StringComparer.OrdinalIgnoreCase) { ["apikey"] = "MISTRAL_API_KEY" },
        ["bedrock"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["accesskey"] = "AWS_ACCESS_KEY_ID",
            ["secretkey"] = "AWS_SECRET_ACCESS_KEY",
            ["region"] = "AWS_REGION",
        },
        ["huggingface"] = new(StringComparer.OrdinalIgnoreCase) { ["apikey"] = "HUGGINGFACE_API_KEY" },
    };

    public ProviderConfigurationManager(
        ICredentialStore store,
        IConfiguration? configuration = null)
    {
        _store = store;
        _configuration = configuration;
    }

    public ICredentialStore Store => _store;

    /// <summary>
    /// Resolves a credential for a provider field via the resolution chain.
    /// </summary>
    public async Task<string?> GetCredentialAsync(
        string provider, string field, CancellationToken ct = default)
    {
        // 1. Secure credential store
        var storeKey = $"jdai:provider:{provider}:{field}";
        var value = await _store.GetAsync(storeKey, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(value))
            return value;

        // 2. IConfiguration (e.g. appsettings.json: Providers:OpenAI:ApiKey)
        if (_configuration != null)
        {
            var configKey = $"Providers:{provider}:{field}";
            value = _configuration[configKey];
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        // 3. Well-known environment variables
        if (WellKnownEnvVars.TryGetValue(provider, out var envMap) &&
            envMap.TryGetValue(field, out var envVar))
        {
            value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    /// <summary>
    /// Stores a credential in the secure store.
    /// </summary>
    public Task SetCredentialAsync(
        string provider, string field, string value, CancellationToken ct = default)
    {
        var storeKey = $"jdai:provider:{provider}:{field}";
        return _store.SetAsync(storeKey, value, ct);
    }

    /// <summary>
    /// Removes a credential from the secure store.
    /// </summary>
    public Task RemoveCredentialAsync(
        string provider, string field, CancellationToken ct = default)
    {
        var storeKey = $"jdai:provider:{provider}:{field}";
        return _store.RemoveAsync(storeKey, ct);
    }

    /// <summary>
    /// Removes all credentials for a provider.
    /// </summary>
    public async Task RemoveProviderAsync(
        string provider, CancellationToken ct = default)
    {
        var prefix = $"jdai:provider:{provider}:";
        var keys = await _store.ListKeysAsync(prefix, ct).ConfigureAwait(false);
        foreach (var key in keys)
        {
            await _store.RemoveAsync(key, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lists all configured provider names (those with at least one credential stored).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListConfiguredProvidersAsync(
        CancellationToken ct = default)
    {
        var keys = await _store.ListKeysAsync("jdai:provider:", ct).ConfigureAwait(false);
        return keys
            .Select(k =>
            {
                // key format: jdai:provider:{name}:{field}
                var parts = k.Split(':');
                return parts.Length >= 3 ? parts[2] : null;
            })
            .Where(n => n != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }
}
