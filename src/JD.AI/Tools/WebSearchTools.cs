using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// Web search tool using Bing Search API (via Copilot connector auth or direct API key).
/// </summary>
public sealed class WebSearchTools : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public WebSearchTools(string? bingApiKey = null)
    {
        _apiKey = bingApiKey ?? Environment.GetEnvironmentVariable("BING_SEARCH_API_KEY");
        _httpClient = new HttpClient();
    }

    [KernelFunction("web_search")]
    [Description("Search the web for current information. Returns top results with snippets and URLs.")]
    public async Task<string> SearchAsync(
        [Description("Search query")] string query,
        [Description("Number of results to return (default 5, max 10)")] int count = 5,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 10);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return "Web search not configured. Set BING_SEARCH_API_KEY environment variable.";
        }

        try
        {
            var url = $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={count}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            var results = new List<string>();
            if (doc.RootElement.TryGetProperty("webPages", out var pages) &&
                pages.TryGetProperty("value", out var values))
            {
                foreach (var item in values.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString() ?? "";
                    var snippet = item.GetProperty("snippet").GetString() ?? "";
                    var itemUrl = item.GetProperty("url").GetString() ?? "";
                    results.Add($"**{name}**\n{snippet}\n{itemUrl}");
                }
            }

            return results.Count > 0
                ? string.Join("\n\n---\n\n", results)
                : "No results found.";
        }
        catch (HttpRequestException ex)
        {
            return $"Search failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
