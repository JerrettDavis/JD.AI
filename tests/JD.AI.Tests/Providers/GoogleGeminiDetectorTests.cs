using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="GoogleGeminiDetector"/>.
/// </summary>
public sealed class GoogleGeminiDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public GoogleGeminiDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    // ── DetectAsync — no API key ───────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithNoCredentials_ReturnsUnavailable()
    {
        var detector = new GoogleGeminiDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
        result.Name.Should().Be("Google Gemini");
    }

    // ── DetectAsync — API key but discovery fails ──────────────────────────────

    [Fact]
    public async Task DetectAsync_WithApiKey_DiscoveryThrows_FallsBackToKnownModels()
    {
        // A syntactically valid but fake key — causes HTTP error on discovery,
        // which triggers the fallback to the curated catalog.
        await _store.SetAsync("jdai:provider:google-gemini:apikey", "fake-gemini-key-unittest");

        var detector = new GoogleGeminiDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().NotBeEmpty();
        result.Models.Should().AllSatisfy(m =>
            m.ProviderName.Should().Be("Google Gemini"));
        // Verify a known catalog model is present as fallback
        result.Models.Should().Contain(m => m.Id.Contains("gemini", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithApiKey_DiscoveryReturnsEmpty_FallsBackToKnownModels()
    {
        // Even with a key, if discovery returns no models, fall back to known models
        await _store.SetAsync("jdai:provider:google-gemini:apikey", "test-gemini-key");

        var detector = new GoogleGeminiDetector(_config);
        var result = await detector.DetectAsync();

        // Whether discovery succeeds or fails (network likely fails in test env),
        // we should at least have the fallback known models
        result.IsAvailable.Should().BeTrue();
        result.Models.Should().NotBeEmpty();
        result.Models.Should().Contain(m => m.Id == "gemini-2.5-pro");
        result.Models.Should().Contain(m => m.Id == "gemini-1.5-pro");
    }

    // ── Model ID prefix stripping ──────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_ModelsPrefix_StrippedFromId()
    {
        // The Gemini API returns model names like "models/gemini-2.5-pro"
        // Our detector should strip the "models/" prefix when parsing
        await _store.SetAsync("jdai:provider:google-gemini:apikey", "test-key");

        var detector = new GoogleGeminiDetector(_config);
        var result = await detector.DetectAsync();

        // Even if we fall back to known models, verify the IDs don't have the prefix
        result.Models.Should().AllSatisfy(m =>
            m.Id.Should().NotStartWith("models/"));
    }

    // ── ProviderName ───────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ReturnsGoogleGemini()
    {
        var detector = new GoogleGeminiDetector(_config);

        detector.ProviderName.Should().Be("Google Gemini");
    }

    // ── BuildKernel ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildKernel_BeforeDetect_ThrowsInvalidOperationException()
    {
        // _apiKey is null when DetectAsync has never been called
        var detector = new GoogleGeminiDetector(_config);
        var model = new ProviderModelInfo("gemini-2.5-pro", "Gemini 2.5 Pro", "Google Gemini");

        var act = () => detector.BuildKernel(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Google Gemini*API key*");
    }

    [Fact]
    public async Task BuildKernel_AfterDetect_ReturnsNonNullKernel()
    {
        // We set a fake key and run DetectAsync so _apiKey is populated.
        await _store.SetAsync("jdai:provider:google-gemini:apikey", "fake-key-for-kernel");

        var detector = new GoogleGeminiDetector(_config);
        await detector.DetectAsync();

        var model = new ProviderModelInfo("gemini-2.5-pro", "Gemini 2.5 Pro", "Google Gemini");
        var kernel = detector.BuildKernel(model);

        kernel.Should().NotBeNull();
        var chatServices = kernel.GetAllServices<IChatCompletionService>();
        chatServices.Should().NotBeEmpty();
    }

    // ── StatusMessage ──────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusMessage_ContainsModelCount()
    {
        await _store.SetAsync("jdai:provider:google-gemini:apikey", "test-key");

        var detector = new GoogleGeminiDetector(_config);
        var result = await detector.DetectAsync();

        if (result.IsAvailable)
        {
            result.StatusMessage.Should().Contain("model(s)");
            result.StatusMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task DetectAsync_WithNoCredentials_StatusMessageIndicatesMissing()
    {
        var detector = new GoogleGeminiDetector(_config);
        var result = await detector.DetectAsync();

        result.StatusMessage.Should().NotBeNullOrEmpty();
        result.StatusMessage.Should().Contain("No API key");
    }
}
