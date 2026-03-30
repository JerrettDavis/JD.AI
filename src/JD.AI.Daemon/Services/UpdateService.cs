using JD.AI.Core.Events;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Installation;
using JD.AI.Daemon.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JD.AI.Daemon.Services;

/// <summary>
/// Hosted service that periodically checks for daemon updates
/// and orchestrates graceful update application.
/// </summary>
public sealed class UpdateService : BackgroundService
{
    private readonly UpdateConfig _config;
    private readonly UpdateChecker _checker;
    private readonly IEventBus _events;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<UpdateService> _logger;

    private UpdateInfo? _pendingUpdate;

    public UpdateService(
        IOptions<UpdateConfig> config,
        UpdateChecker checker,
        IEventBus events,
        IHostApplicationLifetime lifetime,
        ILogger<UpdateService> logger)
    {
        _config = config.Value;
        _checker = checker;
        _events = events;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>The last detected pending update, if any.</summary>
    public UpdateInfo? PendingUpdate => _pendingUpdate;

    /// <summary>Whether the service is currently draining for an update.</summary>
    public bool IsDraining { get; private set; }

    /// <summary>
    /// Checks all installed JD.AI tools for updates and returns a structured plan.
    /// </summary>
    public async Task<AllToolsUpdatePlan> CheckAllToolsAsync(CancellationToken ct = default)
        => await _checker.CheckAllToolsAsync(ct).ConfigureAwait(false);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Update service started (check interval: {Interval})", _config.CheckInterval);

        try
        {
            // Initial delay — let the gateway stabilize
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var update = await _checker.CheckForUpdateAsync(stoppingToken);
                if (update is not null)
                {
                    _pendingUpdate = update;
                    await NotifyUpdateAvailableAsync(update, stoppingToken);

                    if (_config.AutoApply)
                    {
                        _logger.LogInformation("Auto-applying update {Update}", update);
                        await ApplyUpdateAsync(stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update check cycle failed");
            }

            try
            {
                await Task.Delay(_config.CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Applies a pending update: drains agents, runs dotnet tool update, restarts the service.
    /// Can be called manually by the CLI update command.
    /// </summary>
    public async Task ApplyUpdateAsync(CancellationToken ct = default)
    {
        if (_pendingUpdate is null)
        {
            var check = await _checker.CheckForUpdateAsync(ct);
            if (check is null)
            {
                _logger.LogInformation("Already up-to-date (v{Version})", _checker.CurrentVersion);
                return;
            }

            _pendingUpdate = check;
        }

        _logger.LogInformation("Applying update {Update}...", _pendingUpdate);
        IsDraining = true;

        try
        {
            // Notify channels about impending update
            await _events.PublishAsync(
                new GatewayEvent("gateway.update.draining", "update-service", DateTimeOffset.UtcNow,
                    new { Update = _pendingUpdate.ToString(), DrainTimeout = _config.DrainTimeout }),
                ct);

            // Wait for drain timeout to let in-flight requests finish
            _logger.LogInformation("Draining for {Timeout}...", _config.DrainTimeout);
            await Task.Delay(_config.DrainTimeout, ct);

            // Run dotnet tool update
            await _events.PublishAsync(
                new GatewayEvent("gateway.update.applying", "update-service", DateTimeOffset.UtcNow,
                    new { Update = _pendingUpdate.ToString() }),
                ct);

            var success = await RunToolUpdateAsync(ct);
            if (!success)
            {
                _logger.LogError("dotnet tool update failed — aborting");
                IsDraining = false;
                return;
            }

            _logger.LogInformation("Update applied. Restarting...");

            await _events.PublishAsync(
                new GatewayEvent("gateway.update.restarting", "update-service", DateTimeOffset.UtcNow,
                    new { Update = _pendingUpdate.ToString() }),
                ct);

            // Signal the host to stop — Windows Service recovery / systemd Restart=on-failure
            // will restart us automatically with the new version
            _lifetime.StopApplication();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update application failed");
            IsDraining = false;
        }
    }

    /// <summary>
    /// Applies updates to all installed JD.AI tools: drains agents,
    /// updates each tool, then restarts the daemon.
    /// Can be called manually via the CLI update command.
    /// </summary>
    public async Task UpdateAllToolsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Checking all JD.AI tools for updates...");
        var plan = await CheckAllToolsAsync(ct).ConfigureAwait(false);

        if (!plan.HasUpdates)
        {
            _logger.LogInformation("All JD.AI tools are up-to-date.");
            return;
        }

        _logger.LogInformation(
            "{Count} tool(s) have updates available: {Tools}",
            plan.Entries.Count(e => e.HasUpdate),
            string.Join(", ", plan.Entries.Where(e => e.HasUpdate).Select(e => $"{e.PackageId} ({e.CurrentVersion} → {e.LatestVersion})")));

        _logger.LogInformation("Applying updates to all tools...");
        IsDraining = true;

        try
        {
            await _events.PublishAsync(
                new GatewayEvent("gateway.update.draining", "update-service", DateTimeOffset.UtcNow,
                    new { DrainTimeout = _config.DrainTimeout, ToolCount = plan.Entries.Count(e => e.HasUpdate) }),
                ct);

            _logger.LogInformation("Draining for {Timeout}...", _config.DrainTimeout);
            await Task.Delay(_config.DrainTimeout, ct);

            var success = await RunAllToolsUpdateAsync(ct).ConfigureAwait(false);
            if (!success)
            {
                _logger.LogError("One or more tool updates failed — see above for details.");
                IsDraining = false;
                return;
            }

            _logger.LogInformation("All tools updated. Restarting daemon...");
            await _events.PublishAsync(
                new GatewayEvent("gateway.update.restarting", "update-service", DateTimeOffset.UtcNow,
                    new { ToolsUpdated = plan.Entries.Count(e => e.HasUpdate) }),
                ct);

            _lifetime.StopApplication();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Multi-tool update application failed");
            IsDraining = false;
        }
    }

    private async Task<bool> RunAllToolsUpdateAsync(CancellationToken ct)
    {
        try
        {
            var tools = await JDAIToolkit.GetInstalledToolsAsync(ct).ConfigureAwait(false);
            var plan = await JDAIToolkit.CheckAllAsync(tools, ct).ConfigureAwait(false);

            var allSuccess = true;
            await JDAIToolkit.ApplyAllAsync(
                plan,
                continueOnError: true,
                onToolUpdated: (tool, result) =>
                {
                    if (result.Success)
                        _logger.LogInformation("  ✓ {PackageId} updated", tool.PackageId);
                    else
                    {
                        _logger.LogError("  ✗ {PackageId} failed: {Output}", tool.PackageId, result.Output);
                        allSuccess = false;
                    }
                },
                ct).ConfigureAwait(false);

            return allSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run multi-tool update");
            return false;
        }
    }

    private async Task NotifyUpdateAvailableAsync(UpdateInfo update, CancellationToken ct)
    {
        if (!_config.NotifyChannels) return;

        await _events.PublishAsync(
            new GatewayEvent("gateway.update.available", "update-service", DateTimeOffset.UtcNow,
                new { Current = update.CurrentVersion.ToString(), Latest = update.LatestVersion.ToString() }),
            ct);
    }

    private async Task<bool> RunToolUpdateAsync(CancellationToken ct)
    {
        try
        {
            // On Windows the running jdai-daemon binary is file-locked by the OS.
            // Use DetachedUpdater to launch the update after the daemon exits;
            // the service manager (SCM / systemd) will restart the daemon with
            // the new version after _lifetime.StopApplication() is called below.
            if (OperatingSystem.IsWindows())
            {
                var result = DetachedUpdater.Launch(
                    _config.PackageId,
                    visibleWindow: false,
                    pauseOnExit: false);
                if (!result.Success)
                {
                    _logger.LogError("Failed to launch detached updater: {Output}", result.Output);
                    return false;
                }

                _logger.LogInformation("Detached updater launched. Daemon will stop and be restarted by SCM.");
                return true;
            }

            var procResult = await ProcessExecutor.RunAsync(
                "dotnet", $"tool update -g {_config.PackageId}", cancellationToken: ct);

            if (procResult.Success)
            {
                _logger.LogInformation("dotnet tool update succeeded: {Output}", procResult.StandardOutput);
                return true;
            }

            _logger.LogError("dotnet tool update failed (exit {Code}): {Err}", procResult.ExitCode, procResult.StandardError);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute dotnet tool update");
            return false;
        }
    }
}
