---
title: "Custom Providers"
description: "How to write a custom AI provider for JD.AI — implementing IProviderDetector, detection logic, Semantic Kernel integration, and credential resolution."
---

# Custom Providers

JD.AI supports 14 AI providers out of the box. You can add your own by implementing the `IProviderDetector` interface — a three-method contract that handles detection, model listing, and kernel construction.

## IProviderDetector interface

Every provider implements this interface from `JD.AI.Core`:

```csharp
public interface IProviderDetector
{
    string ProviderName { get; }
    Task<ProviderInfo> DetectAsync(CancellationToken ct = default);
    Kernel BuildKernel(ProviderModelInfo model);
}
```

| Member | Purpose |
|--------|---------|
| `ProviderName` | Unique identifier shown in the UI and used in configuration |
| `DetectAsync` | Probe for availability — check credentials, endpoints, local services |
| `BuildKernel` | Create a configured `Kernel` with the appropriate `IChatCompletionService` |

## ProviderInfo and ProviderModelInfo

`DetectAsync` returns a `ProviderInfo` describing what was found:

```csharp
public record ProviderInfo(
    string Name,
    bool IsAvailable,
    IReadOnlyList<ProviderModelInfo>? Models = null,
    string? StatusDetail = null);

public record ProviderModelInfo(
    string Id,
    string DisplayName,
    string? ProviderName = null);
```

## Step-by-step: Writing a provider

### 1. Create the detector class

```csharp
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;

public class MyServiceDetector : IProviderDetector
{
    private readonly IConfiguration _configuration;

    public MyServiceDetector(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ProviderName => "my-service";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        // 1. Resolve credentials
        var apiKey = ResolveApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ProviderInfo(ProviderName, isAvailable: false,
                StatusDetail: "No API key found");
        }

        // 2. Verify connectivity (optional but recommended)
        try
        {
            var models = await FetchAvailableModelsAsync(apiKey, ct);
            return new ProviderInfo(ProviderName, isAvailable: true,
                Models: models,
                StatusDetail: $"{models.Count} model(s) available");
        }
        catch (Exception ex)
        {
            return new ProviderInfo(ProviderName, isAvailable: false,
                StatusDetail: $"Connection failed: {ex.Message}");
        }
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var apiKey = ResolveApiKey()!;
        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IChatCompletionService>(
            new MyServiceChatCompletion(apiKey, model.Id));

        return builder.Build();
    }

    private string? ResolveApiKey()
    {
        // Priority: secure store → configuration → environment variable
        return _configuration["Providers:my-service:ApiKey"]
            ?? Environment.GetEnvironmentVariable("MY_SERVICE_API_KEY");
    }

    private async Task<List<ProviderModelInfo>> FetchAvailableModelsAsync(
        string apiKey, CancellationToken ct)
    {
        // Call the service's model list endpoint
        return new List<ProviderModelInfo>
        {
            new("my-model-large", "My Model Large", ProviderName),
            new("my-model-small", "My Model Small", ProviderName),
        };
    }
}
```

### 2. Implement IChatCompletionService

If your provider is OpenAI-compatible, use the built-in Semantic Kernel OpenAI connector. Otherwise, implement `IChatCompletionService`:

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

public class MyServiceChatCompletion : IChatCompletionService
{
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly HttpClient _httpClient = new();

    public IReadOnlyDictionary<string, object?> Attributes =>
        new Dictionary<string, object?> { ["ModelId"] = _modelId };

