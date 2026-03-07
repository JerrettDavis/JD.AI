using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="MistralDetector"/>.
/// </summary>
public sealed class MistralDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public MistralDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    // ── ProviderName ───────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_IsMistral()
    {
        var detector = new MistralDetector(_config);

        detector.ProviderName.Should().Be("Mistral");
    }

    // ── DetectAsync — no API key ───────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_NoApiKey_ReturnsUnavailable()
    {
        var detector = new MistralDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_NoApiKey_ReturnsProviderName()
    {
        var detector = new MistralDetector(_config);

        var result = await detector.DetectAsync();

        result.Name.Should().Be("Mistral");
    }

    [Fact]
    public async Task DetectAsync_NoApiKey_ReturnsEmptyModels()
    {
        var detector = new MistralDetector(_config);

        var result = await detector.DetectAsync();

        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_NoApiKey_StatusMessageIndicatesMissingKey()
    {
        var detector = new MistralDetector(_config);

        var result = await detector.DetectAsync();

        result.StatusMessage.Should().NotBeNullOrEmpty();
        result.StatusMessage.Should().Contain("No API key");
    }

    // ── DetectAsync — invalid API key (falls back to known catalog) ────────────

    [Fact]
    public async Task DetectAsync_InvalidApiKey_IsAvailable()
    {
        // An invalid key causes an HTTP error in DiscoverModelsAsync.
        // ApiKeyProviderDetectorBase catches the exception and falls back to KnownModels.
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-mistral-api-key-unittest");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        // With any non-empty key, base class returns IsAvailable=true with fallback catalog
        result.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task DetectAsync_InvalidApiKey_FallsBackToKnownModelsCatalog()
    {
        // A bad key triggers a network error in DiscoverModelsAsync;
        // the base class catches and falls back to KnownModels.
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-mistral-api-key-unittest");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().NotBeEmpty();
        result.Models.Should().AllSatisfy(m =>
            m.ProviderName.Should().Be("Mistral"));
    }

    [Fact]
    public async Task DetectAsync_InvalidApiKey_ReturnsMistralProviderName()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-mistral-api-key-unittest");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Name.Should().Be("Mistral");
    }

    [Fact]
    public async Task DetectAsync_InvalidApiKey_StatusMessageContainsAuthenticated()
    {
        // Base class uses BuildAuthenticatedStatus when IsAvailable=true
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-mistral-api-key-unittest");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.StatusMessage.Should().Contain("Authenticated");
    }

    [Fact]
    public async Task DetectAsync_InvalidApiKey_StatusMessageContainsModelCount()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-mistral-api-key-unittest");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.StatusMessage.Should().Contain("model(s)");
    }

    // ── Known models catalog content ───────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithFallback_ContainsMistralLargeModel()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().Contain(m =>
            m.Id.Equals("mistral-large-latest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithFallback_ContainsMistralSmallModel()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().Contain(m =>
            m.Id.Equals("mistral-small-latest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithFallback_ContainsCodestralModel()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().Contain(m =>
            m.Id.Equals("codestral-latest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithFallback_ContainsOpenMistralNemoModel()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().Contain(m =>
            m.Id.Equals("open-mistral-nemo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithFallback_ContainsMistralMediumModel()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().Contain(m =>
            m.Id.Equals("mistral-medium-latest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithFallback_ContainsMinstral8BModel()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().Contain(m =>
            m.Id.Equals("ministral-8b-latest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithFallback_HasAtLeastSixModels()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().HaveCountGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task DetectAsync_WithFallback_AllModelsHaveMistralProviderName()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().AllSatisfy(m =>
            m.ProviderName.Should().Be("Mistral"));
    }

    [Fact]
    public async Task DetectAsync_WithFallback_KnownModelsDisplayNamesAreNonEmpty()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().AllSatisfy(m =>
            m.DisplayName.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task DetectAsync_WithFallback_ModelIdsAreNonEmpty()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().AllSatisfy(m =>
            m.Id.Should().NotBeNullOrWhiteSpace());
    }

    // ── Known catalog display names ────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithFallback_MistralLargeHasExpectedDisplayName()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        var model = result.Models.FirstOrDefault(m =>
            m.Id.Equals("mistral-large-latest", StringComparison.OrdinalIgnoreCase));

        model.Should().NotBeNull();
        model!.DisplayName.Should().Be("Mistral Large");
    }

    [Fact]
    public async Task DetectAsync_WithFallback_CodestralHasExpectedDisplayName()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        var model = result.Models.FirstOrDefault(m =>
            m.Id.Equals("codestral-latest", StringComparison.OrdinalIgnoreCase));

        model.Should().NotBeNull();
        model!.DisplayName.Should().Be("Codestral");
    }

    [Fact]
    public async Task DetectAsync_WithFallback_MistralNemoHasExpectedDisplayName()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        var model = result.Models.FirstOrDefault(m =>
            m.Id.Equals("open-mistral-nemo", StringComparison.OrdinalIgnoreCase));

        model.Should().NotBeNull();
        model!.DisplayName.Should().Be("Mistral Nemo");
    }

    // ── FormatName logic (observed through discovered model display names) ─────
    // FormatName converts "mistral-large-latest" -> "mistral large" (hyphens to spaces,
    // "latest" removed). We test this via the discovered path by crafting a scenario
    // where the API response is parsed. Since we cannot intercept the private HttpClient,
    // we validate the contract on known models and confirm the private method behavior
    // through the public API when real data is returned.

    [Fact]
    public async Task DetectAsync_WhenModelsDiscovered_DisplayNamesDoNotContainHyphens()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        // All display names (whether from catalog or discovered) should not use
        // raw hyphen-separated IDs — they should be formatted as human-readable names.
        if (result.IsAvailable)
        {
            result.Models.Should().AllSatisfy(m =>
                m.DisplayName.Should().NotContain("-"));
        }
    }

    [Fact]
    public async Task DetectAsync_WhenModelsDiscovered_DisplayNamesDoNotContainWordLatest()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        if (result.IsAvailable)
        {
            result.Models.Should().AllSatisfy(m =>
                m.DisplayName.Should()
                    .NotContainEquivalentOf("latest"));
        }
    }

    // ── BuildKernel ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildKernel_AfterDetect_ReturnsChatCompletionService()
    {
        // After DetectAsync runs, _apiKey is populated, enabling BuildKernel.
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key-for-kernel-test");

        var detector = new MistralDetector(_config);
        await detector.DetectAsync();

        var model = new ProviderModelInfo("mistral-large-latest", "Mistral Large", "Mistral");
        var kernel = detector.BuildKernel(model);

        kernel.Should().NotBeNull();
        var chatServices = kernel.GetAllServices<IChatCompletionService>();
        chatServices.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildKernel_WithoutPriorDetect_ThrowsInvalidOperationException()
    {
        // _apiKey is null when DetectAsync has never been called;
        // ApiKeyProviderDetectorBase.BuildKernel calls ApiKeyOrThrow()
        var detector = new MistralDetector(_config);
        var model = new ProviderModelInfo("mistral-large-latest", "Mistral Large", "Mistral");

        var act = () => detector.BuildKernel(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key*");
    }

    [Fact]
    public async Task BuildKernel_WithDifferentModels_ProducesKernelForEach()
    {
        await _store.SetAsync("jdai:provider:mistral:apikey", "fake-key-multi");

        var detector = new MistralDetector(_config);
        await detector.DetectAsync();

        var models = new[]
        {
            new ProviderModelInfo("mistral-large-latest", "Mistral Large", "Mistral"),
            new ProviderModelInfo("mistral-small-latest", "Mistral Small", "Mistral"),
            new ProviderModelInfo("codestral-latest", "Codestral", "Mistral"),
        };

        foreach (var model in models)
        {
            var kernel = detector.BuildKernel(model);
            kernel.Should().NotBeNull($"BuildKernel should succeed for model '{model.Id}'");
        }
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_CancelledToken_NoApiKey_DoesNotHang()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var detector = new MistralDetector(_config);

        try
        {
            var result = await detector.DetectAsync(cts.Token);
            result.IsAvailable.Should().BeFalse();
        }
        catch (OperationCanceledException)
        {
            // Acceptable — task was cancelled before completion
        }
    }

    // ── Multiple successive calls ─────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_CalledMultipleTimes_ReturnsConsistentResults()
    {
        var detector = new MistralDetector(_config);

        var result1 = await detector.DetectAsync();
        var result2 = await detector.DetectAsync();

        result1.IsAvailable.Should().Be(result2.IsAvailable);
        result1.Name.Should().Be(result2.Name);
    }

    // ── Whitespace-only API key (treated as missing by base class) ────────────

    [Fact]
    public async Task DetectAsync_WhitespaceApiKey_ReturnsUnavailable()
    {
        // The base class uses IsNullOrWhiteSpace to check the key,
        // so a whitespace-only key is treated as missing.
        await _store.SetAsync("jdai:provider:mistral:apikey", "   ");

        var detector = new MistralDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }
}
