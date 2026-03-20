using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;
using Xunit;

namespace JD.AI.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ProviderFeatureIntegrationTests
{
    public static IEnumerable<object[]> ApiKeyProviderCases()
    {
        yield return
        [
            new ApiKeyProviderCase(
                Name: "openai",
                EnsureCredentials: IntegrationTestGuard.EnsureOpenAiKey,
                BuildCredentials: () => [("openai", "apikey", IntegrationTestGuard.OpenAIApiKey!)],
                CreateDetector: config => new OpenAIDetector(config),
                PreferredModelIds: ["gpt-4o-mini", "gpt-4.1-mini", "gpt-4o", "gpt-4.1"])
        ];

        yield return
        [
            new ApiKeyProviderCase(
                Name: "azure-openai",
                EnsureCredentials: IntegrationTestGuard.EnsureAzureOpenAi,
                BuildCredentials: () =>
                {
                    var creds = new List<(string Provider, string Field, string Value)>
                    {
                        ("azure-openai", "apikey", IntegrationTestGuard.AzureOpenAIApiKey!),
                        ("azure-openai", "endpoint", IntegrationTestGuard.AzureOpenAIEndpoint!)
                    };

                    if (!string.IsNullOrWhiteSpace(IntegrationTestGuard.AzureOpenAIDeployments))
                        creds.Add(("azure-openai", "deployments", IntegrationTestGuard.AzureOpenAIDeployments!));

                    return creds.ToArray();
                },
                CreateDetector: config => new AzureOpenAIDetector(config))
        ];

        yield return
        [
            new ApiKeyProviderCase(
                Name: "anthropic",
                EnsureCredentials: IntegrationTestGuard.EnsureAnthropicKey,
                BuildCredentials: () => [("anthropic", "apikey", IntegrationTestGuard.AnthropicApiKey!)],
                CreateDetector: config => new AnthropicDetector(config),
                PreferredModelIds: ["haiku", "sonnet"])
        ];

        yield return
        [
            new ApiKeyProviderCase(
                Name: "google-gemini",
                EnsureCredentials: IntegrationTestGuard.EnsureGoogleGeminiKey,
                BuildCredentials: () => [("google-gemini", "apikey", IntegrationTestGuard.GoogleGeminiApiKey!)],
                CreateDetector: config => new GoogleGeminiDetector(config),
                PreferredModelIds: ["flash", "gemini-2.0", "gemini-1.5"])
        ];

        yield return
        [
            new ApiKeyProviderCase(
                Name: "mistral",
                EnsureCredentials: IntegrationTestGuard.EnsureMistralKey,
                BuildCredentials: () => [("mistral", "apikey", IntegrationTestGuard.MistralApiKey!)],
                CreateDetector: config => new MistralDetector(config),
                PreferredModelIds: ["small", "nemo", "codestral"])
        ];

        yield return
        [
            new ApiKeyProviderCase(
                Name: "openrouter",
                EnsureCredentials: IntegrationTestGuard.EnsureOpenRouterKey,
                BuildCredentials: () => [("openrouter", "apikey", IntegrationTestGuard.OpenRouterApiKey!)],
                CreateDetector: config => new OpenRouterDetector(config),
                PreferredModelIds: ["mini", "flash", "haiku", "small"])
        ];

        yield return
        [
            new ApiKeyProviderCase(
                Name: "bedrock",
                EnsureCredentials: IntegrationTestGuard.EnsureBedrockCredentials,
                BuildCredentials: () =>
                [
                    ("bedrock", "accesskey", IntegrationTestGuard.AwsAccessKeyId!),
                    ("bedrock", "secretkey", IntegrationTestGuard.AwsSecretAccessKey!),
                    ("bedrock", "region", IntegrationTestGuard.AwsRegion)
                ],
                CreateDetector: config => new AmazonBedrockDetector(config),
                PreferredModelIds: ["haiku", "sonnet", "nova-lite", "nova"])
        ];

        yield return
        [
            new ApiKeyProviderCase(
                Name: "openai-compat",
                EnsureCredentials: IntegrationTestGuard.EnsureOpenAiCompatEndpoint,
                BuildCredentials: () =>
                {
                    var alias = IntegrationTestGuard.OpenAICompatAlias;
                    var provider = $"openai-compat:{alias}";
                    return
                    [
                        (provider, "apikey", IntegrationTestGuard.OpenAICompatApiKey!),
                        (provider, "baseurl", IntegrationTestGuard.OpenAICompatBaseUrl!)
                    ];
                },
                CreateDetector: config => new OpenAICompatibleDetector(config))
        ];
    }

