using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using JD.AI.Core.Providers.Credentials;
using Microsoft.Extensions.Logging;

namespace JD.AI.Credentials.Aws;

/// <summary>
/// An <see cref="ICredentialStore"/> backed by AWS Secrets Manager.
/// Each credential key maps to an AWS secret name (slashes are valid in secret names).
/// </summary>
public sealed class AwsSecretsManagerCredentialStore : ICredentialStore
{
    private readonly IAmazonSecretsManager _client;
    private readonly ILogger<AwsSecretsManagerCredentialStore> _logger;
    private readonly string _prefix;

    /// <summary>
    /// Initializes the store.
    /// </summary>
    /// <param name="client">The AWS Secrets Manager client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="prefix">Optional prefix prepended to all secret names (e.g. <c>"jdai/"</c>).</param>
    public AwsSecretsManagerCredentialStore(
        IAmazonSecretsManager client,
        ILogger<AwsSecretsManagerCredentialStore> logger,
        string prefix = "")
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _prefix = prefix ?? string.Empty;
    }

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public string StoreName => "AWS Secrets Manager";

    /// <inheritdoc/>
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var secretId = ToSecretId(key);
        try
        {
            var response = await _client.GetSecretValueAsync(
                new GetSecretValueRequest { SecretId = secretId }, ct).ConfigureAwait(false);
            return response.SecretString;
        }
        catch (ResourceNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretId}' from AWS Secrets Manager", secretId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        var secretId = ToSecretId(key);
        try
        {
            // Try update first, create if not found
            try
            {
                await _client.PutSecretValueAsync(
                    new PutSecretValueRequest { SecretId = secretId, SecretString = value }, ct).ConfigureAwait(false);
            }
            catch (ResourceNotFoundException)
            {
                await _client.CreateSecretAsync(
                    new CreateSecretRequest { Name = secretId, SecretString = value }, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret '{SecretId}' in AWS Secrets Manager", secretId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var secretId = ToSecretId(key);
        try
        {
            await _client.DeleteSecretAsync(
                new DeleteSecretRequest
                {
                    SecretId = secretId,
                    ForceDeleteWithoutRecovery = false,
                },
                ct).ConfigureAwait(false);
        }
        catch (ResourceNotFoundException)
        {
            // Already gone — treat as success
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret '{SecretId}' from AWS Secrets Manager", secretId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
    {
        var combined = ToSecretId(prefix ?? string.Empty);
        var results = new List<string>();
        string? nextToken = null;

        do
        {
            var response = await _client.ListSecretsAsync(
                new ListSecretsRequest
                {
                    Filters = [new Filter { Key = FilterNameStringType.Name, Values = [combined] }],
                    NextToken = nextToken,
                },
                ct).ConfigureAwait(false);

            foreach (var entry in response.SecretList)
            {
                results.Add(FromSecretId(entry.Name));
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return results;
    }

    private string ToSecretId(string key) =>
        string.IsNullOrEmpty(_prefix) ? key : $"{_prefix.TrimEnd('/')}/{key.TrimStart('/')}";

    private string FromSecretId(string secretId) =>
        string.IsNullOrEmpty(_prefix) ? secretId : secretId[Math.Min(_prefix.Length, secretId.Length)..];
}
