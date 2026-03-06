using FluentAssertions;
using JD.AI.Gateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace JD.AI.Gateway.Tests.Middleware;

public sealed class ApiVersionRewriteMiddlewareTests
{
    private static ApiVersionRewriteMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        next ??= _ => Task.CompletedTask;
        return new ApiVersionRewriteMiddleware(next);
    }

    // ── Path rewriting ────────────────────────────────────────────────────

    [Fact]
    public async Task UnversionedApiPath_IsRewrittenToV1()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/agents";
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Path.Value.Should().Be("/api/v1/agents");
    }

    [Fact]
    public async Task AlreadyVersionedPath_IsNotRewritten()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/agents";
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Path.Value.Should().Be("/api/v1/agents");
    }

    [Fact]
    public async Task NonApiPath_IsNotRewritten()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Path.Value.Should().Be("/health");
    }

    [Fact]
    public async Task NullPath_IsNotRewritten()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = PathString.Empty;
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Path.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task CaseInsensitive_ApiPath_IsRewritten()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/API/sessions";
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Path.Value.Should().Be("/api/v1/sessions");
    }

    [Fact]
    public async Task CaseInsensitive_AlreadyVersioned_NotRewritten()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/API/V1/agents";
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Path.Value.Should().Be("/API/V1/agents");
    }

    // ── Deprecation headers (via OnStarting callback) ──────────────────

    [Fact]
    public async Task Rewritten_RegistersOnStartingCallback()
    {
        // DefaultHttpContext does not fully support OnStarting in tests,
        // but we can verify the rewrite path was applied and that the middleware
        // registered a callback by using a custom response feature.
        var callbackRegistered = false;
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/agents";
        context.Features.Set<IHttpResponseFeature>(new FakeResponseFeature(() => callbackRegistered = true));

        var middleware = CreateMiddleware();
        await middleware.InvokeAsync(context);

        callbackRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task NotRewritten_DoesNotRegisterCallback()
    {
        var callbackRegistered = false;
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/agents";
        context.Features.Set<IHttpResponseFeature>(new FakeResponseFeature(() => callbackRegistered = true));

        var middleware = CreateMiddleware();
        await middleware.InvokeAsync(context);

        callbackRegistered.Should().BeFalse();
    }

    private sealed class FakeResponseFeature : IHttpResponseFeature
    {
        private readonly Action _onCallbackRegistered;

        public FakeResponseFeature(Action onCallbackRegistered) =>
            _onCallbackRegistered = onCallbackRegistered;

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state) =>
            _onCallbackRegistered();

        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    // ── Next delegate ─────────────────────────────────────────────────────

    [Fact]
    public async Task AlwaysCallsNext()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/agents";
        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task NextCalledEvenForNonApiPath()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task NestedApiPath_IsRewrittenCorrectly()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/agents/123/sessions";
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Path.Value.Should().Be("/api/v1/agents/123/sessions");
    }
}
