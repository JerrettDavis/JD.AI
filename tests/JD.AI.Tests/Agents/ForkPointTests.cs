using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class ForkPointTests
{
    [Fact]
    public void ForkPoint_DefaultValues()
    {
        var fp = new ForkPoint();
        fp.Id.Should().Be(0);
        fp.Timestamp.Should().Be(default);
        fp.ModelId.Should().Be(string.Empty);
        fp.ProviderName.Should().Be(string.Empty);
        fp.TurnIndex.Should().Be(0);
        fp.MessageCount.Should().Be(0);
    }

    [Fact]
    public void ForkPoint_CustomValues()
    {
        var ts = DateTimeOffset.UtcNow;
        var fp = new ForkPoint
        {
            Id = 5,
            Timestamp = ts,
            ModelId = "claude-opus-4",
            ProviderName = "Anthropic",
            TurnIndex = 12,
            MessageCount = 24,
        };

        fp.Id.Should().Be(5);
        fp.Timestamp.Should().Be(ts);
        fp.ModelId.Should().Be("claude-opus-4");
        fp.ProviderName.Should().Be("Anthropic");
        fp.TurnIndex.Should().Be(12);
        fp.MessageCount.Should().Be(24);
    }
}
