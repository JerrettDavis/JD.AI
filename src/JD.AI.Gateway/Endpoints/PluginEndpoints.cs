using JD.AI.Core.Plugins;

namespace JD.AI.Gateway.Endpoints;

public static class PluginEndpoints
{
    public static void MapPluginEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/plugins").WithTags("Plugins");

        group.MapGet("/", (PluginLoader loader) =>
        {
            var plugins = loader.GetAll();
            return Results.Ok(plugins.Select(p => new
            {
                p.Name,
                p.Version,
                p.AssemblyPath,
                p.LoadedAt
            }));
        })
        .WithName("ListPlugins")
        .WithDescription("List all loaded plugins.");

        group.MapPost("/load", async (LoadPluginRequest request, PluginLoader loader, IServiceProvider sp, CancellationToken ct) =>
        {
            var context = new GatewayPluginContext(sp);
            var loaded = await loader.LoadAssemblyAsync(request.Path, context, ct);
            return loaded is null
                ? Results.BadRequest(new { Error = "No IJdAiPlugin found in assembly." })
                : Results.Created($"/api/plugins/{loaded.Name}", new
                {
                    loaded.Name,
                    loaded.Version,
                    loaded.AssemblyPath,
                    loaded.LoadedAt
                });
        })
        .WithName("LoadPlugin")
        .WithDescription("Load a plugin from an assembly path.");

        group.MapDelete("/{name}", async (string name, PluginLoader loader, CancellationToken ct) =>
        {
            await loader.UnloadAsync(name, ct);
            return Results.NoContent();
        })
        .WithName("UnloadPlugin")
        .WithDescription("Unload a plugin by name.");
    }
}

public record LoadPluginRequest(string Path);

/// <summary>IPluginContext backed by the gateway's DI container.</summary>
internal sealed class GatewayPluginContext(IServiceProvider sp) : JD.AI.Plugins.SDK.IPluginContext
{
    public Microsoft.SemanticKernel.Kernel Kernel =>
        sp.GetService<Microsoft.SemanticKernel.Kernel>() ?? new Microsoft.SemanticKernel.Kernel();

    public IReadOnlyDictionary<string, string> Configuration { get; } =
        new Dictionary<string, string>();

    public void OnEvent(string eventType, Func<object?, Task> handler) { }

    public T? GetService<T>() where T : class => sp.GetService<T>();

    public void Log(JD.AI.Plugins.SDK.PluginLogLevel level, string message) =>
        sp.GetService<ILogger<GatewayPluginContext>>()?.LogInformation("[Plugin] {Message}", message);
}
