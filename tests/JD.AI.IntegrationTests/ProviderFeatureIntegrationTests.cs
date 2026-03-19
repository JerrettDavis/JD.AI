using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;
using Xunit;

namespace JD.AI.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ProviderFeatureIntegrationTests
{
    [SkippableFact]
    public async Task OpenAI_Detects_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();
        TuiIntegrationGuard.EnsureOpenAiKey();

        await RunApiKeyProviderToolSmokeAsync(
            provider: "openai",
            credentials:
            [
                ("openai", "apikey", TuiIntegrationGuard.OpenAIApiKey!)
            ],
            createDetector: config => new OpenAIDetector(config),
            preferredModelIds: ["gpt-4o-mini", "gpt-4.1-mini", "gpt-4o", "gpt-4.1"]);
    }

    [SkippableFact]
    public async Task AzureOpenAI_Detects_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();
        TuiIntegrationGuard.EnsureAzureOpenAi();

        var creds = new List<(string Provider, string Field, string Value)>
        {
            ("azure-openai", "apikey", TuiIntegrationGuard.AzureOpenAIApiKey!),
            ("azure-openai", "endpoint", TuiIntegrationGuard.AzureOpenAIEndpoint!)
        };

        if (!string.IsNullOrWhiteSpace(TuiIntegrationGuard.AzureOpenAIDeployments))
        {
            creds.Add(("azure-openai", "deployments", TuiIntegrationGuard.AzureOpenAIDeployments!));
        }

        await RunApiKeyProviderToolSmokeAsync(
            provider: "azure-openai",
            credentials: creds,
            createDetector: config => new AzureOpenAIDetector(config));
    }

    [SkippableFact]
    public async Task Anthropic_Detects_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();
        TuiIntegrationGuard.EnsureAnthropicKey();

        await RunApiKeyProviderToolSmokeAsync(
            provider: "anthropic",
            credentials:
            [
                ("anthropic", "apikey", TuiIntegrationGuard.AnthropicApiKey!)
            ],
            createDetector: config => new AnthropicDetector(config),
            preferredModelIds: ["haiku", "sonnet"]);
    }

    [SkippableFact]
    public async Task GoogleGemini_Detects_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();
        TuiIntegrationGuard.EnsureGoogleGeminiKey();

        await RunApiKeyProviderToolSmokeAsync(
            provider: "google-gemini",
            credentials:
            [
                ("google-gemini", "apikey", TuiIntegrationGuard.GoogleGeminiApiKey!)
            ],
            createDetector: config => new GoogleGeminiDetector(config),
            preferredModelIds: ["flash", "gemini-2.0", "gemini-1.5"]);
    }

    [SkippableFact]
    public async Task Mistral_Detects_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();
        TuiIntegrationGuard.EnsureMistralKey();

        await RunApiKeyProviderToolSmokeAsync(
            provider: "mistral",
            credentials:
            [
                ("mistral", "apikey", TuiIntegrationGuard.MistralApiKey!)
            ],
            createDetector: config => new MistralDetector(config),
            preferredModelIds: ["small", "nemo", "codestral"]);
    }

    [SkippableFact]
    public async Task OpenRouter_Detects_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();
        TuiIntegrationGuard.EnsureOpenRouterKey();

        await RunApiKeyProviderToolSmokeAsync(
            provider: "openrouter",
            credentials:
            [
                ("openrouter", "apikey", TuiIntegrationGuard.OpenRouterApiKey!)
            ],
            createDetector: config => new OpenRouterDetector(config),
            preferredModelIds: ["mini", "flash", "haiku", "small"]);
    }

    [SkippableFact]
    public async Task Bedrock_Detects_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();
        TuiIntegrationGuard.EnsureBedrockCredentials();

        await RunApiKeyProviderToolSmokeAsync(
            provider: "bedrock",
            credentials:
            [
                ("bedrock", "accesskey", TuiIntegrationGuard.AwsAccessKeyId!),
                ("bedrock", "secretkey", TuiIntegrationGuard.AwsSecretAccessKey!),
                ("bedrock", "region", TuiIntegrationGuard.AwsRegion)
            ],
            createDetector: config => new AmazonBedrockDetector(config),
            preferredModelIds: ["haiku", "sonnet", "nova-lite", "nova"]);
    }

    [SkippableFact]
    public async Task OpenAICompatible_Detects_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();
        TuiIntegrationGuard.EnsureOpenAiCompatEndpoint();

        var alias = TuiIntegrationGuard.OpenAICompatAlias;
        await RunApiKeyProviderToolSmokeAsync(
            provider: $"openai-compat:{alias}",
            credentials:
            [
                ($"openai-compat:{alias}", "apikey", TuiIntegrationGuard.OpenAICompatApiKey!),
                ($"openai-compat:{alias}", "baseurl", TuiIntegrationGuard.OpenAICompatBaseUrl!)
            ],
            createDetector: config => new OpenAICompatibleDetector(config));
    }

    [SkippableFact]
    public async Task ClaudeCode_WhenAuthenticated_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();

        var detector = new ClaudeCodeDetector();
        var info = await detector.DetectAsync();
        Skip.IfNot(info.IsAvailable, $"Claude Code unavailable: {info.StatusMessage}");

        await RunToolSmokeAsync(detector, info, preferredModelIds: ["haiku", "sonnet"]);
    }

    [SkippableFact]
    public async Task Copilot_WhenAuthenticated_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();

        var detector = new CopilotDetector();
        var info = await detector.DetectAsync();
        Skip.IfNot(info.IsAvailable, $"GitHub Copilot unavailable: {info.StatusMessage}");

        await RunToolSmokeAsync(detector, info, preferredModelIds: ["mini", "gpt-4.1"]);
    }

    [SkippableFact]
    public async Task Codex_WhenAuthenticated_BuildsKernel_AndCompletesToolTurn()
    {
        TuiIntegrationGuard.EnsureEnabled();

        var detector = new OpenAICodexDetector();
        var info = await detector.DetectAsync();
        Skip.IfNot(info.IsAvailable, $"OpenAI Codex unavailable: {info.StatusMessage}");

        await RunToolSmokeAsync(detector, info, preferredModelIds: ["mini", "gpt-5.1-codex-mini"]);
    }

    private static async Task RunApiKeyProviderToolSmokeAsync(
        string provider,
        IEnumerable<(string Provider, string Field, string Value)> credentials,
        Func<ProviderConfigurationManager, IProviderDetector> createDetector,
        string[]? preferredModelIds = null)
    {
        using var temp = await ProviderIntegrationTestHelpers
            .CreateTempProviderConfigurationAsync(provider, credentials)
            .ConfigureAwait(false);

        var detector = createDetector(temp.Config);
        var info = await detector.DetectAsync();

        Skip.IfNot(info.IsAvailable, $"{detector.ProviderName} unavailable: {info.StatusMessage}");
        await RunToolSmokeAsync(detector, info, preferredModelIds);
    }

    private static async Task RunToolSmokeAsync(
        IProviderDetector detector,
        ProviderInfo info,
        string[]? preferredModelIds = null)
    {
        Assert.NotEmpty(info.Models);

        var model = ProviderIntegrationTestHelpers.SelectPreferredModel(info.Models, preferredModelIds);
        using var harness = HeadlessAgentIntegrationHarness.Create(detector, model);
        harness.Session.Kernel.Plugins.AddFromType<FileTools>("FileTools");
        var chatServices = harness.Session.Kernel
            .GetAllServices<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
        Assert.NotEmpty(chatServices);

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "provider integration test content");
        try
        {
            var response = await harness.ExecuteTurnAsync(
                $"Read the file at {tempFile} and summarize its contents in one short sentence.");
            Assert.NotNull(response);
            Assert.True(harness.Session.History.Count >= 2,
                "Session should contain user + assistant messages after provider tool smoke turn");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

}
