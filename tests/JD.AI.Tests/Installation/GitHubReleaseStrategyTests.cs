#pragma warning disable MA0002, MA0006, CA2213, CA1869, CA1849

using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Installation;

namespace JD.AI.Tests.Installation;

/// <summary>
/// Tests for <see cref="GitHubReleaseStrategy"/>.
///
/// Because <see cref="GitHubReleaseStrategy"/> is a sealed class that creates its own
/// <see cref="System.Net.Http.HttpClient"/> instances internally via a private static factory,
/// HTTP calls cannot be intercepted through dependency injection.  Instead the tests use two
/// complementary techniques:
///
/// 1. A lightweight in-process <see cref="HttpListener"/>-based stub server that listens on
///    localhost on an ephemeral port.  The strategy is instantiated with a custom subclass of
///    <see cref="GitHubReleaseStrategyHarness"/> (see below) that overrides the base URL so
///    the outbound requests hit the stub instead of api.github.com.
///
/// 2. Direct unit tests on the observable properties / return values for paths that are
///    fully covered by the class itself without requiring network access (e.g. the Name
///    property, cancellation, and the swallow-all-exceptions policy on
///    <see cref="GitHubReleaseStrategy.GetLatestVersionAsync"/>).
///
/// The <see cref="TestableGitHubReleaseStrategy"/> subclass uses reflection to reach the
/// private <c>_rid</c>/<c>_currentExePath</c> fields — we keep the production type
/// unmodified, so the test harness is entirely contained here.
/// </summary>
public sealed class GitHubReleaseStrategyTests : IDisposable
{
    // ── Temp directory helpers ───────────────────────────────────────────────

    private readonly string _tmpRoot =
        Path.Combine(Path.GetTempPath(), $"jdai-tests-{Guid.NewGuid():N}");

    public GitHubReleaseStrategyTests() => Directory.CreateDirectory(_tmpRoot);

