using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Governance;

/// <summary>
/// Watches policy directories for changes and triggers reload callbacks.
/// Uses <see cref="FileSystemWatcher"/> with debouncing to avoid spurious reloads.
/// </summary>
public sealed class PolicyWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Action _onReload;
    private readonly ILogger? _logger;
    private readonly TimeSpan _debounce;
    private CancellationTokenSource? _debounceCts;
    private readonly Lock _lock = new();
    private bool _disposed;

    public PolicyWatcher(
        IEnumerable<string> directories,
        Action onReload,
        ILogger? logger = null,
        TimeSpan? debounce = null)
    {
        _onReload = onReload ?? throw new ArgumentNullException(nameof(onReload));
        _logger = logger;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(500);

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir)) continue;

            var watcher = new FileSystemWatcher(dir)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };

            watcher.Filter = "*.yaml";
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileRenamed;
            _watchers.Add(watcher);

            // Also watch .yml files
            var ymlWatcher = new FileSystemWatcher(dir)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                Filter = "*.yml",
            };

            ymlWatcher.Changed += OnFileChanged;
            ymlWatcher.Created += OnFileChanged;
            ymlWatcher.Deleted += OnFileChanged;
            ymlWatcher.Renamed += OnFileRenamed;
            _watchers.Add(ymlWatcher);
        }
    }

    /// <summary>Number of active file system watchers.</summary>
    public int WatcherCount => _watchers.Count;

    private void OnFileChanged(object sender, FileSystemEventArgs e) => ScheduleReload(e.FullPath);
    private void OnFileRenamed(object sender, RenamedEventArgs e) => ScheduleReload(e.FullPath);

    private void ScheduleReload(string path)
    {
        lock (_lock)
        {
            if (_disposed) return;

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_debounce, token).ConfigureAwait(false);
                    _logger?.LogInformation("Policy file changed: {Path}. Reloading policies.", path);
                    _onReload();
                }
                catch (OperationCanceledException)
                {
                    // Debounce cancelled — a newer change superseded this one
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error reloading policies after file change.");
                }
            }, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
    }
}
