namespace JD.AI.IntegrationTests;

/// <summary>
/// Guards for integration test preconditions.
/// </summary>
public static class IntegrationTestGuard
{
    private const string EnvVar = "JDAI_INTEGRATION_TESTS";
    private const string LegacyEnvVar = "TUI_INTEGRATION_TESTS";

    public static bool IsEnabled =>
        IsTrue(Environment.GetEnvironmentVariable(EnvVar)) ||
        IsTrue(Environment.GetEnvironmentVariable(LegacyEnvVar));

    public static void EnsureEnabled() =>
        Xunit.Skip.IfNot(
            IsEnabled,
            $"Set {EnvVar}=true (or legacy {LegacyEnvVar}=true) to run integration tests.");

    /// <summary>
    /// Chat model name, configurable via <c>OLLAMA_CHAT_MODEL</c> env var.
    /// </summary>
    public static string OllamaModel =>
        Environment.GetEnvironmentVariable("OLLAMA_CHAT_MODEL") is { Length: > 0 } m
            ? m : "llama3.2:latest";

    /// <summary>
    /// Ollama base endpoint, configurable via <c>OLLAMA_ENDPOINT</c> env var.
    /// </summary>
    public static string OllamaEndpoint =>
        Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") is { Length: > 0 } ep
            ? ep.TrimEnd('/') : "http://localhost:11434";

    /// <summary>
    /// OpenAI API key from <c>OPENAI_API_KEY</c> env var.
    /// </summary>
    public static string? OpenAIApiKey =>
        Environment.GetEnvironmentVariable("OPENAI_API_KEY") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// Azure OpenAI API key from <c>AZURE_OPENAI_API_KEY</c> env var.
    /// </summary>
    public static string? AzureOpenAIApiKey =>
        Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// Azure OpenAI endpoint from <c>AZURE_OPENAI_ENDPOINT</c> env var.
    /// </summary>
    public static string? AzureOpenAIEndpoint =>
        Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") is { Length: > 0 } e ? e : null;

    /// <summary>
    /// Optional comma-separated Azure deployments from <c>AZURE_OPENAI_DEPLOYMENTS</c> env var.
    /// </summary>
    public static string? AzureOpenAIDeployments =>
        Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENTS") is { Length: > 0 } d ? d : null;

    /// <summary>
    /// Anthropic API key from <c>ANTHROPIC_API_KEY</c> env var.
    /// </summary>
    public static string? AnthropicApiKey =>
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// Google Gemini API key from <c>GOOGLE_AI_API_KEY</c> env var.
    /// </summary>
    public static string? GoogleGeminiApiKey =>
        Environment.GetEnvironmentVariable("GOOGLE_AI_API_KEY") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// Mistral API key from <c>MISTRAL_API_KEY</c> env var.
    /// </summary>
    public static string? MistralApiKey =>
        Environment.GetEnvironmentVariable("MISTRAL_API_KEY") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// OpenRouter API key from <c>OPENROUTER_API_KEY</c> env var.
    /// </summary>
    public static string? OpenRouterApiKey =>
        Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// AWS access key for Bedrock from <c>AWS_ACCESS_KEY_ID</c> env var.
    /// </summary>
    public static string? AwsAccessKeyId =>
        Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// AWS secret key for Bedrock from <c>AWS_SECRET_ACCESS_KEY</c> env var.
    /// </summary>
    public static string? AwsSecretAccessKey =>
        Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// AWS region for Bedrock from <c>AWS_REGION</c> env var.
    /// </summary>
    public static string AwsRegion =>
        Environment.GetEnvironmentVariable("AWS_REGION") is { Length: > 0 } r ? r : "us-east-1";

    /// <summary>
    /// HuggingFace API key from <c>HF_API_KEY</c> env var. Returns null if not set.
    /// </summary>
    public static string? HuggingFaceApiKey =>
        Environment.GetEnvironmentVariable("HF_API_KEY") is { Length: > 0 } k
            ? k
            : Environment.GetEnvironmentVariable("HUGGINGFACE_API_KEY") is { Length: > 0 } hk
                ? hk
                : null;

    /// <summary>
    /// API key for an OpenAI-compatible endpoint test.
    /// </summary>
    public static string? OpenAICompatApiKey =>
        Environment.GetEnvironmentVariable("OPENAI_COMPAT_API_KEY") is { Length: > 0 } k ? k : null;

    /// <summary>
    /// Base URL for an OpenAI-compatible endpoint test.
    /// </summary>
    public static string? OpenAICompatBaseUrl =>
        Environment.GetEnvironmentVariable("OPENAI_COMPAT_BASE_URL") is { Length: > 0 } u ? u : null;

    /// <summary>
    /// Alias name for OpenAI-compatible endpoint test.
    /// </summary>
    public static string OpenAICompatAlias =>
        Environment.GetEnvironmentVariable("OPENAI_COMPAT_ALIAS") is { Length: > 0 } a ? a : "compat";

    /// <summary>
    /// Checks if Ollama is reachable.
    /// </summary>
    public static async Task<bool> IsOllamaAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync($"{OllamaEndpoint}/api/tags").ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static async Task EnsureOllamaAsync()
    {
        EnsureEnabled();
        var available = await IsOllamaAvailableAsync().ConfigureAwait(false);
        Xunit.Skip.IfNot(available, "Ollama is not running on localhost:11434.");
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    public static void EnsureHuggingFaceKey() =>
        Xunit.Skip.IfNot(HuggingFaceApiKey is not null, "Set HF_API_KEY to run HuggingFace integration tests.");

    public static void EnsureOpenAiKey() =>
        Xunit.Skip.IfNot(OpenAIApiKey is not null, "Set OPENAI_API_KEY to run OpenAI integration tests.");

    public static void EnsureAzureOpenAi() =>
        Xunit.Skip.IfNot(
            AzureOpenAIApiKey is not null &&
            AzureOpenAIEndpoint is not null,
            "Set AZURE_OPENAI_API_KEY and AZURE_OPENAI_ENDPOINT to run Azure OpenAI integration tests.");

    public static void EnsureAnthropicKey() =>
        Xunit.Skip.IfNot(AnthropicApiKey is not null, "Set ANTHROPIC_API_KEY to run Anthropic integration tests.");

    public static void EnsureGoogleGeminiKey() =>
        Xunit.Skip.IfNot(
            GoogleGeminiApiKey is not null,
            "Set GOOGLE_AI_API_KEY to run Google Gemini integration tests.");

    public static void EnsureMistralKey() =>
        Xunit.Skip.IfNot(MistralApiKey is not null, "Set MISTRAL_API_KEY to run Mistral integration tests.");

    public static void EnsureOpenRouterKey() =>
        Xunit.Skip.IfNot(OpenRouterApiKey is not null, "Set OPENROUTER_API_KEY to run OpenRouter integration tests.");

    public static void EnsureBedrockCredentials() =>
        Xunit.Skip.IfNot(
            AwsAccessKeyId is not null && AwsSecretAccessKey is not null,
            "Set AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY to run Bedrock integration tests.");

    public static void EnsureOpenAiCompatEndpoint() =>
        Xunit.Skip.IfNot(
            OpenAICompatApiKey is not null && OpenAICompatBaseUrl is not null,
            "Set OPENAI_COMPAT_API_KEY and OPENAI_COMPAT_BASE_URL to run OpenAI-compatible integration tests.");
}
