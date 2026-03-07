using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Config;

/// <summary>
/// Periodically polls <see cref="IRemoteConfigSource"/> instances for changes
/// and invokes a callback when new configuration is detected. Falls back to
/// local config when remote sources are unavailable.
/// </summary>
public sealed class RemoteConfigPoller : IDisposable
{
    private readonly List<IRemoteConfigSource> _sources;
    private readonly Func<string, string, Task> _onConfigChanged;
    private readonly ILogger? _logger;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pollTask;
    private bool _disposed;

    /// <summary>Tracks the last known version per source for change detection.</summary>
    private readonly Dictionary<string, string> _lastVersions = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="sources">Remote config sources to poll.</param>
    /// <param name="onConfigChanged">
    /// Callback invoked when config changes. Receives (sourceName, newContent).
    /// </param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="pollInterval">How often to poll. Defaults to 30 seconds.</param>
    public RemoteConfigPoller(
        IEnumerable<IRemoteConfigSource> sources,
        Func<string, string, Task> onConfigChanged,
        ILogger? logger = null,
        TimeSpan? pollInterval = null)
    {
        _sources = sources?.ToList() ?? throw new ArgumentNullException(nameof(sources));
        _onConfigChanged = onConfigChanged ?? throw new ArgumentNullException(nameof(onConfigChanged));
        _logger = logger;
        _interval = pollInterval ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>Number of registered config sources.</summary>
    public int SourceCount => _sources.Count;

    /// <summary>Whether the poller is actively running.</summary>
    public bool IsRunning => _pollTask is not null && !_pollTask.IsCompleted;

    /// <summary>Starts the polling loop.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pollTask is not null) return;

        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Performs a single poll cycle across all sources. Useful for testing
    /// or for on-demand refresh.
    /// </summary>
    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        foreach (var source in _sources)
        {
            try
            {
                var result = await source.FetchAsync(ct).ConfigureAwait(false);
                if (result is null) continue;

                var versionKey = source.Name;
                if (_lastVersions.TryGetValue(versionKey, out var lastVersion)
                    && string.Equals(lastVersion, result.Version, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // No change
                }

                _lastVersions[versionKey] = result.Version ?? string.Empty;

                _logger?.LogInformation(
                    "Config change detected from source '{Source}' (version: {Version})",
                    source.Name, result.Version ?? "unknown");

                await _onConfigChanged(source.Name, result.Content).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogError(ex, "Error polling config source '{Source}'", source.Name);
            }
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        _logger?.LogInformation(
            "Remote config poller started with {Count} source(s), polling every {Interval}s",
            _sources.Count, _interval.TotalSeconds);

        while (!ct.IsCancellationRequested)
        {
            await PollOnceAsync(ct).ConfigureAwait(false);

            try
            {
                await Task.Delay(_interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();

        // Dispose any sources that are disposable
        foreach (var source in _sources)
        {
            if (source is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