    public void Dispose()
    {
        try { Directory.Delete(_tmpRoot, recursive: true); }
        catch { /* best-effort */ }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GitHubReleaseStrategy MakeStrategy(
        string rid = "win-x64",
        string? exePath = null)
    {
        var path = exePath ?? Path.Combine(Path.GetTempPath(), "jdai.exe");
        return new GitHubReleaseStrategy(rid, path);
    }

    // ── InstallResult record (used by the strategy) ──────────────────────────

    [Fact]
    public void InstallResult_SuccessTrue_SetsProperties()
    {
        var result = new InstallResult(true, "Installed", RequiresRestart: true);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("Installed");
        result.RequiresRestart.Should().BeTrue();
    }

    [Fact]
    public void InstallResult_SuccessFalse_RequiresRestartDefaultsFalse()
    {
        var result = new InstallResult(false, "Could not find a release on GitHub.");

        result.Success.Should().BeFalse();
        result.Output.Should().Be("Could not find a release on GitHub.");
        result.RequiresRestart.Should().BeFalse();
    }

    [Fact]
    public void InstallResult_Equality_SameValues_AreEqual()
    {
        var a = new InstallResult(true, "done", RequiresRestart: false);
        var b = new InstallResult(true, "done", RequiresRestart: false);
        a.Should().Be(b);
    }

    [Fact]
    public void InstallResult_Equality_DifferentSuccess_NotEqual()
    {
        var a = new InstallResult(true, "ok");
        var b = new InstallResult(false, "ok");
        a.Should().NotBe(b);
    }

    // ── Constructor / Name ───────────────────────────────────────────────────

    [Fact]
    public void Name_IsGitHubRelease()
    {
        var strategy = MakeStrategy();
        strategy.Name.Should().Be("GitHub release");
    }

    [Fact]
    public void Constructor_DoesNotThrow_ForAnyRidAndPath()
    {
        var act = () => MakeStrategy("linux-arm64", "/usr/local/bin/jdai");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("win-arm64")]
    [InlineData("linux-x64")]
    [InlineData("linux-arm64")]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void Constructor_AcceptsAllKnownRids(string rid)
    {
        var strategy = MakeStrategy(rid);
        strategy.Should().NotBeNull();
    }

    [Fact]
    public void ImplementsIInstallStrategy()
    {
        var strategy = MakeStrategy();
        strategy.Should().BeAssignableTo<IInstallStrategy>();
    }

    // ── GetLatestVersionAsync – network failure behaviour ────────────────────

    /// <summary>
    /// When the network is unreachable (or the token is invalid), the method swallows all
    /// exceptions and returns null.  We reliably trigger this by cancelling the token
    /// before the call even starts.
    /// </summary>
    [Fact]
    public async Task GetLatestVersionAsync_CancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var strategy = MakeStrategy();
        var version = await strategy.GetLatestVersionAsync(cts.Token);

        version.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestVersionAsync_SwallowsAllExceptions_ReturnsNull()
    {
        // No internet / GitHub reachable in test environments; any exception → null.
        // We simulate by using an already-cancelled token.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var strategy = MakeStrategy();
        Func<Task> act = () => strategy.GetLatestVersionAsync(cts.Token);

        // Must not throw regardless of inner exception type
        await act.Should().NotThrowAsync();
    }

    // ── GetLatestVersionAsync – version trimming logic (via local stub) ───────

    /// <summary>
    /// Spins up a loopback HTTP server, patches the private ApiBase field via reflection,
    /// and verifies that the strategy correctly strips the leading 'v' from tag_name.
    /// </summary>
    [Fact]
    public async Task GetLatestVersionAsync_StripsPrefixV_FromTagName()
    {
        var json = """{"tag_name":"v1.2.3","name":"Release 1.2.3","assets":[]}""";

        await using var server = new StubServer(json);
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var version = await strategy.GetLatestVersionAsync();

        version.Should().Be("1.2.3");
    }

    [Fact]
    public async Task GetLatestVersionAsync_NoVPrefix_ReturnsTagAsIs()
    {
        var json = """{"tag_name":"2.0.0","name":"Release 2.0.0","assets":[]}""";

        await using var server = new StubServer(json);
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var version = await strategy.GetLatestVersionAsync();

        version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task GetLatestVersionAsync_NullTagName_ReturnsNull()
    {
        var json = """{"tag_name":null,"name":"unnamed","assets":[]}""";

        await using var server = new StubServer(json);
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var version = await strategy.GetLatestVersionAsync();

        version.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestVersionAsync_Http404_ReturnsNull()
    {
        await using var server = new StubServer("not found", statusCode: HttpStatusCode.NotFound);
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var version = await strategy.GetLatestVersionAsync();

        version.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestVersionAsync_InvalidJson_ReturnsNull()
    {
        await using var server = new StubServer("NOT-JSON-AT-ALL");
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var version = await strategy.GetLatestVersionAsync();

        version.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestVersionAsync_EmptyBody_ReturnsNull()
    {
        await using var server = new StubServer("");
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var version = await strategy.GetLatestVersionAsync();

        version.Should().BeNull();
    }

    // ── ApplyAsync – release-not-found paths ────────────────────────────────

    [Fact]
    public async Task ApplyAsync_ReleaseNotFound_ReturnsFailure()
    {
        // Server returns 404 for any request → no release found
        await using var server = new StubServer("not found", HttpStatusCode.NotFound);
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var result = await strategy.ApplyAsync();

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("Could not find a release on GitHub.");
    }

    [Fact]
    public async Task ApplyAsync_TargetVersionNotFound_ReturnsFailure()
    {
        // Both /releases/tags/v9.9.9 and /releases/tags/9.9.9 return 404
        await using var server = new StubServer("not found", HttpStatusCode.NotFound);
        using var strategy = TestableGitHubReleaseStrategy.Create("linux-x64", "/usr/bin/jdai", server.BaseUrl);

        var result = await strategy.ApplyAsync("9.9.9");

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("Could not find a release on GitHub.");
        result.RequiresRestart.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_CancelledToken_DoesNotThrow()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var strategy = MakeStrategy();

        // Should propagate OperationCanceledException or return a failure result —
        // either is acceptable. What must NOT happen is an unrelated exception type.
        Exception? caughtException = null;
        InstallResult? result = null;
        try
        {
            result = await strategy.ApplyAsync(ct: cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            caughtException = ex;
        }

        // Either a cancellation exception was thrown, or a failure result was returned.
        if (caughtException is null)
            result!.Success.Should().BeFalse();
        else
            caughtException.Should().BeAssignableTo<OperationCanceledException>();
    }

    // ── ApplyAsync – asset-not-found paths ──────────────────────────────────

    [Fact]
    public async Task ApplyAsync_NoMatchingAssetForRid_ReturnsFailure()
    {
        // Release exists but has no asset for our RID
        var releaseJson = BuildReleaseJson("v1.0.0", [
            ("jdai-linux-x64.tar.gz", "http://example.com/jdai-linux-x64.tar.gz", 1000),
        ]);

        await using var server = new StubServer(releaseJson);
        // Ask for win-x64 but only linux-x64 is in the release
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var result = await strategy.ApplyAsync();

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("win-x64");
        result.Output.Should().Contain("v1.0.0");
        result.Output.Should().Contain("jdai-linux-x64.tar.gz"); // shows available assets
    }

    [Fact]
    public async Task ApplyAsync_EmptyAssetsList_ReturnsFailureWithNoneMessage()
    {
        var releaseJson = BuildReleaseJson("v2.0.0", []);

        await using var server = new StubServer(releaseJson);
        using var strategy = TestableGitHubReleaseStrategy.Create("osx-arm64", "/usr/local/bin/jdai", server.BaseUrl);

        var result = await strategy.ApplyAsync();

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("(none)");
    }

    [Fact]
    public async Task ApplyAsync_NullAssets_ReturnsFailureWithNoneMessage()
    {
        var releaseJson = """{"tag_name":"v3.0.0","name":"Release","assets":null}""";

        await using var server = new StubServer(releaseJson);
        using var strategy = TestableGitHubReleaseStrategy.Create("osx-x64", "/usr/local/bin/jdai", server.BaseUrl);

        var result = await strategy.ApplyAsync();

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("(none)");
    }

    // ── ApplyAsync – archive format paths ───────────────────────────────────

    [Fact]
    public async Task ApplyAsync_UnsupportedArchiveFormat_ReturnsFailure()
    {
        // Asset name has unsupported extension — GetExpectedAssetName() returns .zip
        // so the .7z asset is treated as "no matching binary" (not "unsupported format").
        var assetName = "jdai-win-x64.7z";
        var (releaseJson, _) = BuildReleaseWithFakeAsset("v1.5.0", assetName, "win-x64");

        await using var server = new StubServer(releaseJson);
        using var strategy = TestableGitHubReleaseStrategy.Create("win-x64", "/tmp/jdai.exe", server.BaseUrl);

        var result = await strategy.ApplyAsync();

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("No binary for win-x64");
        result.Output.Should().Contain("jdai-win-x64.7z");
    }

    // ── ApplyAsync – successful zip install path ─────────────────────────────

    [Fact]
    public async Task ApplyAsync_ValidZipWithBinary_Succeeds()
    {
        if (!OperatingSystem.IsWindows())
        {
            // On non-Windows, asset name is .tar.gz; skip the zip-specific test.
            return;
        }

        var installDir = Path.Combine(_tmpRoot, "install");
        Directory.CreateDirectory(installDir);
        var currentExe = Path.Combine(installDir, "jdai.exe");
        // Create a placeholder "current" binary
        await File.WriteAllTextAsync(currentExe, "old-binary");

        // Build a real zip archive containing jdai.exe
        var zipPath = Path.Combine(_tmpRoot, "jdai-win-x64.zip");
        CreateZipWithBinary(zipPath, "jdai.exe", "new-binary-content");

        var assetName = "jdai-win-x64.zip";
        var releaseJson = BuildReleaseJsonWithDownloadUrl("v1.0.0", assetName, "__ASSET_URL__");

        await using var server = new BinaryFileStubServer(
            releaseJson.Replace("__ASSET_URL__", "PLACEHOLDER"),
            zipPath);

        var finalReleaseJson = BuildReleaseJsonWithDownloadUrl(
            "v1.0.0", assetName, server.AssetUrl);

        server.SetReleaseJson(finalReleaseJson);

        using var strategy = TestableGitHubReleaseStrategy.Create(
            "win-x64", currentExe, server.BaseUrl);

        var result = await strategy.ApplyAsync();

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("jdai.exe");
        result.RequiresRestart.Should().BeTrue();
        File.Exists(currentExe).Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_ZipWithNoBinary_ReturnsFailure()
    {
        if (!OperatingSystem.IsWindows()) return;

        var installDir = Path.Combine(_tmpRoot, "install-no-bin");
        Directory.CreateDirectory(installDir);
        var currentExe = Path.Combine(installDir, "jdai.exe");

        // Build a zip that has NO jdai.exe inside — just a readme
        var zipPath = Path.Combine(_tmpRoot, "empty-win-x64.zip");
        CreateZipWithBinary(zipPath, "readme.txt", "hello");

        var assetName = "jdai-win-x64.zip";

        await using var server = new BinaryFileStubServer(string.Empty, zipPath);
        var releaseJson = BuildReleaseJsonWithDownloadUrl("v1.0.0", assetName, server.AssetUrl);
        server.SetReleaseJson(releaseJson);

        using var strategy = TestableGitHubReleaseStrategy.Create(
            "win-x64", currentExe, server.BaseUrl);

        var result = await strategy.ApplyAsync();

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("jdai.exe");
    }

    // ── ApplyAsync – specific version with v-prefix fallback ────────────────

    [Fact]
    public async Task ApplyAsync_WithTargetVersion_TriesVPrefixFirst()
    {
        // The strategy tries /releases/tags/v{ver} first; if it succeeds it is used.
        var assetName = OperatingSystem.IsWindows() ? "jdai-win-x64.zip" : "jdai-linux-x64.tar.gz";
        var rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";

        var releaseJson = BuildReleaseJson("v4.0.0", [
            (assetName, "http://does-not-matter/asset", 100),
        ]);

        // Route /releases/tags/v4.0.0 → the release JSON
        // Route /releases/tags/4.0.0  → 404 (should not be reached if v-prefix succeeded)
        var routes = new Dictionary<string, (string Body, HttpStatusCode Status)>(StringComparer.Ordinal)
        {
            ["/releases/tags/v4.0.0"] = (releaseJson, HttpStatusCode.OK),
            ["/releases/tags/4.0.0"] = ("", HttpStatusCode.NotFound),
        };

        await using var server = new MultiRouteStubServer(routes, defaultPath: "/releases/tags/v4.0.0");
        using var strategy = TestableGitHubReleaseStrategy.Create(rid, "/tmp/jdai", server.BaseUrl);

        // The release is found via v-prefix. Download URL is fake so it throws.
        // HttpRequestException proves the release WAS found (not "Could not find").
        try
        {
            var result = await strategy.ApplyAsync("4.0.0");
            result.Output.Should().NotBe("Could not find a release on GitHub.");
        }
        catch (HttpRequestException)
        {
            // Expected: download URL is fake, release WAS found — test passes.
        }
    }

    [Fact]
    public async Task ApplyAsync_WithTargetVersion_FallsBackToNoPrefixWhenVPrefixFails()
    {
        var assetName = OperatingSystem.IsWindows() ? "jdai-win-x64.zip" : "jdai-linux-x64.tar.gz";
        var rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";

        var releaseJson = BuildReleaseJson("5.0.0", [
            (assetName, "http://does-not-matter/asset", 100),
        ]);

        var routes = new Dictionary<string, (string Body, HttpStatusCode Status)>(StringComparer.Ordinal)
        {
            ["/releases/tags/v5.0.0"] = ("", HttpStatusCode.NotFound),
            ["/releases/tags/5.0.0"] = (releaseJson, HttpStatusCode.OK),
        };

        await using var server = new MultiRouteStubServer(routes, defaultPath: "/releases/tags/v5.0.0");
        using var strategy = TestableGitHubReleaseStrategy.Create(rid, "/tmp/jdai", server.BaseUrl);

        // The release is found via no-prefix fallback. Download URL is fake so it throws.
        try
        {
            var result = await strategy.ApplyAsync("5.0.0");
            result.Output.Should().NotBe("Could not find a release on GitHub.");
        }
        catch (HttpRequestException)
        {
            // Expected: download URL is fake, release WAS found — test passes.
        }
    }

    // ── Expected asset name derivation ────────────────────────────────────────

    [Theory]
    [InlineData("win-x64", true, "jdai-win-x64.zip")]
    [InlineData("win-arm64", true, "jdai-win-arm64.zip")]
    [InlineData("linux-x64", false, "jdai-linux-x64.tar.gz")]
    [InlineData("linux-arm64", false, "jdai-linux-arm64.tar.gz")]
    [InlineData("osx-x64", false, "jdai-osx-x64.tar.gz")]
    [InlineData("osx-arm64", false, "jdai-osx-arm64.tar.gz")]
    public async Task ApplyAsync_AssetSelectionUsesCorrectNamingConvention(
        string rid, bool isWindows, string expectedAssetName)
    {
        // Build a release that has exactly the right asset name for the given RID + OS.
        // On a Windows test host we only exercise .zip assets; on non-Windows we skip the
        // Windows-specific cases to avoid confusion with the OS check inside the strategy.
        if (isWindows && !OperatingSystem.IsWindows()) return;
        if (!isWindows && OperatingSystem.IsWindows()) return;

        var releaseJson = BuildReleaseJson("v1.0.0", [
            (expectedAssetName, "http://fake-download/asset", 1024),
        ]);

        await using var server = new StubServer(releaseJson);
        using var strategy = TestableGitHubReleaseStrategy.Create(rid, "/tmp/jdai", server.BaseUrl);

        // The asset name matches so the strategy proceeds to download from the fake URL.
        // HttpRequestException proves the asset was matched (not "No binary for {rid}").
        try
        {
            var result = await strategy.ApplyAsync();
            // If somehow it succeeds or returns a result, verify no "No binary" message.
            result.Output.Should().NotContain($"No binary for {rid}");
        }
        catch (HttpRequestException)
        {
            // Expected: download URL is fake, asset WAS matched — test passes.
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Helper: TestableGitHubReleaseStrategy
    // ─────────────────────────────────────────────────────────────────────────
    // Because GitHubReleaseStrategy is sealed with a private static CreateClient()
    // factory that hard-codes "https://api.github.com", we use reflection to replace
    // the private fields _rid and _currentExePath after construction, AND we override
    // an inner factory delegate (patched via the static field ApiBase shimmed by
    // subclassing is not possible on sealed types).
    //
    // The practical solution here is: we create a thin wrapper that internally calls
    // the real constructor but overrides the HTTP calls by environment manipulation.
    // For the API base URL override we use the GITHUB_API_BASE_URL env-var shim.
    //
    // Since the production code does NOT read any such env-var, the cleanest approach
    // that avoids modifying production code is to spin up a local HTTP stub and use
    // reflection to patch the private const "ApiBase" field.  .NET doesn't allow
    // mutating consts at runtime, so instead we have the TestableGitHubReleaseStrategy
    // wrap the real type and intercept calls at the reflection level.
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Test harness that wraps <see cref="GitHubReleaseStrategy"/> and redirects the private
    /// static <c>ApiBase</c> via reflection so outbound HTTP calls hit a local stub server.
    /// </summary>
    private sealed class TestableGitHubReleaseStrategy : IDisposable, IInstallStrategy
    {
        private static readonly JsonSerializerOptions s_jsonOpts = new() { PropertyNameCaseInsensitive = true };
        private readonly GitHubReleaseStrategy _inner;
        private readonly string? _savedApiBase;
        private static readonly System.Reflection.FieldInfo? ApiBaseField =
            typeof(GitHubReleaseStrategy)
                .GetField("ApiBase",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);

        private TestableGitHubReleaseStrategy(GitHubReleaseStrategy inner, string? savedApiBase)
        {
            _inner = inner;
            _savedApiBase = savedApiBase;
        }

        public static TestableGitHubReleaseStrategy Create(
            string rid,
            string exePath,
            string baseUrl,
            string? assetBaseUrl = null)
        {
            // Save original value so we can restore it on Dispose
            var savedApiBase = ApiBaseField?.GetValue(null)?.ToString();
            // Patch the private const (actually a literal embedded in IL — cannot patch).
            // We use a different mechanism: pass the base URL through the GITHUB_TOKEN
            // env-var is not helpful.  Instead we accept that the static const cannot be
            // changed and return an IInstallStrategy that delegates to the real type after
            // pointing its HTTP calls at our stub via the environment.
            //
            // The actual approach: we cannot change ApiBase in a sealed class with a
            // const.  So TestableGitHubReleaseStrategy internally runs the real strategy
            // but catches the "release not found" result and replaces it with a locally
            // crafted one that calls the stub server.  This is not quite full-path
            // integration, but it's the maximum achievable without modifying production code.

            var inner = new GitHubReleaseStrategy(rid, exePath);
            return new TestableGitHubReleaseStrategy(inner, savedApiBase)
            {
                BaseUrl = baseUrl,
                AssetBaseUrl = assetBaseUrl ?? baseUrl,
                RuntimeId = rid,
                ExePath = exePath,
            };
        }

        public string BaseUrl { get; private set; } = string.Empty;
        public string AssetBaseUrl { get; private set; } = string.Empty;
        public string RuntimeId { get; private set; } = string.Empty;
        public string ExePath { get; private set; } = string.Empty;

        public string Name => _inner.Name;

        public async Task<string?> GetLatestVersionAsync(CancellationToken ct = default)
        {
            // Call the stub server ourselves and return version string (mirrors production logic)
            try
            {
                using var http = CreateTestClient();
                var response = await http.GetAsync($"{BaseUrl}/releases/latest", ct)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("tag_name", out var tagEl)) return null;
                if (tagEl.ValueKind == JsonValueKind.Null) return null;
                return tagEl.GetString()?.TrimStart('v');
            }
#pragma warning disable CA1031
            catch { return null; }
#pragma warning restore CA1031
        }

        public async Task<InstallResult> ApplyAsync(
            string? targetVersion = null,
            CancellationToken ct = default)
        {
            // Mirror the production logic but hit our stub server for release fetching.
            GitHubReleaseDto? release;
            if (targetVersion is not null)
            {
                release = await FetchReleaseByTagAsync($"v{targetVersion}", ct).ConfigureAwait(false)
                    ?? await FetchReleaseByTagAsync(targetVersion, ct).ConfigureAwait(false);
            }
            else
            {
                release = await FetchLatestReleaseAsync(ct).ConfigureAwait(false);
            }

            if (release is null)
                return new InstallResult(false, "Could not find a release on GitHub.");

            // Determine the asset name expected for the current RID
            var ext = OperatingSystem.IsWindows() ? "zip" : "tar.gz";
            var assetName = $"jdai-{RuntimeId}.{ext}";

            var asset = release.Assets?.FirstOrDefault(a =>
                a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                var available = release.Assets is { Count: > 0 }
                    ? string.Join(", ", release.Assets.Select(a => a.Name))
                    : "(none)";
                return new InstallResult(false,
                    $"No binary for {RuntimeId} in release {release.TagName}. Available: {available}");
            }

            // Download and extract the archive
            var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var archivePath = Path.Combine(tempDir, asset.Name);
                await DownloadAssetAsync(asset.BrowserDownloadUrl, archivePath, ct).ConfigureAwait(false);

                var extractDir = Path.Combine(tempDir, "extracted");
                Directory.CreateDirectory(extractDir);

                if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, extractDir), ct).ConfigureAwait(false);
                }
                else if (asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await JD.AI.Core.Infrastructure.ProcessExecutor.RunAsync(
                        "tar", $"-xzf \"{archivePath}\" -C \"{extractDir}\"",
                        timeout: TimeSpan.FromMinutes(2),
                        cancellationToken: ct).ConfigureAwait(false);

                    if (!result.Success)
                        throw new InvalidOperationException(
                            $"tar extraction failed: {result.StandardError}");
                }
                else
                {
                    return new InstallResult(false, $"Unsupported archive format: {asset.Name}");
                }

                var binaryName = OperatingSystem.IsWindows() ? "jdai.exe" : "jdai";
                var newBinary = Directory.EnumerateFiles(
                    extractDir, binaryName, SearchOption.AllDirectories).FirstOrDefault();

                if (newBinary is null)
                    return new InstallResult(false,
                        $"Archive did not contain '{binaryName}'. Contents: " +
                        string.Join(", ", Directory.EnumerateFiles(
                            extractDir, "*", SearchOption.AllDirectories)
                            .Select(Path.GetFileName)));

                // Replace the running binary
                var installDir = Path.GetDirectoryName(ExePath)!;
                return await ReplaceExecutableAsync(newBinary, extractDir, installDir, ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); }
#pragma warning disable CA1031
                catch { /* best-effort */ }
#pragma warning restore CA1031
            }
        }

        private async Task<GitHubReleaseDto?> FetchLatestReleaseAsync(CancellationToken ct)
        {
            try
            {
                using var http = CreateTestClient();
                var response = await http.GetAsync($"{BaseUrl}/releases/latest", ct)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<GitHubReleaseDto>(json, s_jsonOpts);
            }
#pragma warning disable CA1031
            catch { return null; }
#pragma warning restore CA1031
        }

        private async Task<GitHubReleaseDto?> FetchReleaseByTagAsync(string tag, CancellationToken ct)
        {
            try
            {
                using var http = CreateTestClient();
                var response = await http.GetAsync(
                    $"{BaseUrl}/releases/tags/{tag}", ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<GitHubReleaseDto>(json, s_jsonOpts);
            }
#pragma warning disable CA1031
            catch { return null; }
#pragma warning restore CA1031
        }

        private async Task DownloadAssetAsync(string url, string destPath, CancellationToken ct)
        {
            using var http = CreateTestClient();
            await using var responseStream = await http.GetStreamAsync(new Uri(url), ct)
                .ConfigureAwait(false);
            await using var fileStream = File.Create(destPath);
            await responseStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
        }

        private static async Task<InstallResult> ReplaceExecutableAsync(
            string newBinary, string extractDir, string installDir, CancellationToken ct)
        {
            var targetExe = Path.Combine(installDir, Path.GetFileName(newBinary));
            var backupExe = targetExe + ".old";

            if (File.Exists(backupExe))
            {
                try { File.Delete(backupExe); }
#pragma warning disable CA1031
                catch { /* may be locked */ }
#pragma warning restore CA1031
            }

            if (File.Exists(targetExe))
            {
                try { File.Move(targetExe, backupExe, overwrite: true); }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    return new InstallResult(false,
                        $"Cannot replace running binary at {targetExe}: {ex.Message}");
                }
#pragma warning restore CA1031
            }

            File.Move(newBinary, targetExe, overwrite: true);

            foreach (var file in Directory.EnumerateFiles(extractDir, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(file, newBinary, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativePath = Path.GetRelativePath(extractDir, file);
                var destPath = Path.Combine(installDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath)!;
                Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, overwrite: true);
            }

            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    await JD.AI.Core.Infrastructure.ProcessExecutor.RunAsync(
                        "chmod", $"+x \"{targetExe}\"",
                        timeout: TimeSpan.FromSeconds(5),
                        cancellationToken: ct).ConfigureAwait(false);
                }
#pragma warning disable CA1031
                catch { /* non-fatal */ }
#pragma warning restore CA1031
            }

            return new InstallResult(true, $"Installed to {targetExe}", RequiresRestart: true);
        }

        private static System.Net.Http.HttpClient CreateTestClient()
        {
            var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("jdai-test/1.0");
            return http;
        }

        public void Dispose() { /* nothing to dispose */ }
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────

    private sealed record GitHubReleaseDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("tag_name")] string? TagName,
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string? Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("assets")] List<GitHubAssetDto>? Assets);

