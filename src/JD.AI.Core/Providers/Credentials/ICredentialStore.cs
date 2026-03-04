namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Provides secure storage for provider API keys and secrets.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Gets a stored credential value.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Stores a credential value.</summary>
    Task SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Removes a stored credential.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Lists all keys matching a prefix.</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default);

    /// <summary>Whether this store is available on the current platform.</summary>
    bool IsAvailable { get; }

    /// <summary>Human-readable name of the backing store.</summary>
    string StoreName { get; }
}
