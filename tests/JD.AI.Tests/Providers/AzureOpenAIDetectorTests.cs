using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="AzureOpenAIDetector"/>.
/// </summary>
public sealed class AzureOpenAIDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public AzureOpenAIDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    // ── DetectAsync — no credentials ───────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithNoCredentials_ReturnsUnavailable()
    {
        var detector = new AzureOpenAIDetector(_config);

        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
        result.Name.Should().Be("Azure OpenAI");
    }

    [Fact]
    public async Task DetectAsync_WithOnlyApiKey_ReturnsUnavailable()
    {
        // API key alone is not sufficient; endpoint is required
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_WithOnlyEndpoint_ReturnsUnavailable()
    {
        // Endpoint alone is not sufficient; API key is required
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    // ── DetectAsync — with credentials, no deployments ────────────────────────

    [Fact]
    public async Task DetectAsync_WithKeyAndEndpoint_NoDeployments_ReturnsThreeDefaults()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().HaveCount(3);
        result.Models.Should().Contain(m => m.Id == "gpt-4o");
        result.Models.Should().Contain(m => m.Id == "gpt-4o-mini");
        result.Models.Should().Contain(m => m.Id == "gpt-4");
    }

    [Fact]
    public async Task DetectAsync_WithKeyAndEndpoint_NoDeployments_ModelsHaveAzurePrefix()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().AllSatisfy(m =>
            m.DisplayName.Should().StartWith("azure:"));
    }

    // ── DetectAsync — with custom deployments ──────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithKeyAndEndpoint_WithDeployments_ReturnsParsedModels()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");
        await _store.SetAsync("jdai:provider:azure-openai:deployments", "my-gpt4,my-gpt35");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().HaveCount(2);
        result.Models.Should().Contain(m => m.Id == "my-gpt4");
        result.Models.Should().Contain(m => m.Id == "my-gpt35");
    }

    [Fact]
    public async Task DetectAsync_WithKeyAndEndpoint_WithDeployments_ModelsHaveAzurePrefix()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");
        await _store.SetAsync("jdai:provider:azure-openai:deployments", "my-deployment");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.Models[0].DisplayName.Should().Be("azure:my-deployment");
    }

    // ── DetectAsync — whitespace handling ──────────────────────────────────────

    [Fact]
    public async Task DetectAsync_DeploymentsCsvTrimmed_HandlesWhitespace()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");
        // Deployments with leading/trailing whitespace
        await _store.SetAsync("jdai:provider:azure-openai:deployments", "  my-gpt4  ,  my-gpt35  ");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().HaveCount(2);
        result.Models[0].Id.Should().Be("my-gpt4");
        result.Models[1].Id.Should().Be("my-gpt35");
    }

    [Fact]
    public async Task DetectAsync_DeploymentsEmptyStringItems_FilteredOut()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");
        // Deployments with empty entries between commas
        await _store.SetAsync("jdai:provider:azure-openai:deployments", "my-gpt4,,my-gpt35");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().HaveCount(2);
        result.Models.Should().NotContain(m => string.IsNullOrEmpty(m.Id));
    }

    // ── ProviderName ───────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ReturnsAzureOpenAI()
    {
        var detector = new AzureOpenAIDetector(_config);

        detector.ProviderName.Should().Be("Azure OpenAI");
    }

    // ── BuildKernel ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildKernel_BeforeDetect_ThrowsInvalidOperationException()
    {
        // BuildKernel should throw if endpoint and key are not set
        var detector = new AzureOpenAIDetector(_config);
        var model = new ProviderModelInfo("my-deployment", "azure:my-deployment", "Azure OpenAI");

        var act = () => detector.BuildKernel(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint*");
    }

    [Fact]
    public async Task BuildKernel_AfterDetect_ReturnsNonNullKernel()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");

        var detector = new AzureOpenAIDetector(_config);
        await detector.DetectAsync();

        var model = new ProviderModelInfo("gpt-4o", "azure:gpt-4o", "Azure OpenAI");
        var kernel = detector.BuildKernel(model);

        kernel.Should().NotBeNull();
        var chatServices = kernel.GetAllServices<IChatCompletionService>();
        chatServices.Should().NotBeEmpty();
    }

    // ── StatusMessage ──────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusMessage_ContainsDeploymentCount()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");
        await _store.SetAsync("jdai:provider:azure-openai:deployments", "dep1,dep2,dep3");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.StatusMessage.Should().Contain("3 deployment(s)");
    }

    [Fact]
    public async Task StatusMessage_ContainsDefaultCountWhenNoDeploymentsConfigured()
    {
        await _store.SetAsync("jdai:provider:azure-openai:apikey", "test-key");
        await _store.SetAsync("jdai:provider:azure-openai:endpoint", "https://test.openai.azure.com/");

        var detector = new AzureOpenAIDetector(_config);
        var result = await detector.DetectAsync();

        result.StatusMessage.Should().Contain("3 deployment(s)");
    }
}
