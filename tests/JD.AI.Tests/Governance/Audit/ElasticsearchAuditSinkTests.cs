using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Governance.Audit;

namespace JD.AI.Tests.Governance.Audit;

public sealed class ElasticsearchAuditSinkTests
{
    private sealed class CapturingMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public bool ThrowOnSend { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
                throw new HttpRequestException("network error");

            LastRequest = request;
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(ResponseStatusCode);
        }
    }

    [Fact]
    public void Name_ReturnsElasticsearch()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://localhost:9200", "jdai-{yyyy.MM}");

        sink.Name.Should().Be("elasticsearch");
    }

    [Fact]
    public async Task WriteAsync_PostsToCorrectUrl()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://localhost:9200", "jdai-{yyyy.MM}");

        var evt = new AuditEvent
        {
            Action = "test",
            Timestamp = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
        };

        await sink.WriteAsync(evt);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be("http://localhost:9200/jdai-2025.03/_doc");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task WriteAsync_IndexDateTemplate_Replaced()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://es:9200", "audit-logs-{yyyy.MM}");

        var evt = new AuditEvent
        {
            Action = "test",
            Timestamp = new DateTimeOffset(2025, 11, 15, 0, 0, 0, TimeSpan.Zero),
        };

        await sink.WriteAsync(evt);

        handler.LastRequest!.RequestUri!.ToString().Should().Be("http://es:9200/audit-logs-2025.11/_doc");
    }

    [Fact]
    public async Task WriteAsync_BodyIsValidJson()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://localhost:9200", "jdai-{yyyy.MM}");

        var evt = new AuditEvent { Action = "es-test", UserId = "user-1" };

        await sink.WriteAsync(evt);

        handler.LastRequestBody.Should().NotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(handler.LastRequestBody!);
        doc.RootElement.GetProperty("action").GetString().Should().Be("es-test");
        doc.RootElement.GetProperty("userId").GetString().Should().Be("user-1");
    }

    [Fact]
    public async Task WriteAsync_WithToken_SetsAuthorizationHeader()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://localhost:9200", "jdai-{yyyy.MM}", token: "my-secret-token");

        await sink.WriteAsync(new AuditEvent { Action = "auth-test" });

        handler.LastRequest!.Headers.Authorization.Should().NotBeNull();
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("my-secret-token");
    }

    [Fact]
    public async Task WriteAsync_HttpFailure_DoesNotThrow()
    {
        var handler = new CapturingMessageHandler { ResponseStatusCode = HttpStatusCode.InternalServerError };
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://localhost:9200", "jdai-{yyyy.MM}");

        var act = async () => await sink.WriteAsync(new AuditEvent { Action = "fail" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WriteAsync_NetworkException_DoesNotThrow()
    {
        var handler = new CapturingMessageHandler { ThrowOnSend = true };
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://localhost:9200", "jdai-{yyyy.MM}");

        var act = async () => await sink.WriteAsync(new AuditEvent { Action = "network-fail" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FlushAsync_CompletesSuccessfully()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://localhost:9200", "jdai-{yyyy.MM}");

        var act = async () => await sink.FlushAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WriteAsync_ContentTypeIsApplicationJson()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new ElasticsearchAuditSink(httpClient, "http://localhost:9200", "jdai-{yyyy.MM}");

        await sink.WriteAsync(new AuditEvent { Action = "content-type-test" });

        handler.LastRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }
}
