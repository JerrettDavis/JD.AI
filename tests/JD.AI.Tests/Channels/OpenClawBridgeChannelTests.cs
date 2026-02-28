using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JD.AI.Channels.OpenClaw;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Channels;

public sealed class OpenClawBridgeChannelTests : IAsyncDisposable
{
    private readonly OpenClawConfig _config = new()
    {
        BaseUrl = "http://localhost:19999",
        InstanceName = "test",
        TargetChannel = "out",
        SourceChannel = "in",
        PollIntervalMs = 50,
    };

    private OpenClawBridgeChannel? _channel;

    /// <summary>
    /// Intercepts HTTP calls so we never hit a real network endpoint.
    /// </summary>
    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public List<HttpRequestMessage> Requests { get; } = [];

        public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task ConnectAsync_CallsHealthEndpoint()
    {
        var handler = new MockHttpHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/health")
                return new HttpResponseMessage(HttpStatusCode.OK);

            // Return empty array for poll requests
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>()),
            };
        });

        var http = new HttpClient(handler);
        _channel = new OpenClawBridgeChannel(
            http, NullLogger<OpenClawBridgeChannel>.Instance, _config);

        await _channel.ConnectAsync();

        _channel.IsConnected.Should().BeTrue();
        handler.Requests.Should().Contain(r =>
            r.RequestUri!.AbsolutePath == "/api/health");
    }

    [Fact]
    public async Task SendMessageAsync_PostsToOpenClawApi()
    {
        var handler = new MockHttpHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/health")
                return new HttpResponseMessage(HttpStatusCode.OK);

            if (req.RequestUri.AbsolutePath == "/api/messages" && req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>()),
            };
        });

        var http = new HttpClient(handler);
        _channel = new OpenClawBridgeChannel(
            http, NullLogger<OpenClawBridgeChannel>.Instance, _config);

        await _channel.ConnectAsync();
        await _channel.SendMessageAsync("conv-1", "Hello OpenClaw!");

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.AbsolutePath == "/api/messages" && r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task DisconnectAsync_StopsPolling()
    {
        var handler = new MockHttpHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/health")
                return new HttpResponseMessage(HttpStatusCode.OK);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>()),
            };
        });

        var http = new HttpClient(handler);
        _channel = new OpenClawBridgeChannel(
            http, NullLogger<OpenClawBridgeChannel>.Instance, _config);

        await _channel.ConnectAsync();
        _channel.IsConnected.Should().BeTrue();

        await _channel.DisconnectAsync();

        _channel.IsConnected.Should().BeFalse();

        // Capture request count after disconnect; polling should have stopped
        var countAfterDisconnect = handler.Requests.Count;
        await Task.Delay(150); // Wait longer than poll interval
        handler.Requests.Count.Should().Be(countAfterDisconnect,
            "no new requests should be made after disconnect");
    }

    [Fact]
    public void ChannelType_IsOpenClaw()
    {
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var http = new HttpClient(handler);
        _channel = new OpenClawBridgeChannel(
            http, NullLogger<OpenClawBridgeChannel>.Instance, _config);

        _channel.ChannelType.Should().Be("openclaw");
    }

    [Fact]
    public void DisplayName_IncludesInstanceName()
    {
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var http = new HttpClient(handler);
        _channel = new OpenClawBridgeChannel(
            http, NullLogger<OpenClawBridgeChannel>.Instance, _config);

        _channel.DisplayName.Should().Contain("test");
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
    }
}
