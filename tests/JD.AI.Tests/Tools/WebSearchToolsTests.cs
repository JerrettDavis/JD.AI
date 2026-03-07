using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Tests for <see cref="WebSearchTools"/>.
/// HTTP interactions are intercepted via a custom <see cref="HttpMessageHandler"/>
/// so no real network calls are made.
/// </summary>
public sealed class WebSearchToolsTests : IDisposable
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, HttpResponseMessage>? _responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        public void RespondWith(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responder = _ =>
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                return new HttpResponseMessage(statusCode) { Content = content };
            };
        }

        public void RespondWith(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public void ThrowNetworkError()
        {
            _responder = _ => throw new HttpRequestException("network error");
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (_responder is null)
                throw new InvalidOperationException("No response configured.");

            return Task.FromResult(_responder(request));
        }
    }

    /// <summary>Builds a minimal valid Bing Search API JSON response.</summary>
    private static string BuildBingResponse(params (string name, string snippet, string url)[] results)
    {
        var items = results.Select(r => new
        {
            name = r.name,
            snippet = r.snippet,
            url = r.url,
        });

        var payload = new
        {
            webPages = new
            {
                value = items,
            },
        };

        return JsonSerializer.Serialize(payload);
    }

    private readonly FakeHttpHandler _handler = new();

    /// <summary>Creates a <see cref="WebSearchTools"/> whose internal HttpClient uses <paramref name="handler"/>.</summary>
    private WebSearchTools CreateSut(string? apiKey = "test-api-key")
    {
        // Use reflection to inject a fake HttpClient because the constructor
        // always creates its own. The field is private but this is a test-only
        // workaround used only in this test class.
        var sut = new WebSearchTools(apiKey);

        var field = typeof(WebSearchTools).GetField(
            "_httpClient",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        // Dispose the real client first
        var realClient = (HttpClient)field.GetValue(sut)!;
        realClient.Dispose();

        field.SetValue(sut, new HttpClient(_handler));
        return sut;
    }

    public void Dispose() => _handler.Dispose();

    // ── No API key ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_WhenApiKeyIsNull_ReturnsNotConfiguredMessage()
    {
        using var sut = new WebSearchTools(bingApiKey: null);

        // Ensure the env var is absent for this test
        var savedEnv = Environment.GetEnvironmentVariable("BING_SEARCH_API_KEY");
        Environment.SetEnvironmentVariable("BING_SEARCH_API_KEY", null);
        try
        {
            // Re-create without env var set
            using var noKey = new WebSearchTools(bingApiKey: null);
            var result = await noKey.SearchAsync("test query");

            result.Should().Contain("not configured");
            result.Should().Contain("BING_SEARCH_API_KEY");
        }
        finally
        {
            Environment.SetEnvironmentVariable("BING_SEARCH_API_KEY", savedEnv);
        }
    }

    [Fact]
    public async Task SearchAsync_WhenApiKeyIsEmpty_ReturnsNotConfiguredMessage()
    {
        using var sut = new WebSearchTools(bingApiKey: "");

        var result = await sut.SearchAsync("test");

        result.Should().Contain("not configured");
    }

    [Fact]
    public async Task SearchAsync_WhenApiKeyIsWhitespace_ReturnsNotConfiguredMessage()
    {
        using var sut = new WebSearchTools(bingApiKey: "   ");

        var result = await sut.SearchAsync("test");

        result.Should().Contain("not configured");
    }

    // ── Successful searches ───────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ReturnsFormattedResults()
    {
        _handler.RespondWith(BuildBingResponse(
            ("Result One", "Snippet for result one.", "https://example.com/1"),
            ("Result Two", "Snippet for result two.", "https://example.com/2")));

        using var sut = CreateSut();
        var result = await sut.SearchAsync("test query");

        result.Should().Contain("Result One");
        result.Should().Contain("Snippet for result one.");
        result.Should().Contain("https://example.com/1");
        result.Should().Contain("Result Two");
        result.Should().Contain("---"); // separator between results
    }

    [Fact]
    public async Task SearchAsync_IncludesApiKeyHeader()
    {
        _handler.RespondWith(BuildBingResponse(("Title", "Snippet", "https://x.com")));

        using var sut = CreateSut("my-secret-key");
        await sut.SearchAsync("anything");

        _handler.LastRequest.Should().NotBeNull();
        _handler.LastRequest!.Headers.Should().ContainKey("Ocp-Apim-Subscription-Key");
        _handler.LastRequest.Headers.GetValues("Ocp-Apim-Subscription-Key").Single()
            .Should().Be("my-secret-key");
    }

    [Fact]
    public async Task SearchAsync_UrlContainsEncodedQuery()
    {
        _handler.RespondWith(BuildBingResponse(("T", "S", "https://x.com")));

        using var sut = CreateSut();
        await sut.SearchAsync("C# programming");

        var requestUrl = _handler.LastRequest!.RequestUri!.ToString();
        requestUrl.Should().Contain("C%23"); // '#' is encoded as %23
    }

    [Fact]
    public async Task SearchAsync_DefaultCountIsIncludedInUrl()
    {
        _handler.RespondWith(BuildBingResponse(("T", "S", "https://x.com")));

        using var sut = CreateSut();
        await sut.SearchAsync("query"); // default count = 5

        var url = _handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("count=5");
    }

    [Fact]
    public async Task SearchAsync_CustomCount_IsClampedToMax10()
    {
        _handler.RespondWith(BuildBingResponse(("T", "S", "https://x.com")));

        using var sut = CreateSut();
        await sut.SearchAsync("query", count: 99); // should clamp to 10

        var url = _handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("count=10");
    }

    [Fact]
    public async Task SearchAsync_CountZero_IsClamped_ToMin1()
    {
        _handler.RespondWith(BuildBingResponse(("T", "S", "https://x.com")));

        using var sut = CreateSut();
        await sut.SearchAsync("query", count: 0);

        var url = _handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("count=1");
    }

    [Fact]
    public async Task SearchAsync_NegativeCount_IsClamped_ToMin1()
    {
        _handler.RespondWith(BuildBingResponse(("T", "S", "https://x.com")));

        using var sut = CreateSut();
        await sut.SearchAsync("query", count: -5);

        var url = _handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("count=1");
    }

    // ── Empty / missing results ───────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_WhenNoWebPagesProperty_ReturnsNoResults()
    {
        _handler.RespondWith("""{"status":"OK"}""");

        using var sut = CreateSut();
        var result = await sut.SearchAsync("empty");

        result.Should().Be("No results found.");
    }

    [Fact]
    public async Task SearchAsync_WhenValueArrayIsEmpty_ReturnsNoResults()
    {
        _handler.RespondWith("""{"webPages":{"value":[]}}""");

        using var sut = CreateSut();
        var result = await sut.SearchAsync("empty");

        result.Should().Be("No results found.");
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_OnHttpRequestException_ReturnsSearchFailedMessage()
    {
        _handler.ThrowNetworkError();

        using var sut = CreateSut();
        var result = await sut.SearchAsync("query");

        result.Should().StartWith("Search failed:");
        result.Should().Contain("network error");
    }

    [Fact]
    public async Task SearchAsync_OnNon2xxResponse_ReturnsSearchFailedMessage()
    {
        _handler.RespondWith("{}", HttpStatusCode.ServiceUnavailable);

        using var sut = CreateSut();
        var result = await sut.SearchAsync("query");

        result.Should().StartWith("Search failed:");
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = new WebSearchTools("key");
        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var sut = new WebSearchTools("key");
        sut.Dispose();

        // HttpClient.Dispose() is idempotent
        var act = () => sut.Dispose();
        act.Should().NotThrow();
    }

    // ── Environment variable fallback ─────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_WhenEnvVarIsSet_UsesItAsApiKey()
    {
        var savedEnv = Environment.GetEnvironmentVariable("BING_SEARCH_API_KEY");
        Environment.SetEnvironmentVariable("BING_SEARCH_API_KEY", "env-key");
        try
        {
            using var sut = new WebSearchTools(bingApiKey: null);
            var field = typeof(WebSearchTools).GetField(
                "_httpClient",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var realClient = (HttpClient)field.GetValue(sut)!;
            realClient.Dispose();
            field.SetValue(sut, new HttpClient(_handler));

            _handler.RespondWith(BuildBingResponse(("T", "S", "https://x.com")));
            var result = await sut.SearchAsync("hello");

            _handler.LastRequest!.Headers.GetValues("Ocp-Apim-Subscription-Key").Single()
                .Should().Be("env-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable("BING_SEARCH_API_KEY", savedEnv);
        }
    }

    // ── Result formatting ─────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_SingleResult_DoesNotContainSeparator()
    {
        _handler.RespondWith(BuildBingResponse(
            ("Only Result", "Only snippet.", "https://only.com")));

        using var sut = CreateSut();
        var result = await sut.SearchAsync("solo");

        result.Should().NotContain("---");
        result.Should().Contain("Only Result");
    }

    [Fact]
    public async Task SearchAsync_MultipleResults_SeparatedByDashes()
    {
        _handler.RespondWith(BuildBingResponse(
            ("A", "sa", "https://a.com"),
            ("B", "sb", "https://b.com"),
            ("C", "sc", "https://c.com")));

        using var sut = CreateSut();
        var result = await sut.SearchAsync("multi");

        // Two separators for three results
        result.Split("---").Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_ResultsBoldTitle()
    {
        _handler.RespondWith(BuildBingResponse(("My Title", "my snippet", "https://t.com")));

        using var sut = CreateSut();
        var result = await sut.SearchAsync("anything");

        result.Should().Contain("**My Title**");
    }
}
