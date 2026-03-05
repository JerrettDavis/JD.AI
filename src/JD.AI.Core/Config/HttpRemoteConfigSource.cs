using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Config;

/// <summary>
/// Fetches configuration from an HTTP endpoint. Compatible with Consul KV,
/// Azure App Configuration REST API, Spring Cloud Config, or any endpoint
/// that returns YAML/JSON configuration content.
/// </summary>
public sealed class HttpRemoteConfigSource : IRemoteConfigSource, IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly ILogger? _logger;
    private readonly bool _ownsClient;
    private string? _lastETag;

    public string Name => "http";

    /// <param name="endpoint">URL to fetch configuration from.</param>
    /// <param name="httpClient">Optional pre-configured HttpClient (e.g. with auth headers).</param>
    /// <param name="logger">Optional logger.</param>
    public HttpRemoteConfigSource(
        Uri endpoint,
        HttpClient? httpClient = null,
        ILogger? logger = null)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _logger = logger;

        if (httpClient is not null)
        {
            _http = httpClient;
            _ownsClient = false;
        }
        else
        {
            _http = new HttpClient();
            _ownsClient = true;
        }
    }

    public async Task<RemoteConfigResult?> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint);

            if (_lastETag is not null)
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{_lastETag}\""));

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                return null; // No change since last fetch

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var etag = response.Headers.ETag?.Tag?.Trim('"');
            _lastETag = etag;

            var contentType = response.Content.Headers.ContentType?.MediaType switch
            {
                "application/x-yaml" or "text/yaml" => "yaml",
                "application/json" or "text/json" => "json",
                _ => null,
            };

            return new RemoteConfigResult
            {
                Content = content,
                Version = etag ?? response.Content.Headers.LastModified?.ToString("O"),
                ContentType = contentType,
                LastModified = response.Content.Headers.LastModified,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger?.LogWarning(ex, "Failed to fetch remote config from {Endpoint}", _endpoint);
            return null;
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
