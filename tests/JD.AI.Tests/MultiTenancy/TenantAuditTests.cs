using JD.AI.Core.Governance.Audit;
using JD.AI.Core.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.MultiTenancy;

public sealed class TenantAuditTests
{
    [Fact]
    public async Task EmitAsync_StampsTenantId_FromContext()
    {
        var sink = new InMemoryAuditSink();
        var ctx = new TenantContext { TenantId = "team-alpha" };
        var svc = new AuditService([sink], NullLogger<AuditService>.Instance, ctx);

        await svc.EmitAsync(new AuditEvent { Action = "login" });

        var result = await sink.QueryAsync(new AuditQuery());
        Assert.Single(result.Events);
        Assert.Equal("team-alpha", result.Events[0].TenantId);
    }

    [Fact]
    public async Task EmitAsync_DoesNotOverrideExistingTenantId()
    {
        var sink = new InMemoryAuditSink();
        var ctx = new TenantContext { TenantId = "ctx-tenant" };
        var svc = new AuditService([sink], NullLogger<AuditService>.Instance, ctx);
        var evt = new AuditEvent { Action = "test", TenantId = "explicit-tenant" };

        await svc.EmitAsync(evt);

        var result = await sink.QueryAsync(new AuditQuery());
        Assert.Equal("explicit-tenant", result.Events[0].TenantId);
    }

    [Fact]
    public async Task QueryAsync_FiltersByTenantId()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(new AuditEvent { Action = "a", TenantId = "alpha" });
        await sink.WriteAsync(new AuditEvent { Action = "b", TenantId = "beta" });
        await sink.WriteAsync(new AuditEvent { Action = "c", TenantId = "alpha" });

        var result = await sink.QueryAsync(new AuditQuery { TenantId = "alpha" });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Events, e => Assert.Equal("alpha", e.TenantId));
    }

    [Fact]
    public async Task QueryAsync_TenantFilter_ExcludesCrossTenantData()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(new AuditEvent { Action = "secret", TenantId = "acme" });
        await sink.WriteAsync(new AuditEvent { Action = "public", TenantId = "other" });

        var result = await sink.QueryAsync(new AuditQuery { TenantId = "acme" });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("secret", result.Events[0].Action);
    }
}
