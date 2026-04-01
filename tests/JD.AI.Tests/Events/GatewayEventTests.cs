using FluentAssertions;
using JD.AI.Core.Events;

namespace JD.AI.Tests.Events;

/// <summary>
/// Tests for <see cref="GatewayEvent"/> record construction, equality,
/// and Id generation.
/// </summary>
public sealed class GatewayEventTests
{
    [Fact]
    public void Constructor_GeneratesNonEmptyId()
    {
        var evt = new GatewayEvent("test.type", "src-1", DateTimeOffset.UtcNow);

        evt.Id.Should().NotBeNullOrEmpty();
        evt.Id.Should().HaveLength(32); // 16-char hex GUID = 32 chars
    }

    [Fact]
    public void Constructor_SetsAllFields()
    {
        var ts = DateTimeOffset.UtcNow;
        var evt = new GatewayEvent("my.event", "my-source", ts, "payload");

        evt.EventType.Should().Be("my.event");
        evt.SourceId.Should().Be("my-source");
        evt.Timestamp.Should().Be(ts);
        evt.Payload.Should().Be("payload");
    }

    [Fact]
    public void Constructor_DefaultsPayloadToNull()
    {
        var evt = new GatewayEvent("type", "src", DateTimeOffset.UtcNow);

        evt.Payload.Should().BeNull();
    }

    [Fact]
    public void RecordsAreValueEqual_SameValues()
    {
        var ts = DateTimeOffset.UtcNow;
        var evt1 = new GatewayEvent("type", "src", ts, "x");
        var evt2 = new GatewayEvent("type", "src", ts, "x");

        // Id is auto-generated on construction, so use BeEquivalentTo to compare
        // all fields except the auto-generated Id.
        evt1.Should().BeEquivalentTo(evt2, opts => opts.Excluding(e => e.Id));
    }

    [Fact]
    public void RecordsAreValueNotEqual_DifferentEventType()
    {
        var ts = DateTimeOffset.UtcNow;
        var evt1 = new GatewayEvent("type1", "src", ts);
        var evt2 = new GatewayEvent("type2", "src", ts);

        evt1.Should().NotBe(evt2);
    }

    [Fact]
    public void RecordsAreValueNotEqual_DifferentSourceId()
    {
        var ts = DateTimeOffset.UtcNow;
        var evt1 = new GatewayEvent("type", "src1", ts);
        var evt2 = new GatewayEvent("type", "src2", ts);

        evt1.Should().NotBe(evt2);
    }

    [Fact]
    public void EachConstruction_ProducesUniqueId()
    {
        var evt1 = new GatewayEvent("type", "src", DateTimeOffset.UtcNow);
        var evt2 = new GatewayEvent("type", "src", DateTimeOffset.UtcNow);

        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Theory]
    [InlineData("tool.executed")]
    [InlineData("session.start")]
    [InlineData("session.end")]
    [InlineData("memory.consolidated")]
    public void EventType_CanBeAnyString(string eventType)
    {
        var evt = new GatewayEvent(eventType, "src", DateTimeOffset.UtcNow);

        evt.EventType.Should().Be(eventType);
    }
}
