using System.Text;
using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Governance.Audit;

/// <summary>
/// POSTs audit events as JSON to a configured webhook URL.
/// Failures are swallowed so they never break the application.
/// </summary>
public sealed class WebhookAuditSink : IAuditSink
{
    private readonly HttpClient _httpClient;
    private readonly string _url;

    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Compact;

    public WebhookAuditSink(HttpClient httpClient, string url)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(url);

        _httpClient = httpClient;
        _url = url;
    }

    public string Name => "webhook";

    /// <inheritdoc/>
    public async Task WriteAsync(AuditEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        try
        {
            var json = JsonSerializer.Serialize(evt, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(new Uri(_url), content, ct).ConfigureAwait(false);
            // Non-success responses are gracefully ignored.
            _ = response;
        }
        catch (Exception)
        {
            // Graceful failure — audit must not break the application.
        }
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
}
