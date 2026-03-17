using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tests.Providers;

public sealed class OllamaDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public OllamaDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void ProviderName_IsOllama()
    {
        var detector = new OllamaDetector();
        detector.ProviderName.Should().Be("Ollama");
    }

    [Fact]
    public async Task BuildKernel_NamedInstanceModel_LoadsConfiguredEndpointWithoutPriorDetect()
    {
        await _store.SetAsync("jdai:provider:ollama:gpu:endpoint", "http://10.0.0.55:11434");

        var detector = new OllamaDetector(_config, defaultEndpoint: "http://localhost:11434");

        var kernel = detector.BuildKernel(
            new ProviderModelInfo("gpu/qwen3:8b", "[gpu] qwen3:8b", "Ollama"));

        var chatServices = kernel.GetAllServices<IChatCompletionService>();
        chatServices.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildKernel_UnknownAliasPrefix_FallsBackToLocalModelParsing()
    {
        var detector = new OllamaDetector(_config, defaultEndpoint: "http://localhost:11434");

        var kernel = detector.BuildKernel(
            new ProviderModelInfo("does-not-exist/qwen3:8b", "[x] qwen3:8b", "Ollama"));

        var chatServices = kernel.GetAllServices<IChatCompletionService>();
        chatServices.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildKernel_UnprefixedModel_UsesLocalInstance()
    {
        var detector = new OllamaDetector(endpoint: "http://localhost:11434");

        var kernel = detector.BuildKernel(
            new ProviderModelInfo("qwen3:8b", "qwen3:8b", "Ollama"));

        var chatServices = kernel.GetAllServices<IChatCompletionService>();
        chatServices.Should().NotBeEmpty();
    }
}
