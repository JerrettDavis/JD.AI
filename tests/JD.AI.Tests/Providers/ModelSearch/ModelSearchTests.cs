using JD.AI.Core.Providers.ModelSearch;

namespace JD.AI.Tests.Providers.ModelSearch;

public sealed class ModelSearchTests
{
    // ------------------------------------------------------------------
    // OllamaModelSearch
    // ------------------------------------------------------------------

    [Fact]
    public async Task OllamaModelSearch_ReturnsEmpty_WhenOllamaNotAvailable()
    {
        // Use a client that always fails
        var http = new HttpClient(new FailHandler())
        {
            BaseAddress = new Uri("http://localhost:19999"),
        };
        var search = new OllamaModelSearch(http);

        var results = await search.SearchAsync("llama");

        Assert.Empty(results);
    }

    [Fact]
    public void OllamaModelSearch_ProviderName_IsOllama()
    {
        var search = new OllamaModelSearch(new HttpClient());
        Assert.Equal("Ollama", search.ProviderName);
    }

    // ------------------------------------------------------------------
    // HuggingFaceModelSearch
    // ------------------------------------------------------------------

    [Fact]
    public void HuggingFaceModelSearch_BuildsCorrectApiUrl()
    {
        var url = HuggingFaceModelSearch.BuildSearchUrl("llama");

        Assert.Contains("search=llama", url, StringComparison.Ordinal);
        Assert.Contains("pipeline_tag=text-generation", url, StringComparison.Ordinal);
        Assert.Contains("sort=downloads", url, StringComparison.Ordinal);
        Assert.Contains("direction=-1", url, StringComparison.Ordinal);
        Assert.Contains("limit=20", url, StringComparison.Ordinal);
    }

    [Fact]
    public void HuggingFaceModelSearch_EscapesQueryInUrl()
    {
        var url = HuggingFaceModelSearch.BuildSearchUrl("my model");
        Assert.Contains("search=my%20model", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HuggingFaceModelSearch_ReturnsEmpty_OnHttpError()
    {
        var http = new HttpClient(new FailHandler());
        var search = new HuggingFaceModelSearch(http);

        var results = await search.SearchAsync("llama");

        Assert.Empty(results);
    }

    // ------------------------------------------------------------------
    // FoundryLocalModelSearch
    // ------------------------------------------------------------------

    [Fact]
    public async Task FoundryLocalModelSearch_ReturnsEmpty_WhenCliNotFound()
    {
        var search = new FoundryLocalModelSearch();

        // On CI / test machines the foundry CLI is typically not installed
        var results = await search.SearchAsync("phi");

        Assert.Empty(results);
    }

    [Fact]
    public void FoundryLocalModelSearch_ProviderName_IsFoundryLocal()
    {
        var search = new FoundryLocalModelSearch();
        Assert.Equal("Foundry Local", search.ProviderName);
    }

    // ------------------------------------------------------------------
    // ModelSearchAggregator
    // ------------------------------------------------------------------

    [Fact]
    public async Task Aggregator_MergesResultsFromMultipleProviders()
    {
        var a = new StubSearch("A",
        [
            new RemoteModelResult("a1", "A-Model", "A", null, "Installed", null),
        ]);
        var b = new StubSearch("B",
        [
            new RemoteModelResult("b1", "B-Model", "B", null, "Available", null),
            new RemoteModelResult("b2", "B-Model-2", "B", null, "Available", null),
        ]);

        var aggregator = new ModelSearchAggregator([a, b]);
        var results = await aggregator.SearchAllAsync("test");

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task Aggregator_FiltersToSingleProvider()
    {
        var a = new StubSearch("A",
        [
            new RemoteModelResult("a1", "A-Model", "A", null, "Installed", null),
        ]);
        var b = new StubSearch("B",
        [
            new RemoteModelResult("b1", "B-Model", "B", null, "Available", null),
        ]);

        var aggregator = new ModelSearchAggregator([a, b]);
        var results = await aggregator.SearchProviderAsync("B", "test");

        Assert.Single(results);
        Assert.Equal("b1", results[0].Id);
    }

    [Fact]
    public async Task Aggregator_ReturnsEmpty_WhenNoProvidersRegistered()
    {
        var aggregator = new ModelSearchAggregator([]);
        var results = await aggregator.SearchAllAsync("test");

        Assert.Empty(results);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private sealed class FailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated connection failure");
    }

    private sealed class StubSearch(
        string providerName,
        IReadOnlyList<RemoteModelResult> results) : IRemoteModelSearch
    {
        public string ProviderName => providerName;

        public Task<IReadOnlyList<RemoteModelResult>> SearchAsync(
            string query, CancellationToken ct = default) =>
            Task.FromResult(results);

        public Task<bool> PullAsync(
            RemoteModelResult model,
            IProgress<string>? progress = null,
            CancellationToken ct = default) =>
            Task.FromResult(true);
    }
}
