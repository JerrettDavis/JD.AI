using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Providers;

public sealed class HuggingFaceDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public HuggingFaceDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task DetectAsync_NoApiKey_ReturnsUnavailable()
    {
        var detector = new HuggingFaceDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_WithConfiguredKey_ModelsAreChatOnly()
    {
        // With a fake key, detector falls back to known catalog.
        // With a valid key, discovered models are still marked Chat-only by design.
        await _store.SetAsync("jdai:provider:huggingface:apikey", "fake-hf-key");

        var detector = new HuggingFaceDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().NotBeEmpty();
        result.Models.Should().OnlyContain(m => m.Capabilities == ModelCapabilities.Chat);
    }
}
