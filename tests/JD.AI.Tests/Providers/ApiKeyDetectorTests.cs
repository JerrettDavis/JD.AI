using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Providers;

public sealed class ApiKeyDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public ApiKeyDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task OpenAIDetector_NoApiKey_ReturnsUnavailable()
    {
        var detector = new OpenAIDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task AnthropicDetector_NoApiKey_ReturnsUnavailable()
    {
        var detector = new AnthropicDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task GoogleGeminiDetector_NoApiKey_ReturnsUnavailable()
    {
        var detector = new GoogleGeminiDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task MistralDetector_NoApiKey_ReturnsUnavailable()
    {
        var detector = new MistralDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task HuggingFaceDetector_NoApiKey_ReturnsUnavailable()
    {
        var detector = new HuggingFaceDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task AzureOpenAIDetector_NoApiKey_ReturnsUnavailable()
    {
        var detector = new AzureOpenAIDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task AmazonBedrockDetector_NoCredentials_ReturnsUnavailable()
    {
        var detector = new AmazonBedrockDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenAICompatibleDetector_NoEndpoints_ReturnsUnavailable()
    {
        var detector = new OpenAICompatibleDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenRouterDetector_NoApiKey_ReturnsUnavailable()
    {
        var detector = new OpenRouterDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenRouterDetector_WithApiKey_ReturnsKnownModels()
    {
        await _store.SetAsync("jdai:provider:openrouter:apikey", "sk-or-test-key");

        var detector = new OpenRouterDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().HaveCountGreaterThanOrEqualTo(5);
        result.Models.Should().
            Contain(m =>
                m.ProviderName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase));
        result.Models.Should().Contain(m => m.Id == "anthropic/claude-sonnet-4");
        result.Models.Should().Contain(m => m.Id == "openai/gpt-4.1");
    }

    [Fact]
    public async Task AnthropicDetector_WithApiKey_ReturnsModels()
    {
        await _store.SetAsync("jdai:provider:anthropic:apikey", "sk-ant-test-key");

        var detector = new AnthropicDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().NotBeEmpty();
        result.Models.Should().
            Contain(m =>
                m.ProviderName.Equals("Anthropic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GoogleGeminiDetector_WithApiKey_ReturnsModels()
    {
        await _store.SetAsync("jdai:provider:google-gemini:apikey", "test-gemini-key");

        var detector = new GoogleGeminiDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().NotBeEmpty();
        result.Models.Should().
            Contain(m =>
                m.ProviderName.Equals("Google Gemini", StringComparison.OrdinalIgnoreCase));
    }
}
