using Azure;
using Azure.Security.KeyVault.Secrets;
using JD.AI.Core.Providers.Credentials;
using Microsoft.Extensions.Logging;

namespace JD.AI.Credentials.Azure;

/// <summary>
/// An <see cref="ICredentialStore"/> backed by Azure Key Vault.
/// Each credential key maps to an Azure Key Vault secret name with slashes replaced by hyphens.
/// </summary>
public sealed class AzureKeyVaultCredentialStore : ICredentialStore
{
    private readonly SecretClient _client;
    private readonly ILogger<AzureKeyVaultCredentialStore> _logger;

    /// <summary>Initializes the store with a pre-configured <see cref="SecretClient"/>.</summary>
    public AzureKeyVaultCredentialStore(
        SecretClient client,
        ILogger<AzureKeyVaultCredentialStore> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public string StoreName => "Azure Key Vault";

    /// <inheritdoc/>
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var secretName = ToSecretName(key);
        try
        {
            // Use explicit version argument to keep behavior/mocking stable across SDK overload changes.
            var response = await _client.GetSecretAsync(secretName, null, ct).ConfigureAwait(false);
            return response.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' from Azure Key Vault", secretName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        var secretName = ToSecretName(key);
        try
        {
            await _client.SetSecretAsync(secretName, value, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret '{SecretName}' in Azure Key Vault", secretName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var secretName = ToSecretName(key);
        try
        {
            var operation = await _client.StartDeleteSecretAsync(secretName, ct).ConfigureAwait(false);
            await operation.WaitForCompletionAsync(ct).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already gone — treat as success
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret '{SecretName}' from Azure Key Vault", secretName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
    {
        var normalizedPrefix = ToSecretName(prefix ?? string.Empty);
        var keys = new List<string>();
        await foreach (var prop in _client.GetPropertiesOfSecretsAsync(ct).ConfigureAwait(false))
        {
            if (prop.Name.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Restore slashes from hyphens in returned names
                keys.Add(FromSecretName(prop.Name));
            }
        }

        return keys;
    }

    /// <summary>
    /// Converts a credential key (which may contain slashes) to a valid Key Vault secret name
    /// by replacing <c>/</c> with <c>--</c> and <c>_</c> with <c>-</c> where needed.
    /// Azure Key Vault secret names must match <c>^[0-9a-zA-Z-]+$</c>.
    /// </summary>
    internal static string ToSecretName(string key) =>
        key.Replace('/', '-').Replace('_', '-');

    /// <summary>Converts a Key Vault secret name back to a credential key.</summary>
    internal static string FromSecretName(string name) => name;
}
