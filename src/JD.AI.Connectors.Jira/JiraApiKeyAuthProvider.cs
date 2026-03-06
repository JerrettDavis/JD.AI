using JD.AI.Connectors.Sdk;

namespace JD.AI.Connectors.Jira;

/// <summary>
/// Options for connecting to a Jira Cloud or Server instance.
/// </summary>
public sealed class JiraConnectorOptions
{
    /// <summary>Base URL of the Jira instance, e.g. <c>https://myorg.atlassian.net</c>.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Jira user email address (used with API token authentication).</summary>
    public string? Email { get; set; }

    /// <summary>Jira API token. Generate at https://id.atlassian.com/manage-profile/security/api-tokens.</summary>
    public string? ApiToken { get; set; }
}

/// <summary>
/// Authenticates Jira API requests using Basic auth with email + API token.
/// </summary>
public sealed class JiraApiKeyAuthProvider : IConnectorAuthProvider
{
    private readonly JiraConnectorOptions _options;

    /// <summary>Initializes the auth provider with the configured options.</summary>
    public JiraApiKeyAuthProvider(JiraConnectorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Scheme => "Basic";

    /// <inheritdoc/>
    public Task<string?> GetAuthorizationHeaderAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            return Task.FromResult<string?>(null);
        }

        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{_options.Email}:{_options.ApiToken}"));
        return Task.FromResult<string?>($"Basic {credentials}");
    }

    /// <inheritdoc/>
    public Task<bool> IsAuthenticatedAsync(CancellationToken ct = default) =>
        Task.FromResult(
            !string.IsNullOrWhiteSpace(_options.Email) &&
            !string.IsNullOrWhiteSpace(_options.ApiToken));
}
