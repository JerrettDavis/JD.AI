namespace JD.AI.Core.Governance.Audit;

public interface IAuditSink
{
    string Name { get; }
    Task WriteAsync(AuditEvent evt, CancellationToken ct = default);
    Task FlushAsync(CancellationToken ct = default);
}
