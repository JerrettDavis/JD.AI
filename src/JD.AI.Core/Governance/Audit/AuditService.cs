namespace JD.AI.Core.Governance.Audit;

/// <summary>
/// Dispatches <see cref="AuditEvent"/> instances to all registered <see cref="IAuditSink"/>
/// implementations.  A failure in one sink never propagates to callers.
/// </summary>
public sealed class AuditService
{
    private readonly IReadOnlyList<IAuditSink> _sinks;

    public AuditService(IEnumerable<IAuditSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <summary>
    /// Emits an audit event to all configured sinks.  Exceptions from individual
    /// sinks are caught and swallowed so that audit failures never break the application.
    /// </summary>
    public async Task EmitAsync(AuditEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        foreach (var sink in _sinks)
        {
            try
            {
                await sink.WriteAsync(evt, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Audit failure must not break the application.
            }
        }
    }

    /// <summary>Flushes all sinks.  Sink flush failures are swallowed.</summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
        {
            try
            {
                await sink.FlushAsync(ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Audit failure must not break the application.
            }
        }
    }
}
