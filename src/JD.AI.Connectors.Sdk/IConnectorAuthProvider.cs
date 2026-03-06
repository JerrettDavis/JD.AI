namespace JD.AI.Connectors.Sdk;

/// <summary>
/// Provides authentication credentials or tokens for a connector.
/// Implement this interface to support API key, OAuth, bearer token, or other auth schemes.
/// </summary>
public interface IConnectorAuthProvider
{
    /// <summary>The authentication scheme name (e.g. "ApiKey", "OAuth2", "Bearer").</summary>
    string Scheme { get; }

    /// <summary>
    /// Retrieves the authorization header value for an outbound request.
    /// Return <c>null</c> if no authorization header should be added.
    /// </summary>
    Task<string?> GetAuthorizationHeaderAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether credentials are currently available and valid.
    /// </summary>
    Task<bool> IsAuthenticatedAsync(CancellationToken ct = default);
}
