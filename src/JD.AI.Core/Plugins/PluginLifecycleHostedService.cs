using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Plugins;

/// <summary>
/// Loads enabled plugins at host startup.
/// </summary>
public sealed class PluginLifecycleHostedService : IHostedService
{
    private readonly IPluginLifecycleManager _manager;
    private readonly ILogger<PluginLifecycleHostedService> _logger;

    public PluginLifecycleHostedService(
        IPluginLifecycleManager manager,
        ILogger<PluginLifecycleHostedService> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var count = await _manager.LoadEnabledAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Loaded {Count} enabled plugins from registry.", count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
