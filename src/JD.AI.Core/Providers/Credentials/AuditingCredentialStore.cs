using JD.AI.Core.Governance.Audit;

namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Decorator that logs all credential access to the <see cref="IAuditSink"/>.
/// Wraps any <see cref="ICredentialStore"/> to provide secret access auditing.
/// </summary>
public sealed class AuditingCredentialStore : ICredentialStore
{
    private readonly ICredentialStore _inner;
    private readonly IAuditSink? _auditSink;

    public AuditingCredentialStore(ICredentialStore inner, IAuditSink? auditSink = null)
    {
        _inner = inner;
        _auditSink = auditSink;
    }

    public bool IsAvailable => _inner.IsAvailable;
    public string StoreName => $"Audited({_inner.StoreName})";

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var value = await _inner.GetAsync(key, ct).ConfigureAwait(false);
        await EmitAsync("secret.read", key, value is not null ? "found" : "not_found", ct)
            .ConfigureAwait(false);
        return value;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await _inner.SetAsync(key, value, ct).ConfigureAwait(false);
        await EmitAsync("secret.write", key, "stored", ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _inner.RemoveAsync(key, ct).ConfigureAwait(false);
        await EmitAsync("secret.delete", key, "removed", ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default) =>
        _inner.ListKeysAsync(prefix, ct);

    private async Task EmitAsync(string action, string key, string outcome, CancellationToken ct)
    {
        if (_auditSink is null) return;

        // Never log actual secret values — only the key name and outcome
        var evt = new AuditEvent
        {
            Action = action,
            Resource = key,
            Severity = AuditSeverity.Info,
            Detail = outcome,
        };

        try
        {
            await _auditSink.WriteAsync(evt, ct).ConfigureAwait(false);
        }
        catch
        {
            // Audit failures must not break credential access
        }
    }
}
