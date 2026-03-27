using JD.AI.Core.Providers;

namespace JD.AI.Gateway.Endpoints;

public static class ProviderEndpoints
{
    public static void MapProviderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/providers").WithTags("Providers");

        group.MapGet("/", async (IProviderRegistry registry, CancellationToken ct) =>
        {
            var providers = await registry.DetectProvidersAsync(ct);
            return Results.Ok(providers.Select(p => new
            {
                p.Name,
                p.IsAvailable,
                p.StatusMessage,
                Models = p.Models.Select(m => new { m.Id, m.DisplayName, m.ProviderName })
            }));
        })
        .WithName("ListProviders")
        .WithDescription("Detect and list all available AI providers and their models.");

        group.MapGet("/{name}/models", async (string name, IProviderRegistry registry, CancellationToken ct) =>
        {
            var providers = await registry.DetectProvidersAsync(ct);
            var provider = providers.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return provider is null
                ? Results.NotFound()
                : Results.Ok(provider.Models);
        })
        .WithName("GetProviderModels")
        .WithDescription("Get models for a specific provider.");

        group.MapPost("/refresh", async (IProviderRegistry registry, CancellationToken ct) =>
        {
            var providers = await registry.DetectProvidersAsync(forceRefresh: true, ct);
            return Results.Ok(providers.Select(p => new
            {
                p.Name,
                p.IsAvailable,
                p.StatusMessage,
                Models = p.Models.Select(m => new { m.Id, m.DisplayName, m.ProviderName })
            }));
        })
        .WithName("RefreshProviders")
        .WithDescription("Force re-detection of all providers, invalidating cache.");
    }
}