    public MyServiceChatCompletion(string apiKey, string modelId)
    {
        _apiKey = apiKey;
        _modelId = modelId;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken ct = default)
    {
        // Convert ChatHistory to your provider's format
        // Call the API
        // Convert the response back to ChatMessageContent
        var response = await CallApiAsync(chatHistory, ct);
        return new[] { new ChatMessageContent(AuthorRole.Assistant, response) };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent>
        GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Stream tokens from your provider
        await foreach (var token in StreamApiAsync(chatHistory, ct))
        {
            yield return new StreamingChatMessageContent(
                AuthorRole.Assistant, token);
        }
    }
}
```

### 3. Register in DI

Add your detector to the service collection:

```csharp
// In Program.cs or a DI extension method
services.AddSingleton<IProviderDetector, MyServiceDetector>();
```

JD.AI's `ProviderRegistry` collects all `IProviderDetector` instances from DI and runs `DetectAsync` on each at startup.

## Credential resolution

Follow the established credential resolution pattern (highest priority first):

1. **Encrypted secure store** — `~/.jdai/credentials/` (DPAPI on Windows, AES on Linux/macOS). Users add keys via `/provider add my-service`.
2. **Configuration** — `appsettings.json` under `Providers:my-service:ApiKey`.
3. **Environment variables** — `MY_SERVICE_API_KEY`.

```csharp
private string? ResolveApiKey()
{
    // 1. Secure store (resolved via ISecureStore if available)
    var stored = _secureStore?.GetCredential("my-service", "api_key");
    if (!string.IsNullOrEmpty(stored)) return stored;

    // 2. Configuration
    var configured = _configuration["Providers:my-service:ApiKey"];
    if (!string.IsNullOrEmpty(configured)) return configured;

    // 3. Environment variable
    return Environment.GetEnvironmentVariable("MY_SERVICE_API_KEY");
}
```

## Example: OpenAI-compatible provider

For providers with OpenAI-compatible endpoints, use the built-in connector directly:

```csharp
public class GroqDetector : IProviderDetector
{
    public string ProviderName => "groq";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            return new ProviderInfo(ProviderName, isAvailable: false);

        return new ProviderInfo(ProviderName, isAvailable: true,
            Models: new[]
            {
                new ProviderModelInfo("llama-3.3-70b", "Llama 3.3 70B", ProviderName),
                new ProviderModelInfo("mixtral-8x7b", "Mixtral 8x7B", ProviderName),
            });
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")!;
        var builder = Kernel.CreateBuilder();

        // Use the OpenAI connector with a custom endpoint
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: apiKey,
            endpoint: new Uri("https://api.groq.com/openai/v1"));

        return builder.Build();
    }
}
```

## Example: Local service provider

For local services (like Ollama), detection checks for a running process:

```csharp
public class LocalModelDetector : IProviderDetector
{
    private readonly LocalModelRegistry _registry;
    public string ProviderName => "local";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        await _registry.LoadAsync(ct);
        var models = _registry.GetAll()
            .Select(m => new ProviderModelInfo(m.Id, m.DisplayName))
            .ToList();

        var gpu = GpuDetector.Detect();
        return new ProviderInfo(ProviderName,
            isAvailable: models.Count > 0,
            Models: models,
            StatusDetail: $"{models.Count} model(s) [{gpu.Backend}]");
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(
            new LlamaInferenceEngine(model.Id, _registry));
        return builder.Build();
    }
}
```

## Testing providers

```csharp
public class MyServiceDetectorTests
{
    [Fact]
    public async Task DetectAsync_ReturnsUnavailable_WhenNoApiKey()
    {
        var config = new ConfigurationBuilder().Build();
        var detector = new MyServiceDetector(config);

        var result = await detector.DetectAsync();

        Assert.False(result.IsAvailable);
        Assert.Contains("No API key", result.StatusDetail);
    }

    [Fact]
    public async Task DetectAsync_ReturnsModels_WhenApiKeyPresent()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:my-service:ApiKey"] = "test-key"
            })
            .Build();
        var detector = new MyServiceDetector(config);

        var result = await detector.DetectAsync();

        Assert.True(result.IsAvailable);
        Assert.NotEmpty(result.Models!);
    }

    [Fact]
    public void BuildKernel_ReturnsConfiguredKernel()
    {
        var detector = CreateDetectorWithKey("test-key");
        var model = new ProviderModelInfo("my-model-large", "My Model Large");

        var kernel = detector.BuildKernel(model);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        Assert.NotNull(chatService);
    }
}
```

## See also

- [Architecture Overview](index.md) — provider abstraction layer
- [Extending JD.AI](extending.md) — project layout and coding standards
- [Providers](../reference/providers.md) — all 14 built-in providers
