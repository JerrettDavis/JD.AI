using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Governance.Audit;

/// <summary>
/// Writes audit events as JSON lines to a daily-rotated file under
/// <c>{baseDir}/audit-{yyyy-MM-dd}.jsonl</c>.
/// </summary>
public sealed class FileAuditSink : IAuditSink, IDisposable
{
    private readonly string _baseDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Compact;

    public FileAuditSink(string baseDir)
    {
        ArgumentNullException.ThrowIfNull(baseDir);
        _baseDir = baseDir;
    }

    public string Name => "file";

    /// <inheritdoc/>
    public async Task WriteAsync(AuditEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var line = JsonSerializer.Serialize(evt, JsonOptions);
        var filePath = GetFilePath(evt.Timestamp);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_baseDir);
            await File.AppendAllTextAsync(filePath, line + Environment.NewLine, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken ct = default)
    {
        // File writes are already flushed — no buffering.
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();

    private string GetFilePath(DateTimeOffset timestamp) =>
        Path.Combine(_baseDir, $"audit-{timestamp:yyyy-MM-dd}.jsonl");
}
