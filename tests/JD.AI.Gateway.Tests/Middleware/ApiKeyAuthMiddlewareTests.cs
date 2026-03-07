using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Security;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Gateway.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="ApiKeyAuthMiddleware"/> — exercises the middleware directly
/// using <see cref="DefaultHttpContext"/> so we don't depend on gateway configuration.
/// </summary>
public sealed class ApiKeyAuthMiddlewareUnitTests
{
    private const string ValidKey = "test-key-123";

    private static ApiKeyAuthMiddleware CreateMiddleware(
        RequestDelegate next,
        ApiKeyAuthProvider? authProvider = null)
    {
        var provider = authProvider ?? new ApiKeyAuthProvider();
        provider.RegisterKey(ValidKey, "Test User", GatewayRole.Admin);
        return new ApiKeyAuthMiddleware(next, provider);
    }

    [Fact]
    public async Task Unauthenticated_ApiRequest_Returns401()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };

        var middleware = CreateMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/sessions";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task ValidApiKey_InHeader_CallsNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };

        var middleware = CreateMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/sessions";
        context.Request.Headers["X-API-Key"] = ValidKey;

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
        context.Items.Should().ContainKey("Identity");
    }

    [Fact]
    public async Task ValidApiKey_InQueryParam_CallsNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };

        var middleware = CreateMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/sessions";
        context.Request.QueryString = new QueryString($"?api_key={ValidKey}");

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Theory]
    [InlineData(GatewayRuntimeDefaults.HealthPath)]
    [InlineData(GatewayRuntimeDefaults.ReadyPath)]
    [InlineData("/hubs/agent")]
    [InlineData("/hubs/events")]
    public async Task SkipPaths_BypassAuth(string path)
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };

        var middleware = CreateMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        called.Should().BeTrue("path '{0}' should bypass authentication", path);
    }

    [Fact]
    public async Task InvalidApiKey_Returns401()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };

        var middleware = CreateMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/agents";
        context.Request.Headers["X-API-Key"] = "wrong-key";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task NonApiPath_WithoutKey_PassesThrough()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };

        var middleware = CreateMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Path = "/some/other/path";

        await middleware.InvokeAsync(context);

        called.Should().BeTrue("non-/api/ paths should pass through without auth");
    }
}

/// <summary>
/// Integration tests that enable auth via custom configuration and exercise
/// the full middleware pipeline using <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// </summary>
public sealed class ApiKeyAuthMiddlewareIntegrationTests
    : IClassFixture<ApiKeyAuthMiddlewareIntegrationTests.AuthEnabledFactory>
{
    private const string TestApiKey = "integration-test-key";
    private readonly HttpClient _client;

    public ApiKeyAuthMiddlewareIntegrationTests(AuthEnabledFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Unauthenticated_ApiRequest_Returns401_WhenAuthEnabled()
    {
        var response = await _client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidApiKey_ReturnsOk_WhenAuthEnabled()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/sessions");
        request.Headers.Add("X-API-Key", TestApiKey);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_BypassesAuth()
    {
        var response = await _client.GetAsync(GatewayRuntimeDefaults.HealthPath);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyEndpoint_BypassesAuth()
    {
        var response = await _client.GetAsync(GatewayRuntimeDefaults.ReadyPath);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Custom factory that injects Gateway config with Auth.Enabled = true
    /// by replacing the <see cref="GatewayConfig"/> and <see cref="IAuthProvider"/>
    /// singletons in the DI container.
    /// </summary>
#pragma warning disable CA1034 // Nested type used by xUnit IClassFixture
    public sealed class AuthEnabledFactory : WebApplicationFactory<Program>
#pragma warning restore CA1034
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("Gateway:Auth:Enabled", "true");
            builder.UseSetting("Gateway:Auth:ApiKeys:0:Key", TestApiKey);
            builder.UseSetting("Gateway:Auth:ApiKeys:0:Name", "IntegrationTest");
            builder.UseSetting("Gateway:Auth:ApiKeys:0:Role", "Admin");
            builder.UseSetting("Gateway:RateLimit:Enabled", "false");
        }

        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            catch (AggregateException) { }
        }

        public override async ValueTask DisposeAsync()
        {
            try { await base.DisposeAsync(); }
            catch (AggregateException) { }
            GC.SuppressFinalize(this);
        }
    }
}
