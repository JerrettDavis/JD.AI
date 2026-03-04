namespace JD.AI.Core.Providers.Metadata;

/// <summary>
/// Fetches model metadata from LiteLLM's published GitHub catalog.
/// </summary>
public sealed class LiteLlmMetadataSource : IModelMetadataSource
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    private static readonly Uri CatalogUri =
        new("https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json");

    public async Task<string?> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            return await SharedClient.GetStringAsync(CatalogUri, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
