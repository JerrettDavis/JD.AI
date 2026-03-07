using JD.AI.Core.Plugins;

namespace JD.AI.Gateway.Endpoints;

public static class PluginEndpoints
{
    public static void MapPluginEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/plugins").WithTags("Plugins");

        group.MapGet("/", async (IPluginLifecycleManager manager, CancellationToken ct) =>
        {
            var plugins = await manager.ListAsync(ct).ConfigureAwait(false);
            return Results.Ok(plugins);
        })
        .WithName("ListPlugins")
        .WithDescription("List installed plugins and their runtime status.");

        group.MapGet("/{id}", async (string id, IPluginLifecycleManager manager, CancellationToken ct) =>
        {
            var plugin = await manager.GetAsync(id, ct).ConfigureAwait(false);
            return plugin is null
                ? Results.NotFound(new { Error = $"Plugin '{id}' not found." })
                : Results.Ok(plugin);
        })
        .WithName("GetPlugin")
        .WithDescription("Get plugin status by id.");

        group.MapPost("/install", async (PluginInstallRequest request, IPluginLifecycleManager manager, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Source))
            {
                return Results.BadRequest(new { Error = "source is required." });
            }

            try
            {
                var plugin = await manager.InstallAsync(request.Source, request.Enable, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/plugins/{plugin.Id}", plugin);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("InstallPlugin")
        .WithDescription("Install a plugin from local path, package file, or URL.");

        group.MapPost("/{id}/enable", async (string id, IPluginLifecycleManager manager, CancellationToken ct) =>
        {
            try
            {
                var plugin = await manager.EnableAsync(id, ct).ConfigureAwait(false);
                return Results.Ok(plugin);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { Error = $"Plugin '{id}' not found." });
            }
        })
        .WithName("EnablePlugin")
        .WithDescription("Enable an installed plugin.");

        group.MapPost("/{id}/disable", async (string id, IPluginLifecycleManager manager, CancellationToken ct) =>
        {
            try
            {
                var plugin = await manager.DisableAsync(id, ct).ConfigureAwait(false);
                return Results.Ok(plugin);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { Error = $"Plugin '{id}' not found." });
            }
        })
        .WithName("DisablePlugin")
        .WithDescription("Disable an installed plugin.");

        group.MapPost("/{id}/update", async (string id, IPluginLifecycleManager manager, CancellationToken ct) =>
        {
            try
            {
                var plugin = await manager.UpdateAsync(id, ct).ConfigureAwait(false);
                return Results.Ok(plugin);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { Error = ex.Message });
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("UpdatePlugin")
        .WithDescription("Update a single plugin from its recorded source.");

        group.MapPost("/update", async (IPluginLifecycleManager manager, CancellationToken ct) =>
        {
            try
            {
                var plugins = await manager.UpdateAllAsync(ct).ConfigureAwait(false);
                return Results.Ok(plugins);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("UpdateAllPlugins")
        .WithDescription("Update all installed plugins from their recorded sources.");

        group.MapDelete("/{id}", async (string id, IPluginLifecycleManager manager, CancellationToken ct) =>
        {
            var removed = await manager.UninstallAsync(id, ct).ConfigureAwait(false);
            return removed
                ? Results.NoContent()
                : Results.NotFound(new { Error = $"Plugin '{id}' not found." });
        })
        .WithName("UnloadPlugin")
        .WithDescription("Uninstall a plugin.");
    }
}

public sealed record PluginInstallRequest(string Source, bool Enable = true);
