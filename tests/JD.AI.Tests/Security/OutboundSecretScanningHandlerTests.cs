using System.Net;
using System.Text;
using JD.AI.Core.Governance;
using JD.AI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Security;

public sealed class OutboundSecretScanningHandlerTests
{
    private static DataRedactor BuildRedactor() =>
        new(SecretPatternLibrary.HighConfidence);

    private static HttpClient BuildClient(bool block = true)
    {
        var inner = new MockHandler();
        var handler = new OutboundSecretScanningHandler(
            BuildRedactor(),
            NullLogger<OutboundSecretScanningHandler>.Instance,
            blockOnDetection: block)
        {
            InnerHandler = inner,
        };
        return new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
    }

    [Fact]
    public async Task SendAsync_CleanBody_PassesThrough()
    {
        using var client = BuildClient();
        using var content = new StringContent("{\"prompt\":\"Hello world\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/chat", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_BodyContainsGitHubToken_ThrowsSecurityException()
    {
        using var client = BuildClient(block: true);
        var token = "ghp_" + "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrR";
        using var content = new StringContent($"{{\"data\":\"{token}\"}}", Encoding.UTF8, "application/json");

        await Assert.ThrowsAsync<SecurityException>(() => client.PostAsync("/upload", content));
    }

    [Fact]
    public async Task SendAsync_BodyContainsGitHubToken_AuditModeAllowsThrough()
    {
        using var client = BuildClient(block: false);
        var token = "ghp_" + "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrR";
        using var content = new StringContent($"{{\"data\":\"{token}\"}}", Encoding.UTF8, "application/json");

        // Should not throw in audit mode
        var response = await client.PostAsync("/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_NoBody_PassesThrough()
    {
        using var client = BuildClient();
        var response = await client.GetAsync("/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class MockHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
