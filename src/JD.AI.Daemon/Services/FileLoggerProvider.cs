using Microsoft.Extensions.Logging;

namespace JD.AI.Daemon.Services;

/// <summary>
/// Lightweight file logger that appends to a single log file with daily rotation.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _basePath;
    private readonly Lock _lock = new();
    private StreamWriter? _writer;
    private string _currentDate = "";

    public FileLoggerProvider(string filePath) => _basePath = filePath;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    internal void WriteEntry(string category, LogLevel level, string message)
    {
        if (level < LogLevel.Information) return;

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        lock (_lock)
        {
            if (!string.Equals(_currentDate, today, StringComparison.Ordinal) || _writer is null)
            {
                _writer?.Dispose();
                var dir = Path.GetDirectoryName(_basePath)!;
                var name = Path.GetFileNameWithoutExtension(_basePath);
                var ext = Path.GetExtension(_basePath);
                var path = Path.Combine(dir, $"{name}-{today}{ext}");
                _writer = new StreamWriter(path, append: true) { AutoFlush = true };
                _currentDate = today;
            }

            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            var lvl = level switch
            {
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => level.ToString()[..3].ToUpperInvariant()
            };
            _writer.WriteLine($"{ts} [{lvl}] {category}: {message}");
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var msg = formatter(state, exception);
            if (exception is not null) msg += $"\n  {exception}";
            provider.WriteEntry(category, logLevel, msg);
        }
    }
}
