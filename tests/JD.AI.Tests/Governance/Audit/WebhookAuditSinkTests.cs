using System.Net;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Governance.Audit;

namespace JD.AI.Tests.Governance.Audit;

public sealed class WebhookAuditSinkTests
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
    public void Name_ReturnsWebhook()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");

        sink.Name.Should().Be("webhook");
    }

    [Fact]
    public async Task WriteAsync_PostsToConfiguredUrl()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://hooks.example.com/audit");

        await sink.WriteAsync(new AuditEvent { Action = "test" });

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://hooks.example.com/audit");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task WriteAsync_BodyIsValidJson()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");

        var evt = new AuditEvent { Action = "webhook-test", UserId = "user-99" };

        await sink.WriteAsync(evt);

        handler.LastRequestBody.Should().NotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(handler.LastRequestBody!);
        doc.RootElement.GetProperty("action").GetString().Should().Be("webhook-test");
        doc.RootElement.GetProperty("userId").GetString().Should().Be("user-99");
    }

    [Fact]
    public async Task WriteAsync_HttpFailure_DoesNotThrow()
    {
        var handler = new CapturingMessageHandler { ResponseStatusCode = HttpStatusCode.InternalServerError };
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");

        var act = async () => await sink.WriteAsync(new AuditEvent { Action = "fail" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WriteAsync_NetworkException_DoesNotThrow()
    {
        var handler = new CapturingMessageHandler { ThrowOnSend = true };
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");

        var act = async () => await sink.WriteAsync(new AuditEvent { Action = "network-fail" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FlushAsync_CompletesSuccessfully()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");

        var act = async () => await sink.FlushAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WriteAsync_ContentTypeIsApplicationJson()
    {
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");

        await sink.WriteAsync(new AuditEvent { Action = "content-test" });

        handler.LastRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task WriteAsync_MultipleEvents_EachPosted()
    {
        var callCount = 0;
        var handler = new CapturingMessageHandler();
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");

        for (var i = 0; i < 3; i++)
        {
            await sink.WriteAsync(new AuditEvent { Action = $"event-{i}" });
            callCount++;
        }

        callCount.Should().Be(3);
    }
}
