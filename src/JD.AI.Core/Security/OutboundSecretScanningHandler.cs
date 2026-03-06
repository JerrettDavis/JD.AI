using System.Net.Http.Headers;
using JD.AI.Core.Governance;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Security;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that scans outbound request bodies and headers for secrets
/// before allowing the request to be sent. Uses a <see cref="DataRedactor"/> to detect matches.
/// </summary>
/// <remarks>
/// Wire this into any <see cref="HttpClient"/> that may carry user-controlled content or
/// provider responses — for example the client used by tool implementations that call external APIs.
/// </remarks>
public sealed class OutboundSecretScanningHandler : DelegatingHandler
{
    private readonly DataRedactor _redactor;
    private readonly ILogger<OutboundSecretScanningHandler> _logger;
    private readonly bool _blockOnDetection;

    /// <summary>
    /// Initializes the handler.
    /// </summary>
    /// <param name="redactor">
    /// A <see cref="DataRedactor"/> configured with secret detection patterns.
    /// Use <see cref="DataRedactor"/> constructed from <see cref="SecretPatternLibrary.All"/> for broad coverage.
    /// </param>
    /// <param name="logger">Logger for security alerts.</param>
    /// <param name="blockOnDetection">
    /// When <c>true</c> (default), throws <see cref="SecurityException"/> if a secret is detected.
    /// When <c>false</c>, logs a warning and allows the request through (audit-only mode).
    /// </param>
    public OutboundSecretScanningHandler(
        DataRedactor redactor,
        ILogger<OutboundSecretScanningHandler> logger,
        bool blockOnDetection = true)
    {
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _blockOnDetection = blockOnDetection;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(body))
            {
                CheckForSecrets(body, request.RequestUri, "request body");
            }
        }

        CheckAuthorizationHeader(request.Headers.Authorization, request.RequestUri);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private void CheckForSecrets(string content, Uri? uri, string location)
    {
        var redacted = _redactor.Redact(content);
        if (string.Equals(redacted, content, StringComparison.Ordinal))
        {
            return;
        }

        // Content changed — secrets were found
        var message =
            $"Outbound secret detected in {location} for request to {uri?.Host ?? "unknown host"}. " +
            "The request has been blocked.";

        _logger.LogWarning(
            "Outbound secret scanning blocked a request to {Host} — secrets found in {Location}",
            uri?.Host ?? "unknown",
            location);

        if (_blockOnDetection)
        {
            throw new SecurityException(message);
        }
    }

    private void CheckAuthorizationHeader(AuthenticationHeaderValue? auth, Uri? uri)
    {
        if (auth?.Parameter is null)
        {
            return;
        }

        // Only scan Bearer tokens that look like structured secrets (not short OAuth flows)
        if (auth.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) &&
            auth.Parameter.Length > 20)
        {
            CheckForSecrets(auth.Parameter, uri, "Authorization header");
        }
    }
}
