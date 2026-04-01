using JD.AI.Core.Sessions;
using JD.AI.Gateway.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JD.AI.Gateway.Services;

/// <summary>
/// Background service that periodically scans for stale sessions and closes them.
/// A session is considered stale when it has been inactive (no UpdatedAt change)
/// for longer than the configured <see cref="SessionCleanupConfig.MaxInactiveAge"/>.
/// </summary>
public sealed class SessionCleanupService : BackgroundService
{
    private readonly SessionStore _sessionStore;
    private readonly ILogger<SessionCleanupService> _log;
    private readonly SessionCleanupConfig _config;

    public SessionCleanupService(
        SessionStore sessionStore,
        IOptions<GatewayConfig> config,
        ILogger<SessionCleanupService> log)
    {
        _sessionStore = sessionStore;
        _config = config.Value.SessionCleanup;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _log.LogInformation("SessionCleanupService is disabled");
            return;
        }

        var interval = ParseDuration(_config.Interval, TimeSpan.FromMinutes(30));
        var maxInactiveAge = ParseDuration(_config.MaxInactiveAge, TimeSpan.FromHours(24));

        _log.LogInformation(
            "SessionCleanupService starting — will close sessions inactive for > {MaxInactiveAge}, "
            + "scan every {Interval}",
            maxInactiveAge, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
                if (stoppingToken.IsCancellationRequested) break;

                var closed = await _sessionStore.CloseInactiveSessionsAsync(maxInactiveAge)
                    .ConfigureAwait(false);

                if (closed > 0)
                    _log.LogInformation(
                        "Closed {Count} stale session(s) older than {MaxInactiveAge}",
                        closed, maxInactiveAge);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "SessionCleanupService encountered an error — continuing after {Delay}s",
                    (int)interval.TotalSeconds / 2);
            }
        }

        _log.LogInformation("SessionCleanupService stopping");
    }

    private static TimeSpan ParseDuration(string value, TimeSpan defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var lastChar = char.ToLowerInvariant(value[^1]);
        var numberPart = char.IsDigit(lastChar) ? value : value[..^1];
        if (!double.TryParse(numberPart, out var n))
            return defaultValue;

        return lastChar switch
        {
            's' => TimeSpan.FromSeconds(n),
            'm' => TimeSpan.FromMinutes(n),
            'h' => TimeSpan.FromHours(n),
            'd' => TimeSpan.FromDays(n),
            _ => TimeSpan.FromHours(n),
        };
    }
}
