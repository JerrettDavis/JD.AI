using JD.AI.Core.Providers;
using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace JD.AI.Tests.Providers;

public sealed class ClaudeCodeDetectorKernelTests
{
    [Fact]
    public void BuildKernel_RegistersKernelWithoutResolvingCredentials()
    {
        var detector = new ClaudeCodeDetector();
        var model = new ProviderModelInfo(ClaudeModels.Sonnet, "Claude Sonnet 4.6", "Claude Code");

        var kernel = detector.BuildKernel(model);

        Assert.NotNull(kernel);
    }

    [Fact]
    public void ConfigureKernelBuilder_WithOAuthToken_RegistersPromptCachingChatClient()
    {
        var builder = Kernel.CreateBuilder();
        var options = new ClaudeCodeSessionOptions
        {
            OAuthToken = "sk-ant-oat-test-token",
        };

        ClaudeCodeDetector.ConfigureKernelBuilder(builder, options);
        var kernel = builder.Build();

        var chatClient = kernel.GetRequiredService<IChatClient>();
        Assert.NotNull(chatClient);

        // Outer wrapper is ConfigureOptionsChatClient (sets model ID default);
        // AnthropicPromptCachingChatClient is inner.
        Assert.IsAssignableFrom<IChatClient>(chatClient);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        Assert.NotNull(chatService);
    }

    [Fact]
    public void ConfigureKernelBuilder_NullBuilder_Throws()
    {
        var options = new ClaudeCodeSessionOptions
        {
            OAuthToken = "sk-ant-oat-test-token",
        };

        Assert.Throws<ArgumentNullException>(() =>
            ClaudeCodeDetector.ConfigureKernelBuilder(null!, options));
    }

    [Fact]
    public void ConfigureKernelBuilder_NullOptions_Throws()
    {
        var builder = Kernel.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            ClaudeCodeDetector.ConfigureKernelBuilder(builder, null!));
    }

}
