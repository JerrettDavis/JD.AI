using System.Text;
using FluentAssertions;
using JD.AI.Core.Governance.Audit;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class AuditSinksSteps : IDisposable
{
    private readonly ScenarioContext _context;
    private FileAuditSink? _fileSink;

    public AuditSinksSteps(ScenarioContext context) => _context = context;

    [Given(@"a file audit sink with a temporary directory")]
    public void GivenAFileAuditSinkWithATempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jdai-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _fileSink = new FileAuditSink(dir);
        _context.Set(_fileSink, "FileSink");
        _context.Set(dir, "AuditDir");
    }

    [Given(@"a webhook audit sink with a mock HTTP handler")]
    public void GivenAWebhookAuditSinkWithMockHandler()
    {
        var handler = new MockHttpHandler();
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");
        _context.Set(sink, "WebhookSink");
        _context.Set(handler, "MockHandler");
    }

    [Given(@"a webhook audit sink that returns HTTP (\d+)")]
    public void GivenAWebhookAuditSinkThatReturnsError(int statusCode)
    {
        var handler = new MockHttpHandler((System.Net.HttpStatusCode)statusCode);
        var httpClient = new HttpClient(handler);
        var sink = new WebhookAuditSink(httpClient, "https://example.com/webhook");
        _context.Set(sink, "WebhookSink");
        _context.Set(handler, "MockHandler");
    }

    [When(@"I write an audit event with action ""(.*)""")]
    public async Task WhenIWriteAnAuditEventWithAction(string action)
    {
        if (_context.TryGetValue("FileSink", out FileAuditSink? fileSink))
        {
            await fileSink.WriteAsync(new AuditEvent { Action = action });
        }
        else if (_context.TryGetValue("WebhookSink", out WebhookAuditSink? webhookSink))
        {
            await webhookSink.WriteAsync(new AuditEvent { Action = action });
        }
    }

    [When(@"I write (\d+) audit events")]
    public async Task WhenIWriteMultipleAuditEvents(int count)
    {
        var sink = _context.Get<FileAuditSink>("FileSink");
        for (var i = 0; i < count; i++)
        {
            await sink.WriteAsync(new AuditEvent { Action = $"event-{i}" });
        }
    }

    [Then(@"a JSONL audit file should exist for today")]
    public void ThenAJsonlAuditFileShouldExistForToday()
    {
        var dir = _context.Get<string>("AuditDir");
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var expectedFile = Path.Combine(dir, $"audit-{today}.jsonl");
        File.Exists(expectedFile).Should().BeTrue($"audit file for {today} should exist");
    }

    [Then(@"the audit file should contain ""(.*)""")]
    public void ThenTheAuditFileShouldContain(string expected)
    {
        var dir = _context.Get<string>("AuditDir");
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var filePath = Path.Combine(dir, $"audit-{today}.jsonl");
        var content = File.ReadAllText(filePath);
        content.Should().Contain(expected);
    }

    [Then(@"the audit file should contain (\d+) lines")]
    public void ThenTheAuditFileShouldContainLines(int count)
    {
        var dir = _context.Get<string>("AuditDir");
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var filePath = Path.Combine(dir, $"audit-{today}.jsonl");
        var lines = File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        lines.Should().HaveCount(count);
    }

    [Then(@"the mock HTTP handler should have received (\d+) POST request")]
    public void ThenTheMockHandlerShouldHaveReceivedPostRequests(int count)
    {
        var handler = _context.Get<MockHttpHandler>("MockHandler");
        handler.RequestCount.Should().Be(count);
    }

    [Then(@"the POST body should contain ""(.*)""")]
    public void ThenThePostBodyShouldContain(string expected)
    {
        var handler = _context.Get<MockHttpHandler>("MockHandler");
        handler.LastBody.Should().Contain(expected);
    }

    [Then(@"no exception should be thrown")]
    public void ThenNoExceptionShouldBeThrown()
    {
        // If we got here, no exception was thrown. This is a validation step.
    }

    public void Dispose()
    {
        _fileSink?.Dispose();
        if (_context.TryGetValue("AuditDir", out string? dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly System.Net.HttpStatusCode _statusCode;
        public int RequestCount { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        public MockHttpHandler(System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
        {
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.Content != null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_statusCode);
        }
    }
}
