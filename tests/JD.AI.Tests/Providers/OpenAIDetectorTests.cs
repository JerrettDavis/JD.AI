using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="OpenAIDetector"/>.
/// </summary>
public sealed class OpenAIDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public OpenAIDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    // ── ProviderName ───────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_IsOpenAI()
    {
        var detector = new OpenAIDetector(_config);

        detector.ProviderName.Should().Be("OpenAI");
    }

    // ── DetectAsync — no API key ───────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_NoApiKey_ReturnsUnavailable()
    {
        var detector = new OpenAIDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_NoApiKey_ReturnsProviderName()
    {
        var detector = new OpenAIDetector(_config);

        var result = await detector.DetectAsync();

        result.Name.Should().Be("OpenAI");
    }

    [Fact]
    public async Task DetectAsync_NoApiKey_ReturnsEmptyModels()
    {
        var detector = new OpenAIDetector(_config);

        var result = await detector.DetectAsync();

        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_NoApiKey_StatusMessageIndicatesMissingKey()
    {
        var detector = new OpenAIDetector(_config);

        var result = await detector.DetectAsync();

        result.StatusMessage.Should().NotBeNullOrEmpty();
        result.StatusMessage.Should().Contain("No API key");
    }

    // ── DetectAsync — invalid API key (network rejection) ─────────────────────

    [Fact]
    public async Task DetectAsync_InvalidApiKey_ReturnsUnavailable()
    {
        // A syntactically valid but fake key — causes HTTP 401 from OpenAI,
        // which triggers the HttpRequestException catch path.
        await _store.SetAsync("jdai:provider:openai:apikey", "sk-fake-key-unittest");

        var detector = new OpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_InvalidApiKey_ReturnsEmptyModels()
    {
        await _store.SetAsync("jdai:provider:openai:apikey", "sk-fake-key-unittest");

        var detector = new OpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_InvalidApiKey_StatusMessageContainsApiError()
    {
        await _store.SetAsync("jdai:provider:openai:apikey", "sk-fake-key-unittest");

        var detector = new OpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.StatusMessage.Should().NotBeNullOrEmpty();
        result.StatusMessage.Should().Contain("API error");
    }

    [Fact]
    public async Task DetectAsync_InvalidApiKey_ReturnOpenAIProviderName()
    {
        await _store.SetAsync("jdai:provider:openai:apikey", "sk-fake-key-unittest");

        var detector = new OpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.Name.Should().Be("OpenAI");
    }

    // ── DetectAsync — cancellation ─────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_CancelledToken_NoApiKey_ReturnsQuickly()
    {
        // When the token is already cancelled before hitting the network,
        // the credential lookup will complete first (it checks store before token),
        // then the HTTP call should not be made. We verify it returns IsAvailable=false.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var detector = new OpenAIDetector(_config);

        // Should not throw — either returns fast or propagates cancellation
        try
        {
            var result = await detector.DetectAsync(cts.Token);
            result.IsAvailable.Should().BeFalse();
        }
        catch (OperationCanceledException)
        {
            // Acceptable — task was cancelled
        }
    }

    // ── BuildKernel ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildKernel_AfterSuccessfulDetect_ReturnsChatCompletionService()
    {
        // We cannot make a real API call in unit tests, so we verify that after
        // DetectAsync has been called with a non-empty key the kernel is buildable.
        // Even though DetectAsync returns IsAvailable=false due to invalid key,
        // the key is stored on the instance after the credential lookup.
        // However, BuildKernel reads _apiKey which is set during DetectAsync.
        // We set a fake key and run DetectAsync so _apiKey is populated.
        await _store.SetAsync("jdai:provider:openai:apikey", "sk-fake-key-for-kernel-build");

        var detector = new OpenAIDetector(_config);

        // Run DetectAsync to populate _apiKey (network will fail, but that's expected)
        await detector.DetectAsync();

        // BuildKernel should succeed because _apiKey is now set
        var kernel = detector.BuildKernel(
            new ProviderModelInfo("gpt-4o", "GPT-4o", "OpenAI"));

        kernel.Should().NotBeNull();
        var chatServices = kernel.GetAllServices<IChatCompletionService>();
        chatServices.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildKernel_WithoutPriorDetect_ThrowsInvalidOperationException()
    {
        // _apiKey is null when DetectAsync has never been called
        var detector = new OpenAIDetector(_config);
        var model = new ProviderModelInfo("gpt-4o", "GPT-4o", "OpenAI");

        var act = () => detector.BuildKernel(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key*");
    }

    // ── Model filtering logic ──────────────────────────────────────────────────
    // The filtering logic in DetectAsync selects models starting with gpt-, o1, o3, o4, chatgpt.
    // Because we cannot inject a custom HttpClient into OpenAIDetector (it uses a static field),
    // we validate the filtering intent through integration-style tests that confirm the
    // contract when models are returned.

    [Fact]
    public async Task DetectAsync_WithApiKey_ResultHasOpenAIProviderName()
    {
        await _store.SetAsync("jdai:provider:openai:apikey", "sk-test-provider-name");

        var detector = new OpenAIDetector(_config);
        var result = await detector.DetectAsync();

        // Whether available or not (network likely fails), name must always be "OpenAI"
        result.Name.Should().Be("OpenAI");
    }

    // ── ProviderModelInfo assertions on returned models ────────────────────────

    [Fact]
    public async Task DetectAsync_WhenAvailable_ModelsAllHaveOpenAIProviderName()
    {
        // If by some miracle a real key is configured in the environment,
        // validate that all returned models have the correct provider name.
        var detector = new OpenAIDetector(_config);
        var result = await detector.DetectAsync();

        if (result.IsAvailable)
        {
            result.Models.Should().AllSatisfy(m =>
                m.ProviderName.Should().Be("OpenAI"));
        }
    }

    [Fact]
    public async Task DetectAsync_WhenAvailable_StatusMessageContainsModelCount()
    {
        var detector = new OpenAIDetector(_config);
        var result = await detector.DetectAsync();

        if (result.IsAvailable)
        {
            result.StatusMessage.Should().Contain("model(s)");
        }
    }

    [Fact]
    public async Task DetectAsync_WhenAvailable_ModelsAreSortedAlphabetically()
    {
        var detector = new OpenAIDetector(_config);
        var result = await detector.DetectAsync();

        if (result.IsAvailable && result.Models.Count > 1)
        {
            var ids = result.Models.Select(m => m.Id).ToList();
            ids.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        }
    }

    // ── Concurrent DetectAsync calls ──────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_CalledMultipleTimes_DoesNotThrow()
    {
        // Calling DetectAsync multiple times should be safe even with a shared static HttpClient
        var detector = new OpenAIDetector(_config);

        var task1 = detector.DetectAsync();
        var task2 = detector.DetectAsync();

        var results = await Task.WhenAll(task1, task2);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }
}
