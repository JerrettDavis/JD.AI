using FluentAssertions;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Providers.Credentials;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests.Providers.Credentials;

public class AuditingCredentialStoreTests
{
    [Fact]
    public async Task GetAsync_EmitsReadEvent()
    {
        var inner = Substitute.For<ICredentialStore>();
        inner.IsAvailable.Returns(true);
        inner.StoreName.Returns("Inner");
        inner.GetAsync("key", Arg.Any<CancellationToken>()).Returns("value");

        var audit = Substitute.For<IAuditSink>();
        var store = new AuditingCredentialStore(inner, audit);

        await store.GetAsync("key");

        await audit.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(e => e.Action == "secret.read" && e.Resource == "key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_EmitsWriteEvent()
    {
        var inner = Substitute.For<ICredentialStore>();
        inner.IsAvailable.Returns(true);
        inner.StoreName.Returns("Inner");

        var audit = Substitute.For<IAuditSink>();
        var store = new AuditingCredentialStore(inner, audit);

        await store.SetAsync("key", "secret-value");

        await audit.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(e => e.Action == "secret.write"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_EmitsDeleteEvent()
    {
        var inner = Substitute.For<ICredentialStore>();
        inner.IsAvailable.Returns(true);
        inner.StoreName.Returns("Inner");

        var audit = Substitute.For<IAuditSink>();
        var store = new AuditingCredentialStore(inner, audit);

        await store.RemoveAsync("key");

        await audit.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(e => e.Action == "secret.delete"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuditFailure_DoesNotBreakCredentialAccess()
    {
        var inner = Substitute.For<ICredentialStore>();
        inner.IsAvailable.Returns(true);
        inner.StoreName.Returns("Inner");
        inner.GetAsync("key", Arg.Any<CancellationToken>()).Returns("value");

        var audit = Substitute.For<IAuditSink>();
        audit.WriteAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Audit sink broken")));

        var store = new AuditingCredentialStore(inner, audit);
        var result = await store.GetAsync("key");

        result.Should().Be("value");
    }

    [Fact]
    public async Task NullAuditSink_StillWorks()
    {
        var inner = Substitute.For<ICredentialStore>();
        inner.IsAvailable.Returns(true);
        inner.StoreName.Returns("Inner");
        inner.GetAsync("key", Arg.Any<CancellationToken>()).Returns("value");

        var store = new AuditingCredentialStore(inner, auditSink: null);
        var result = await store.GetAsync("key");

        result.Should().Be("value");
    }
}