    [SkippableTheory]
    [MemberData(nameof(ApiKeyProviderCases))]
    public async Task ApiKeyProvider_Detects_BuildsKernel_AndCompletesToolTurn(ApiKeyProviderCase providerCase)
    {
        IntegrationTestGuard.EnsureEnabled();
        providerCase.EnsureCredentials();

        await RunApiKeyProviderToolSmokeAsync(
            providerCase.Name,
            providerCase.BuildCredentials(),
            providerCase.CreateDetector,
            providerCase.PreferredModelIds).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task ClaudeCode_WhenAuthenticated_BuildsKernel_AndCompletesToolTurn()
    {
        IntegrationTestGuard.EnsureEnabled();

        var detector = new ClaudeCodeDetector();
        var info = await detector.DetectAsync().ConfigureAwait(false);
        Skip.IfNot(info.IsAvailable, $"Claude Code unavailable: {info.StatusMessage}");

        await RunToolSmokeAsync(detector, info, preferredModelIds: ["haiku", "sonnet"]).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task Ollama_WhenAvailable_BuildsKernel_AndCompletesToolTurn()
    {
        await IntegrationTestGuard.EnsureOllamaAsync();

        var detector = new OllamaDetector();
        var info = await detector.DetectAsync().ConfigureAwait(false);
        Skip.IfNot(info.IsAvailable, $"Ollama unavailable: {info.StatusMessage}");

        await RunToolSmokeAsync(detector, info, preferredModelIds: ["qwen", "llama", "phi"]).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task Copilot_WhenAuthenticated_BuildsKernel_AndCompletesToolTurn()
    {
        IntegrationTestGuard.EnsureEnabled();

        var detector = new CopilotDetector();
        var info = await detector.DetectAsync().ConfigureAwait(false);
        Skip.IfNot(info.IsAvailable, $"GitHub Copilot unavailable: {info.StatusMessage}");

        await RunToolSmokeAsync(detector, info, preferredModelIds: ["mini", "gpt-4.1"]).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task Codex_WhenAuthenticated_BuildsKernel_AndCompletesToolTurn()
    {
        IntegrationTestGuard.EnsureEnabled();

        var detector = new OpenAICodexDetector();
        var info = await detector.DetectAsync().ConfigureAwait(false);
        Skip.IfNot(info.IsAvailable, $"OpenAI Codex unavailable: {info.StatusMessage}");

        await RunToolSmokeAsync(detector, info, preferredModelIds: ["mini", "gpt-5.1-codex-mini"]).ConfigureAwait(false);
    }

    private static async Task RunApiKeyProviderToolSmokeAsync(
        string providerName,
        IEnumerable<(string Provider, string Field, string Value)> credentials,
        Func<ProviderConfigurationManager, IProviderDetector> createDetector,
        string[]? preferredModelIds = null)
    {
        using var temp = await ProviderIntegrationTestHelpers
            .CreateTempProviderConfigurationAsync(providerName, credentials)
            .ConfigureAwait(false);

        var detector = createDetector(temp.Config);
        var info = await detector.DetectAsync().ConfigureAwait(false);

        Skip.IfNot(info.IsAvailable, $"{detector.ProviderName} unavailable: {info.StatusMessage}");
        await RunToolSmokeAsync(detector, info, preferredModelIds).ConfigureAwait(false);
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
        await File.WriteAllTextAsync(tempFile, "provider integration test content").ConfigureAwait(false);
        try
        {
            var response = await harness.ExecuteTurnAsync(
                    $"Read the file at {tempFile} and summarize its contents in one short sentence.")
                .ConfigureAwait(false);
            Assert.NotNull(response);
            Assert.NotEmpty(response.Trim());
            Assert.DoesNotContain("error:", response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("insufficient_quota", response, StringComparison.OrdinalIgnoreCase);
            Assert.True(harness.Session.History.Count >= 2,
                "Session should contain user + assistant messages after provider tool smoke turn");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

}

public sealed record ApiKeyProviderCase(
    string Name,
    Action EnsureCredentials,
    Func<(string Provider, string Field, string Value)[]> BuildCredentials,
    Func<ProviderConfigurationManager, IProviderDetector> CreateDetector,
    string[]? PreferredModelIds = null);
