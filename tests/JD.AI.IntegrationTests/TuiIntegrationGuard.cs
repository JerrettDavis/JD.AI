namespace JD.AI.Tui.IntegrationTests;

/// <summary>
/// Guards for integration test preconditions.
/// </summary>
public static class TuiIntegrationGuard
{
    private const string EnvVar = "TUI_INTEGRATION_TESTS";

    public static bool IsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable(EnvVar),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static void EnsureEnabled() =>
        Xunit.Skip.IfNot(IsEnabled, $"Set {EnvVar}=true to run TUI integration tests.");

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
}
