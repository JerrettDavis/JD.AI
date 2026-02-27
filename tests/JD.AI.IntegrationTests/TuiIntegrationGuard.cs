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
    /// Checks if Ollama is reachable.
    /// </summary>
    public static async Task<bool> IsOllamaAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync("http://localhost:11434/api/tags").ConfigureAwait(false);
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
