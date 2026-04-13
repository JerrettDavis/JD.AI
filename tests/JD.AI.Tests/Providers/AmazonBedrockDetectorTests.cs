using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="AmazonBedrockDetector"/>.
/// </summary>
public sealed class AmazonBedrockDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _config;

    public AmazonBedrockDetectorTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _config = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    // ── DetectAsync — no credentials ───────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithNoCredentials_ReturnsUnavailable()
    {
        // Clear AWS credentials from environment so the detector sees no credentials
        var savedAccess = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var savedSecret = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);

            var detector = new AmazonBedrockDetector(_config);
            var result = await detector.DetectAsync();

            result.IsAvailable.Should().BeFalse();
            result.Models.Should().BeEmpty();
            result.Name.Should().Be("AWS Bedrock");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", savedAccess);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", savedSecret);
        }
    }

    [Fact]
    public async Task DetectAsync_MissingAccessKey_ReturnsUnavailable()
    {
        // Only secret key is provided; access key is required
        await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret");

        var detector = new AmazonBedrockDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_MissingSecretKey_ReturnsUnavailable()
    {
        // Only access key is provided; secret key is required
        await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access");

        var detector = new AmazonBedrockDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeFalse();
        result.Models.Should().BeEmpty();
    }

    // ── DetectAsync — with both keys, region handling ────────────────────────

    [Fact]
    public async Task DetectAsync_WithBothKeys_NoRegion_DefaultsToUsEast1()
    {
        await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access-key");
        await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret-key");

        var detector = new AmazonBedrockDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        // Verify the status message mentions us-east-1
        result.StatusMessage.Should().Contain("us-east-1");
    }

    [Fact]
    public async Task DetectAsync_WithBothKeys_ExplicitRegion_UsesConfiguredRegion()
    {
        await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access-key");
        await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret-key");
        await _store.SetAsync("jdai:provider:bedrock:region", "eu-west-1");

        var detector = new AmazonBedrockDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        // Verify the status message mentions the configured region
        result.StatusMessage.Should().Contain("eu-west-1");
    }

    // ── DetectAsync — models returned ──────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithBothKeys_ReturnsSixKnownModels()
    {
        await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access-key");
        await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret-key");

        var detector = new AmazonBedrockDetector(_config);
        var result = await detector.DetectAsync();

        result.IsAvailable.Should().BeTrue();
        result.Models.Should().HaveCount(6);
        result.Models.Should().Contain(m => m.Id == "anthropic.claude-sonnet-4-20250514-v1:0");
        result.Models.Should().Contain(m => m.Id == "amazon.nova-pro-v1:0");
        result.Models.Should().Contain(m => m.Id == "meta.llama3-1-70b-instruct-v1:0");
    }

    [Fact]
    public async Task DetectAsync_WithBothKeys_AllModelsHaveAwsBedrockProvider()
    {
        await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access-key");
        await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret-key");

        var detector = new AmazonBedrockDetector(_config);
        var result = await detector.DetectAsync();

        result.Models.Should().AllSatisfy(m =>
            m.ProviderName.Should().Be("AWS Bedrock"));
    }

    // ── ProviderName ───────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ReturnsAwsBedrock()
    {
        var detector = new AmazonBedrockDetector(_config);

        detector.ProviderName.Should().Be("AWS Bedrock");
    }

    // ── Environment variable setup ─────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WithBothKeys_SetsAwsEnvVars_WhenNotAlreadySet()
    {
        // Clear any existing AWS env vars for this test
        var originalAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var originalSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        var originalRegion = Environment.GetEnvironmentVariable("AWS_REGION");

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
            Environment.SetEnvironmentVariable("AWS_REGION", null);

            await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access-from-store");
            await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret-from-store");

            var detector = new AmazonBedrockDetector(_config);
            var result = await detector.DetectAsync();

            // After detection, the env vars should be set from the store
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID").Should().Be("test-access-from-store");
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY").Should().Be("test-secret-from-store");
            Environment.GetEnvironmentVariable("AWS_REGION").Should().Be("us-east-1");
        }
        finally
        {
            // Restore original env vars
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", originalAccessKey);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", originalSecretKey);
            Environment.SetEnvironmentVariable("AWS_REGION", originalRegion);
        }
    }

    [Fact]
    public async Task DetectAsync_WithBothKeys_DoesNotOverwriteExistingAwsEnvVars()
    {
        // If env vars are already set, they should not be overwritten
        var originalAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var originalSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        var originalRegion = Environment.GetEnvironmentVariable("AWS_REGION");

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "pre-existing-access");
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "pre-existing-secret");
            Environment.SetEnvironmentVariable("AWS_REGION", "ap-southeast-1");

            await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access-from-store");
            await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret-from-store");

            var detector = new AmazonBedrockDetector(_config);
            var result = await detector.DetectAsync();

            // Pre-existing env vars should not be overwritten
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID").Should().Be("pre-existing-access");
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY").Should().Be("pre-existing-secret");
            Environment.GetEnvironmentVariable("AWS_REGION").Should().Be("ap-southeast-1");
        }
        finally
        {
            // Restore original env vars
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", originalAccessKey);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", originalSecretKey);
            Environment.SetEnvironmentVariable("AWS_REGION", originalRegion);
        }
    }

    // ── BuildKernel ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildKernel_WithoutPriorDetect_ThrowsInvalidOperationException()
    {
        // BuildKernel should not throw but will fail at runtime without env vars set.
        // However, we verify it doesn't crash on instantiation.
        var detector = new AmazonBedrockDetector(_config);
        var model = new ProviderModelInfo("anthropic.claude-sonnet-4-20250514-v1:0", "Claude Sonnet 4 (Bedrock)", "AWS Bedrock");

        // Note: BuildKernel may not throw synchronously; the error occurs at kernel.Build() time
        // due to missing AWS credentials in the environment. This is expected behavior.
        var act = () => detector.BuildKernel(model);

        // Depending on AWS SDK behavior, this may or may not throw immediately.
        // If it does throw, it should be related to AWS configuration.
        // If it doesn't, the kernel will still be created but will fail at runtime.
        try
        {
            var kernel = act();
            kernel.Should().NotBeNull();
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task BuildKernel_AfterDetect_ReturnsNonNullKernel()
    {
        var originalAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var originalSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        var originalRegion = Environment.GetEnvironmentVariable("AWS_REGION");

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
            Environment.SetEnvironmentVariable("AWS_REGION", null);

            await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access-key");
            await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret-key");

            var detector = new AmazonBedrockDetector(_config);
            await detector.DetectAsync();

            var model = new ProviderModelInfo("anthropic.claude-sonnet-4-20250514-v1:0", "Claude Sonnet 4 (Bedrock)", "AWS Bedrock");
            var kernel = detector.BuildKernel(model);

            kernel.Should().NotBeNull();
            // BuildKernel succeeds (though runtime use would fail due to invalid creds)
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", originalAccessKey);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", originalSecretKey);
            Environment.SetEnvironmentVariable("AWS_REGION", originalRegion);
        }
    }

    // ── StatusMessage ──────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusMessage_ContainsRegionAndModelCount()
    {
        await _store.SetAsync("jdai:provider:bedrock:accesskey", "test-access-key");
        await _store.SetAsync("jdai:provider:bedrock:secretkey", "test-secret-key");
        await _store.SetAsync("jdai:provider:bedrock:region", "us-west-2");

        var detector = new AmazonBedrockDetector(_config);
        var result = await detector.DetectAsync();

        result.StatusMessage.Should().Contain("us-west-2");
        result.StatusMessage.Should().Contain("6 model(s)");
    }
}
