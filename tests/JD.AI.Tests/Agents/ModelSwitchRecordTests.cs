using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class ModelSwitchRecordTests
{
    [Fact]
    public void Construction_AllProperties()
    {
        var ts = DateTimeOffset.UtcNow;
        var record = new ModelSwitchRecord(ts, "claude-3.5-sonnet", "Anthropic", "preserve");
        record.Timestamp.Should().Be(ts);
        record.ModelId.Should().Be("claude-3.5-sonnet");
        record.ProviderName.Should().Be("Anthropic");
        record.SwitchMode.Should().Be("preserve");
    }

    [Fact]
    public void RecordEquality()
    {
        var ts = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new ModelSwitchRecord(ts, "m1", "p1", "compact");
        var b = new ModelSwitchRecord(ts, "m1", "p1", "compact");
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality_DifferentMode()
    {
        var ts = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new ModelSwitchRecord(ts, "m1", "p1", "preserve");
        var b = new ModelSwitchRecord(ts, "m1", "p1", "compact");
        a.Should().NotBe(b);
    }

    [Theory]
    [InlineData("preserve")]
    [InlineData("compact")]
    [InlineData("transform")]
    [InlineData("fresh")]
    public void SwitchMode_AcceptsAllValidModes(string mode)
    {
        var record = new ModelSwitchRecord(DateTimeOffset.UtcNow, "m", "p", mode);
        record.SwitchMode.Should().Be(mode);
    }
}
