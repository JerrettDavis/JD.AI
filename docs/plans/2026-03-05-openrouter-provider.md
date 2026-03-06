# OpenRouter Provider Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a first-class OpenRouter provider that discovers models from the OpenRouter API, exposes rich metadata (context length, pricing, capabilities), and routes requests via the OpenAI-compatible chat completions endpoint.

**Architecture:** Extend `ApiKeyProviderDetectorBase` (the shared template for API-key providers) with an `OpenRouterDetector` that calls `GET /api/v1/models` for live model discovery and delegates kernel building to Semantic Kernel's `AddOpenAIChatCompletion` with a custom base URL. Remove OpenRouter from the `OpenAICompatibleDetector` well-known list to avoid duplicate detection. Register the new detector in all three composition roots and add `OPENROUTER_API_KEY` to the credential resolution chain.

**Tech Stack:** .NET 10, Semantic Kernel (OpenAI connector), `System.Net.Http.Json`, FluentAssertions, xUnit

---

## Task 1: Create `OpenRouterDetector`

**Files:**
- Create: `src/JD.AI.Core/Providers/OpenRouterDetector.cs`

**Step 1: Create the detector class**

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// First-class provider for OpenRouter — a unified API that exposes hundreds of
/// AI models from multiple vendors through an OpenAI-compatible interface.
/// Dynamically discovers models from the OpenRouter catalog, enriching each with
/// context length, pricing, and capability metadata.
/// </summary>
public sealed class OpenRouterDetector : ApiKeyProviderDetectorBase
{
    private const string BaseUrl = "https://openrouter.ai/api/v1";

    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("anthropic/claude-sonnet-4", "Claude Sonnet 4", "OpenRouter",
            ContextWindowTokens: 200_000, MaxOutputTokens: 16_384),
        new("openai/gpt-4.1", "GPT-4.1", "OpenRouter",
            ContextWindowTokens: 1_047_576, MaxOutputTokens: 32_768),
        new("google/gemini-2.5-pro", "Gemini 2.5 Pro", "OpenRouter",
            ContextWindowTokens: 1_048_576, MaxOutputTokens: 65_536),
        new("meta-llama/llama-4-maverick", "Llama 4 Maverick", "OpenRouter",
            ContextWindowTokens: 1_048_576, MaxOutputTokens: 1_048_576),
        new("mistralai/mistral-large", "Mistral Large", "OpenRouter",
            ContextWindowTokens: 128_000, MaxOutputTokens: 128_000),
    ];

    public OpenRouterDetector(ProviderConfigurationManager config)
        : base(config, providerName: "OpenRouter", providerKey: "openrouter")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override async Task<IReadOnlyList<ProviderModelInfo>> DiscoverModelsAsync(
        string apiKey, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await http
            .GetFromJsonAsync<OpenRouterModelsResponse>($"{BaseUrl}/models", ct)
            .ConfigureAwait(false);

        if (response?.Data is not { Count: > 0 })
            return KnownModels;

        return response.Data
            .Where(m => !string.IsNullOrEmpty(m.Id)
                        && m.Architecture?.OutputModalities?.Contains("text") != false)
            .Select(m =>
            {
                var caps = ModelCapabilities.Chat;
                if (m.SupportedParameters?.Contains("tools") == true
                    || m.SupportedParameters?.Contains("tool_choice") == true)
                    caps |= ModelCapabilities.ToolCalling;
                if (m.Architecture?.InputModalities?.Contains("image") == true)
                    caps |= ModelCapabilities.Vision;

                return new ProviderModelInfo(
                    m.Id!,
                    m.Name ?? m.Id!,
                    ProviderName,
                    ContextWindowTokens: m.ContextLength ?? 128_000,
                    MaxOutputTokens: m.TopProvider?.MaxCompletionTokens ?? 16_384,
                    InputCostPerToken: ParseDecimal(m.Pricing?.Prompt),
                    OutputCostPerToken: ParseDecimal(m.Pricing?.Completion),
                    HasMetadata: true,
                    Capabilities: caps);
            })
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    protected override void ConfigureKernel(
        IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0010
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: apiKey,
            httpClient: new HttpClient
            {
                BaseAddress = new Uri($"{BaseUrl}/"),
                Timeout = TimeSpan.FromMinutes(10),
            });
#pragma warning restore SKEXP0010
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

    // --- API response DTOs ---

    private sealed record OpenRouterModelsResponse(
        [property: JsonPropertyName("data")] List<OpenRouterModel>? Data);

    private sealed record OpenRouterModel(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("context_length")] int? ContextLength,
        [property: JsonPropertyName("pricing")] OpenRouterPricing? Pricing,
        [property: JsonPropertyName("architecture")] OpenRouterArchitecture? Architecture,
        [property: JsonPropertyName("top_provider")] OpenRouterTopProvider? TopProvider,
        [property: JsonPropertyName("supported_parameters")] List<string>? SupportedParameters);

    private sealed record OpenRouterPricing(
        [property: JsonPropertyName("prompt")] string? Prompt,
        [property: JsonPropertyName("completion")] string? Completion);

    private sealed record OpenRouterArchitecture(
        [property: JsonPropertyName("input_modalities")] List<string>? InputModalities,
        [property: JsonPropertyName("output_modalities")] List<string>? OutputModalities);

    private sealed record OpenRouterTopProvider(
        [property: JsonPropertyName("max_completion_tokens")] int? MaxCompletionTokens);
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/JD.AI.Core/JD.AI.Core.csproj -c Release --no-restore`
Expected: Build succeeded

**Step 3: Commit**

```
feat(providers): add first-class OpenRouter provider with model discovery
```

---

## Task 2: Register in all composition roots and wire credentials

**Files:**
- Modify: `src/JD.AI/Startup/ProviderOrchestrator.cs:36-52` — add `OpenRouterDetector` to detectors array
- Modify: `src/JD.AI.Core/Providers/Credentials/ProviderConfigurationManager.cs:16-34` — add `openrouter` to `WellKnownEnvVars`
- Modify: `src/JD.AI.Core/Providers/OpenAICompatibleDetector.cs:53` — remove `openrouter` from well-known list

**Step 1: Add to ProviderOrchestrator**

In `ProviderOrchestrator.CreateRegistry()`, add `new OpenRouterDetector(providerConfig)` to the detectors array, before the `OpenAICompatibleDetector` line:

```csharp
new HuggingFaceDetector(providerConfig),
new OpenRouterDetector(providerConfig),
new OpenAICompatibleDetector(providerConfig),
```

**Step 2: Add env var mapping**

In `ProviderConfigurationManager.WellKnownEnvVars`, add after the `huggingface` entry:

```csharp
["openrouter"] = new(StringComparer.OrdinalIgnoreCase) { ["apikey"] = "OPENROUTER_API_KEY" },
```

**Step 3: Remove from OpenAICompatibleDetector**

Delete the `openrouter` line from the `wellKnown` dictionary in `OpenAICompatibleDetector.DetectAsync()`:

```csharp
// Remove this line:
["openrouter"] = ("OPENROUTER_API_KEY", "https://openrouter.ai/api/v1"),
```

**Step 4: Verify it compiles**

Run: `dotnet build -c Release`
Expected: Build succeeded

**Step 5: Commit**

```
feat(providers): register OpenRouter detector and wire credential resolution
```

---

## Task 3: Add unit tests

**Files:**
- Modify: `tests/JD.AI.Tests/Providers/ApiKeyDetectorTests.cs` — add OpenRouter no-key and with-key tests

**Step 1: Add no-key test**

```csharp
[Fact]
public async Task OpenRouterDetector_NoApiKey_ReturnsUnavailable()
{
    var detector = new OpenRouterDetector(_config);

    var result = await detector.DetectAsync();

    result.IsAvailable.Should().BeFalse();
    result.Models.Should().BeEmpty();
}
```

**Step 2: Add with-key test (falls back to known models)**

```csharp
[Fact]
public async Task OpenRouterDetector_WithApiKey_ReturnsKnownModels()
{
    await _store.SetAsync("jdai:provider:openrouter:apikey", "sk-or-test-key");

    var detector = new OpenRouterDetector(_config);
    var result = await detector.DetectAsync();

    result.IsAvailable.Should().BeTrue();
    result.Models.Should().NotBeEmpty();
    result.Models.Should().Contain(m =>
        m.ProviderName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase));
}
```

**Step 3: Run tests**

Run: `dotnet test tests/JD.AI.Tests/ --filter "FullyQualifiedName~ApiKeyDetectorTests" -v normal`
Expected: All tests pass

**Step 4: Commit**

```
test(providers): add OpenRouter detector unit tests
```

---

## Task 4: Run full build and test suite

**Step 1: Full build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors

**Step 2: Run all tests**

Run: `dotnet test -c Release`
Expected: All tests pass

**Step 3: Commit any remaining fixes if needed**

---

## Summary of changes

| File | Action |
|------|--------|
| `src/JD.AI.Core/Providers/OpenRouterDetector.cs` | Create — full detector with model discovery, pricing, capabilities |
| `src/JD.AI/Startup/ProviderOrchestrator.cs` | Modify — register `OpenRouterDetector` in detectors array |
| `src/JD.AI.Core/Providers/Credentials/ProviderConfigurationManager.cs` | Modify — add `OPENROUTER_API_KEY` env var mapping |
| `src/JD.AI.Core/Providers/OpenAICompatibleDetector.cs` | Modify — remove `openrouter` from well-known list |
| `tests/JD.AI.Tests/Providers/ApiKeyDetectorTests.cs` | Modify — add 2 test methods |
