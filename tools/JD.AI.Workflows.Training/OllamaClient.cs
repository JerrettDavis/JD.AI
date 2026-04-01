using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Workflows.Training;

/// <summary>
/// Minimal client for Ollama's REST API.
/// Sends chat completions requests and parses the response.
/// </summary>
public sealed class OllamaClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OllamaClient(string model, string baseUrl = "http://localhost:11434")
    {
        _model = model;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(5) };
    }

    /// <summary>
    /// Sends a chat message and returns the assistant's reply text.
    /// </summary>
    public async Task<string> ChatAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var request = new OllamaChatRequest
        {
            Model = _model,
            Stream = false,
            Messages =
            [
                new OllamaMessage { Role = "system", Content = systemPrompt },
                new OllamaMessage { Role = "user", Content = userMessage },
            ],
            Options = new OllamaOptions { Temperature = 0.7f },
        };

        var response = await _http.PostAsJsonAsync("/api/chat", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(ct);
        return json?.Message?.Content?.Trim() ?? "";
    }

    /// <summary>
    /// Classifies a single prompt as "workflow" or "conversation" using the configured model.
    /// </summary>
    public async Task<string> ClassifyAsync(string prompt, CancellationToken ct = default)
    {
        var system = """
            You are a binary classifier. Given a user prompt to an AI coding assistant,
            classify it as exactly one of:
            - WORKFLOW: the user wants to perform a multi-step task (create/build/deploy/run/fix something, or any task requiring tool use)
            - CONVERSATION: the user is asking a question, having a discussion, seeking advice, or just chatting

            Reply with exactly one word: WORKFLOW or CONVERSATION
            """;

        var result = await ChatAsync(system, $"Prompt: \"{prompt}\"", ct);
        if (result.Contains("WORKFLOW", StringComparison.OrdinalIgnoreCase))
            return "WORKFLOW";
        if (result.Contains("CONVERSATION", StringComparison.OrdinalIgnoreCase))
            return "CONVERSATION";
        return "UNKNOWN"; // ambiguous — will be filtered out
    }

    public void Dispose() => _http.Dispose();

    // ── Ollama REST types ───────────────────────────────────────────────

    private sealed record OllamaChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = "";
        [JsonPropertyName("stream")] public bool Stream { get; init; }
        [JsonPropertyName("messages")] public List<OllamaMessage> Messages { get; init; } = [];
        [JsonPropertyName("options")] public OllamaOptions Options { get; init; } = new();
    }

    private sealed record OllamaMessage
    {
        [JsonPropertyName("role")] public string Role { get; init; } = "";
        [JsonPropertyName("content")] public string Content { get; init; } = "";
    }

#pragma warning disable CA1812 // Instantiated via System.Text.Json deserialization in ChatAsync
    private sealed record OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; init; }
    }
#pragma warning restore CA1812

    private sealed record OllamaOptions
    {
        [JsonPropertyName("temperature")] public float Temperature { get; init; } = 0.7f;
    }
}
