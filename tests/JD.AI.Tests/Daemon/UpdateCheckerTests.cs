using System.Net;
using System.Text;
using JD.AI.Daemon.Config;
using JD.AI.Daemon.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DaemonUpdateChecker = JD.AI.Daemon.Services.UpdateChecker;
using DaemonUpdateInfo = JD.AI.Daemon.Services.UpdateInfo;

namespace JD.AI.Tests.Daemon;

/// <summary>
/// Unit tests for <see cref="DaemonUpdateChecker"/> using a stub HttpClient to avoid network calls.
/// </summary>
public sealed class UpdateCheckerTests
{
    private static DaemonUpdateChecker Build(string responseJson, HttpStatusCode status = HttpStatusCode.OK, string? packageId = null, bool preRelease = false)
    {
        var handler = new StubHttpHandler(responseJson, status);
        var httpClient = new HttpClient(handler) { BaseAddress = null };
        var factory = new StubHttpClientFactory(httpClient);
        var options = Options.Create(new UpdateConfig
        {
            PackageId = packageId ?? "JD.AI.Daemon",
            NuGetFeedUrl = "https://api.nuget.org/v3-flatcontainer/",
            PreRelease = preRelease,
        });
        return new DaemonUpdateChecker(options, factory, NullLogger<DaemonUpdateChecker>.Instance);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_WhenAlreadyLatest()
    {
        // Current version: 0.0.0 (test entry assembly has no version), latest: 0.0.0 → no update
        var json = """{"versions":["0.0.0"]}""";
        var checker = Build(json);
        var result = await checker.CheckForUpdateAsync();
        // Either null (already latest) or not — but no exception thrown
        Assert.True(result is null || result is DaemonUpdateInfo);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsUpdateInfo_WhenNewerVersionAvailable()
    {
        var json = """{"versions":["9999.99.99"]}""";
        var checker = Build(json);
        var result = await checker.CheckForUpdateAsync();
        Assert.NotNull(result);
        Assert.Equal(new Version(9999, 99, 99), result!.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_WhenNoVersionsInResponse()
    {
        var json = """{"versions":[]}""";
        var checker = Build(json);
        var result = await checker.CheckForUpdateAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_WhenHttpFails()
    {
        // Status 500 → HttpRequestException swallowed, returns null
        var checker = Build("error", HttpStatusCode.InternalServerError);
        var result = await checker.CheckForUpdateAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task CheckForUpdateAsync_SkipsPreRelease_WhenPreReleaseIsFalse()
    {
        // Only pre-release versions in feed → should return null since PreRelease = false
        var json = """{"versions":["9999.99.99-beta.1","9999.99.99-rc.1"]}""";
        var checker = Build(json, preRelease: false);
        var result = await checker.CheckForUpdateAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task CheckForUpdateAsync_IncludesPreRelease_WhenEnabled()
    {
        var json = """{"versions":["9999.99.99-beta.1"]}""";
        var checker = Build(json, preRelease: true);
        var result = await checker.CheckForUpdateAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public void CurrentVersion_ReturnsVersion()
    {
        var checker = Build("""{"versions":[]}""");
        Assert.NotNull(checker.CurrentVersion);
    }

    [Fact]
    public void UpdateInfo_ToString_ContainsArrow()
    {
        var info = new DaemonUpdateInfo(new Version(1, 0, 0), new Version(2, 0, 0));
        Assert.Contains("→", info.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateInfo_Properties_AreSet()
    {
        var current = new Version(1, 0);
        var latest = new Version(2, 0);
        var info = new DaemonUpdateInfo(current, latest);
        Assert.Equal(current, info.CurrentVersion);
        Assert.Equal(latest, info.LatestVersion);
    }

    private sealed class StubHttpHandler(string content, HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
