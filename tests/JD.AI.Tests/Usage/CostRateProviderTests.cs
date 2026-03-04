using JD.AI.Core.Usage;

namespace JD.AI.Tests.Usage;

public sealed class CostRateProviderTests
{
    private readonly CostRateProvider _sut = new();

    [Fact]
    public void GetRate_ExactMatch_ReturnsCorrectRate()
    {
        var (input, output) = _sut.GetRate("Claude Code", "claude-sonnet-4.6");

        Assert.Equal(3.00m / 1_000_000m, input);
        Assert.Equal(15.00m / 1_000_000m, output);
    }

    [Fact]
    public void GetRate_ProviderWildcard_ReturnsProviderDefault()
    {
        var (input, output) = _sut.GetRate("Ollama", "llama3:70b");

        Assert.Equal(0m, input);
        Assert.Equal(0m, output);
    }

    [Fact]
    public void GetRate_GlobMatch_MatchesPrefix()
    {
        var (input, output) = _sut.GetRate("Anthropic", "claude-opus-99");

        Assert.Equal(15.00m / 1_000_000m, input);
        Assert.Equal(75.00m / 1_000_000m, output);
    }

    [Fact]
    public void GetRate_UnknownProvider_ReturnsZero()
    {
        var (input, output) = _sut.GetRate("SomeNewProvider", "fancy-model");

        Assert.Equal(0m, input);
        Assert.Equal(0m, output);
    }

    [Fact]
    public void CalculateCost_ReturnsCorrectValue()
    {
        var cost = _sut.CalculateCost("Claude Code", "claude-sonnet-4.6", 1000, 500);

        var expected = (1000 * 3.00m / 1_000_000m) + (500 * 15.00m / 1_000_000m);
        Assert.Equal(expected, cost);
    }

    [Fact]
    public void SetRate_OverridesExisting()
    {
        _sut.SetRate("Ollama", "*", 1.00m, 2.00m);

        var (input, output) = _sut.GetRate("Ollama", "anything");
        Assert.Equal(1.00m / 1_000_000m, input);
        Assert.Equal(2.00m / 1_000_000m, output);
    }

    [Fact]
    public void CalculateCost_FreeProvider_ReturnsZero()
    {
        var cost = _sut.CalculateCost("Ollama", "llama3", 100_000, 50_000);
        Assert.Equal(0m, cost);
    }
}
