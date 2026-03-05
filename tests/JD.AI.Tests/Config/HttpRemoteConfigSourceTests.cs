using System.Net;
using FluentAssertions;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Config;

public sealed class HttpRemoteConfigSourceTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly HttpRemoteConfigSource _source;

    public HttpRemoteConfigSourceTests()
    {
        _httpClient = new HttpClient(_handler);
        _source = new HttpRemoteConfigSource(
            new Uri("https://config.example.com/api/v1/config"),
            _httpClient);
    }

    public void Dispose()
    {
        _source.Dispose();
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task FetchAsync_SuccessfulResponse_ReturnsContent()
    {
        _handler.SetResponse("apiVersion: jdai/v1", HttpStatusCode.OK);

        var result = await _source.FetchAsync();

        result.Should().NotBeNull();
        result!.Content.Should().Be("apiVersion: jdai/v1");
    }

    [Fact]
    public async Task FetchAsync_WithETag_SetsVersion()
    {
        _handler.SetResponse("content", HttpStatusCode.OK, etag: "abc123");

        var result = await _source.FetchAsync();

        result.Should().NotBeNull();
        result!.Version.Should().Be("abc123");
    }

    [Fact]
    public async Task FetchAsync_NotModified_ReturnsNull()
    {
        _handler.SetResponse("content", HttpStatusCode.OK, etag: "v1");
        await _source.FetchAsync(); // First fetch stores ETag

        _handler.SetResponse("", HttpStatusCode.NotModified);
        var result = await _source.FetchAsync(); // Second fetch with If-None-Match

        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_ServerError_ReturnsNull()
    {
        _handler.SetResponse("", HttpStatusCode.InternalServerError);

        var result = await _source.FetchAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_YamlContentType_DetectsYaml()
    {
        _handler.SetResponse("key: value", HttpStatusCode.OK, contentType: "application/x-yaml");

        var result = await _source.FetchAsync();

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("yaml");
    }

    [Fact]
    public async Task FetchAsync_JsonContentType_DetectsJson()
    {
        _handler.SetResponse("{}", HttpStatusCode.OK, contentType: "application/json");

        var result = await _source.FetchAsync();

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("json");
    }

    [Fact]
    public void Name_ReturnsHttp()
    {
        _source.Name.Should().Be("http");
    }

    /// <summary>Simple mock HTTP handler for testing.</summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private string _content = "";
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string? _etag;
        private string? _contentType;

        public void SetResponse(string content, HttpStatusCode statusCode,
            string? etag = null, string? contentType = null)
        {
            _content = content;
            _statusCode = statusCode;
            _etag = etag;
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content),
            };

            if (_etag is not null)
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue($"\"{_etag}\"");

            if (_contentType is not null)
                response.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);

            return Task.FromResult(response);
        }
    }
}
