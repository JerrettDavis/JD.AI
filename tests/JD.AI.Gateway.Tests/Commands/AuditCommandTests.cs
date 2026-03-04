using FluentAssertions;
using JD.AI.Core.Commands;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Gateway.Commands;

namespace JD.AI.Gateway.Tests.Commands;

public sealed class AuditCommandTests
{
    private static CommandContext MakeContext(Dictionary<string, string>? args = null) => new()
    {
        CommandName = "audit",
        InvokerId = "user123",
        ChannelId = "ch456",
        ChannelType = "discord",
        Arguments = args ?? new Dictionary<string, string>(StringComparer.Ordinal),
    };

    private static async Task<InMemoryAuditSink> SeedSink()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(new AuditEvent
        {
            Action = "tool.invoke",
            Resource = "ReadFile",
            Severity = AuditSeverity.Debug,
        });
        await sink.WriteAsync(new AuditEvent
        {
            Action = "session.start",
            Severity = AuditSeverity.Info,
        });
        await sink.WriteAsync(new AuditEvent
        {
            Action = "tool.invoke",
            Resource = "WriteFile",
            Severity = AuditSeverity.Warning,
            PolicyResult = PolicyDecision.Deny,
        });
        return sink;
    }

    [Fact]
    public async Task ExecuteAsync_ShowsAllEvents_ByDefault()
    {
        var sink = await SeedSink();
        var cmd = new AuditCommand(sink);

        var result = await cmd.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Audit Log");
        result.Content.Should().Contain("3 total events");
        result.Content.Should().Contain("tool.invoke");
        result.Content.Should().Contain("session.start");
    }

    [Fact]
    public async Task ExecuteAsync_FiltersBySeverity()
    {
        var sink = await SeedSink();
        var cmd = new AuditCommand(sink);

        var result = await cmd.ExecuteAsync(MakeContext(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["severity"] = "warning" }));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("1 total events");
        result.Content.Should().Contain("tool.invoke");
        result.Content.Should().NotContain("session.start");
    }

    [Fact]
    public async Task ExecuteAsync_RespectsLimit()
    {
        var sink = await SeedSink();
        var cmd = new AuditCommand(sink);

        var result = await cmd.ExecuteAsync(MakeContext(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["limit"] = "1" }));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("showing 1");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyStore_ShowsNoEvents()
    {
        var sink = new InMemoryAuditSink();
        var cmd = new AuditCommand(sink);

        var result = await cmd.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("No audit events found");
    }
}