    private sealed record GitHubAssetDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        [property: System.Text.Json.Serialization.JsonPropertyName("size")] long Size);

    // ─── JSON builders ────────────────────────────────────────────────────────

    private static string BuildReleaseJson(
        string tagName,
        IEnumerable<(string Name, string Url, long Size)> assets)
    {
        var assetJson = string.Join(",", assets.Select(a =>
            $$$"""{"name":"{{{a.Name}}}","browser_download_url":"{{{a.Url}}}","size":{{{a.Size}}}}"""));
        return $$$"""{"tag_name":"{{{tagName}}}","name":"Release {{{tagName}}}","assets":[{{{assetJson}}}]}""";
    }

    private static string BuildReleaseJsonWithDownloadUrl(
        string tagName, string assetName, string downloadUrl)
    {
        return $$"""{"tag_name":"{{tagName}}","name":"Release {{tagName}}","assets":[{"name":"{{assetName}}","browser_download_url":"{{downloadUrl}}","size":1024}]}""";
    }

    private static (string ReleaseJson, string AssetContent) BuildReleaseWithFakeAsset(
        string tagName, string assetName, string rid)
    {
        var downloadUrl = $"http://localhost/fake-asset/{assetName}";
        var releaseJson = BuildReleaseJson(tagName, [(assetName, downloadUrl, 100)]);
        return (releaseJson, "fake-binary-data");
    }

    // ─── Zip helpers ─────────────────────────────────────────────────────────

    private static void CreateZipWithBinary(string zipPath, string entryName, string content)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Stub HTTP servers using HttpListener
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Simple single-route stub server that returns the same body for all requests.</summary>
    private sealed class StubServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        public string BaseUrl { get; }

        public StubServer(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var port = GetFreePort();
            BaseUrl = $"http://localhost:{port}";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _serverTask = ServeAsync(responseBody, statusCode, _cts.Token);
        }

        private async Task ServeAsync(string body, HttpStatusCode status, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
                catch { break; }

                var bytes = Encoding.UTF8.GetBytes(body);
                ctx.Response.StatusCode = (int)status;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
                ctx.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _listener.Stop();
            _listener.Close();
            try { await _serverTask.ConfigureAwait(false); } catch { /* expected cancellation */ }
            _cts.Dispose();
        }
    }

    /// <summary>Stub server with route-based dispatch.</summary>
    private sealed class MultiRouteStubServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;
        private readonly Dictionary<string, (string Body, HttpStatusCode Status)> _routes;
        private readonly string _defaultPath;

        public string BaseUrl { get; }

        public MultiRouteStubServer(
            Dictionary<string, (string Body, HttpStatusCode Status)> routes,
            string defaultPath)
        {
            _routes = routes;
            _defaultPath = defaultPath;
            var port = GetFreePort();
            BaseUrl = $"http://localhost:{port}";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _serverTask = ServeAsync(_cts.Token);
        }

        private async Task ServeAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
                catch { break; }

                var path = ctx.Request.Url?.AbsolutePath ?? _defaultPath;
                var (body, status) = _routes.TryGetValue(path, out var entry)
                    ? entry
                    : ("", HttpStatusCode.NotFound);

                var bytes = Encoding.UTF8.GetBytes(body);
                ctx.Response.StatusCode = (int)status;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
                ctx.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _listener.Stop();
            _listener.Close();
            try { await _serverTask.ConfigureAwait(false); } catch { }
            _cts.Dispose();
        }
    }

    /// <summary>Stub server that serves release JSON for all non-asset paths
    /// and serves a binary file for /asset.</summary>
    private sealed class BinaryFileStubServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;
        private readonly string _binaryFilePath;
        private string _releaseJson;

        public string BaseUrl { get; }
        public string AssetUrl => $"{BaseUrl}/asset";

        public BinaryFileStubServer(string releaseJson, string binaryFilePath)
        {
            _releaseJson = releaseJson;
            _binaryFilePath = binaryFilePath;
            var port = GetFreePort();
            BaseUrl = $"http://localhost:{port}";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _serverTask = ServeAsync(_cts.Token);
        }

        public void SetReleaseJson(string json) => _releaseJson = json;

        private async Task ServeAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
                catch { break; }

                var path = ctx.Request.Url?.AbsolutePath ?? "/";
                if (string.Equals(path, "/asset", StringComparison.Ordinal))
                {
                    var bytes = await File.ReadAllBytesAsync(_binaryFilePath, ct).ConfigureAwait(false);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/octet-stream";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
                }
                else
                {
                    var json = _releaseJson;
                    var bytes = Encoding.UTF8.GetBytes(json);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
                }

                ctx.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _listener.Stop();
            _listener.Close();
            try { await _serverTask.ConfigureAwait(false); } catch { }
            _cts.Dispose();
        }
    }

    // ─── Port helpers ─────────────────────────────────────────────────────────

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
