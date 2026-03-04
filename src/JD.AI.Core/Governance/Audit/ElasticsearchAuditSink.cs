using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Core.Governance.Audit;

/// <summary>
/// POSTs audit events as JSON documents to an Elasticsearch index via
/// <c>{endpoint}/{index}/_doc</c>.  The index name supports date templating:
/// <c>{index}</c> with <c>{yyyy.MM}</c> substituted with the event's month.
/// </summary>
public sealed class ElasticsearchAuditSink : IAuditSink
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _indexTemplate;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public ElasticsearchAuditSink(HttpClient httpClient, string endpoint, string indexTemplate, string? token = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(indexTemplate);

        _httpClient = httpClient;
        _endpoint = endpoint.TrimEnd('/');
        _indexTemplate = indexTemplate;

        if (!string.IsNullOrWhiteSpace(token))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    public string Name => "elasticsearch";

    /// <inheritdoc/>
    public async Task WriteAsync(AuditEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        try
        {
            var index = ResolveIndex(evt.Timestamp);
            var url = $"{_endpoint}/{index}/_doc";
            var json = JsonSerializer.Serialize(evt, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(new Uri(url), content, ct).ConfigureAwait(false);
            // Non-success responses are gracefully ignored to avoid breaking the app.
            _ = response;
        }
        catch (Exception)
        {
            // Graceful failure — audit must not break the application.
        }
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;

    private string ResolveIndex(DateTimeOffset timestamp) =>
        _indexTemplate.Replace("{yyyy.MM}", timestamp.ToString("yyyy.MM"), StringComparison.Ordinal);
}
