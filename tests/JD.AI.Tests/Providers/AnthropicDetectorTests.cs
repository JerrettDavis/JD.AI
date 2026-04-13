using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="AnthropicDetector"/>.
/// </summary>
public sealed class AnthropicDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public AnthropicDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    // ── DetectAsync — no API key ───────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithNoCredentials_ReturnsUnavailable()
    {
        var detector = new AnthropicDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
        result.Name.Should().Be("Anthropic");
    }

    // ── DetectAsync — API key but discovery fails ──────────────────────────────

    [Fact]
    public async Task DetectAsync_WithApiKey_DiscoveryThrows_FallsBackToKnownModels()
    {
        // A syntactically valid but fake key — causes HTTP error on discovery,
        // which triggers the fallback to the curated catalog.
        await _store.SetAsync("jdai:provider:anthropic:apikey", "sk-ant-fake-key-unittest");

        var detector = new AnthropicDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().NotBeEmpty();
        result.Models.Should().AllSatisfy(m =>
            m.ProviderName.Should().Be("Anthropic"));
        // Verify a known catalog model is present as fallback
        result.Models.Should().Contain(m => m.Id.Contains("claude", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithApiKey_DiscoveryReturnsEmpty_FallsBackToKnownModels()
    {
        // Even with a key, if discovery returns no models, fall back to known models
        await _store.SetAsync("jdai:provider:anthropic:apikey", "sk-ant-test-key");

        var detector = new AnthropicDetector(_config);
        var result = await detector.DetectAsync();

        // Whether discovery succeeds or fails (network likely fails in test env),
        // we should at least have the fallback known models
        result.IsAvailable.Should().BeTrue();
        result.Models.Should().NotBeEmpty();
        result.Models.Should().Contain(m => m.Id == "claude-opus-4-20250514");
        result.Models.Should().Contain(m => m.Id == "claude-3-5-sonnet-20241022");
    }

    // ── ProviderName ───────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ReturnsAnthropic()
    {
        var detector = new AnthropicDetector(_config);

        detector.ProviderName.Should().Be("Anthropic");
    }

    // ── BuildKernel ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildKernel_BeforeDetect_ThrowsInvalidOperationException()
    {
        // _apiKey is null when DetectAsync has never been called
        var detector = new AnthropicDetector(_config);
        var model = new ProviderModelInfo("claude-opus-4-20250514", "Claude Opus 4", "Anthropic");

        var act = () => detector.BuildKernel(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Anthropic*API key*");
    }

    [Fact]
    public async Task BuildKernel_AfterDetectWithKey_ReturnsNonNullKernel()
    {
        // We set a fake key and run DetectAsync so _apiKey is populated.
        // Then BuildKernel should succeed even if discovery failed.
        await _store.SetAsync("jdai:provider:anthropic:apikey", "sk-ant-fake-key-for-kernel");

        var detector = new AnthropicDetector(_config);
        await detector.DetectAsync();

        var model = new ProviderModelInfo("claude-opus-4-20250514", "Claude Opus 4", "Anthropic");
        var kernel = detector.BuildKernel(model);

        kernel.Should().NotBeNull();
        var chatServices = kernel.GetAllServices<IChatCompletionService>();
        chatServices.Should().NotBeEmpty();
    }

    // ── StatusMessage ──────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusMessage_ContainsModelCount()
    {
        await _store.SetAsync("jdai:provider:anthropic:apikey", "sk-ant-test-key");

        var detector = new AnthropicDetector(_config);
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
        var detector = new AnthropicDetector(_config);
        var result = await detector.DetectAsync();

        result.StatusMessage.Should().NotBeNullOrEmpty();
        result.StatusMessage.Should().Contain("No API key");
    }
}
