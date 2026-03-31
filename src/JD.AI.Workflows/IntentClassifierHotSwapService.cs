using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML;

namespace JD.AI.Workflows;

/// <summary>
/// Manages the active <see cref="IPromptIntentClassifier"/> and enables hot-swapping
/// to a new classifier (e.g. ML.NET model) without restarting the Gateway.
/// </summary>
public interface IIntentClassifierManager
{
    /// <summary>Current active classifier.</summary>
    IPromptIntentClassifier Classifier { get; }

    /// <summary>
    /// Replaces the active classifier with <paramref name="classifier"/>,
    /// disposing the previous instance if it implements <see cref="IDisposable"/>.
    /// </summary>
    void SetClassifier(IPromptIntentClassifier classifier);

    /// <summary>
    /// Reloads the currently active classifier if it supports hot-reload
    /// (e.g. <see cref="MlNetIntentClassifier"/>).
    /// </summary>
    void ReloadCurrent();
}

/// <summary>
/// Default implementation of <see cref="IIntentClassifierManager"/>.
/// </summary>
public sealed class IntentClassifierManager : IIntentClassifierManager
{
    private IPromptIntentClassifier _classifier;
    private readonly System.Threading.Lock _lock = new();

    public IntentClassifierManager(IPromptIntentClassifier initialClassifier)
    {
        _classifier = initialClassifier ?? throw new ArgumentNullException(nameof(initialClassifier));
    }

    public IPromptIntentClassifier Classifier
    {
        get { lock (_lock) return _classifier; }
    }

    public void SetClassifier(IPromptIntentClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);

        lock (_lock)
        {
            var old = _classifier;
            _classifier = classifier;

            if (old is IDisposable d)
                d.Dispose();
        }
    }

    public void ReloadCurrent()
    {
        lock (_lock)
        {
            if (_classifier is IHotSwappableClassifier swappable)
                swappable.Reload();
        }
    }
}

/// <summary>
/// Optional interface implemented by classifiers that support hot-reloading
/// of their underlying model without full replacement.
/// </summary>
public interface IHotSwappableClassifier
{
    void Reload();
}

/// <summary>
/// Wraps an <see cref="IIntentClassifierManager"/> and a <see cref="IPromptIntentClassifier"/>
/// so that callers can use the manager transparently without knowing about hot-swapping.
/// </summary>
public sealed class HotSwappingIntentClassifier : IPromptIntentClassifier, IDisposable
{
    private readonly IIntentClassifierManager _manager;

    public HotSwappingIntentClassifier(IIntentClassifierManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public IntentClassification Classify(string prompt)
        => _manager.Classifier.Classify(prompt);

    public void Dispose()
    {
        if (_manager is IDisposable d)
            d.Dispose();
    }
}

/// <summary>
/// Background service that watches the ML.NET model file for changes and hot-swaps
/// the active classifier when the file is updated.
/// </summary>
public sealed class IntentClassifierFileWatcher : IHostedService, IDisposable
{
    private readonly IIntentClassifierManager _manager;
    private readonly string _modelPath;
    private readonly ILogger<IntentClassifierFileWatcher> _log;
    private readonly System.Threading.Lock _reloadLock = new();
    private volatile bool _isReloading;
    private FileSystemWatcher? _watcher;

    public IntentClassifierFileWatcher(
        IIntentClassifierManager manager,
        string modelPath,
        ILogger<IntentClassifierFileWatcher>? log = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _modelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        _log = log ?? NullLogger<IntentClassifierFileWatcher>.Instance;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_modelPath))
        {
            _log.LogWarning(
                "[IntentClassifierFileWatcher] Model file not found at {Path} — hot-swap disabled",
                _modelPath);
            return Task.CompletedTask;
        }

        var dir = Path.GetDirectoryName(_modelPath) ?? ".";
        var name = Path.GetFileName(_modelPath);

        _watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnChanged;
        _log.LogInformation(
            "[IntentClassifierFileWatcher] Watching {Path} for changes", _modelPath);

        return Task.CompletedTask;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        lock (_reloadLock)
        {
            if (_isReloading) return;
            _isReloading = true;
        }

        _log.LogInformation(
            "[IntentClassifierFileWatcher] Model file changed ({ChangeType}) — reloading",
            e.ChangeType);

        try
        {
            // Small delay to ensure file write is complete
            Thread.Sleep(500);
            _manager.ReloadCurrent();
            _log.LogInformation("[IntentClassifierFileWatcher] Model reloaded successfully");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[IntentClassifierFileWatcher] Failed to reload model");
        }
        finally
        {
            _isReloading = false;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
