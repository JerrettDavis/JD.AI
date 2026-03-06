using FluentAssertions;
using JD.AI.Core.Usage;

namespace JD.AI.Tests.Usage;

public sealed class CostRateEntryTests
{
    [Fact]
    public void CostRateEntry_Construction()
    {
        var entry = new CostRateEntry
        {
            Provider = "Anthropic",
            Model = "claude-opus-4",
            InputCostPerMillionTokens = 15.00m,
            OutputCostPerMillionTokens = 75.00m,
        };

        entry.Provider.Should().Be("Anthropic");
        entry.Model.Should().Be("claude-opus-4");
        entry.InputCostPerMillionTokens.Should().Be(15.00m);
        entry.OutputCostPerMillionTokens.Should().Be(75.00m);
    }

    [Fact]
    public void CostRateEntry_RecordEquality()
    {
        var a = new CostRateEntry
        {
            Provider = "OpenAI",
            Model = "gpt-4o",
            InputCostPerMillionTokens = 2.50m,
            OutputCostPerMillionTokens = 10.00m,
        };
        var b = new CostRateEntry
        {
            Provider = "OpenAI",
            Model = "gpt-4o",
            InputCostPerMillionTokens = 2.50m,
            OutputCostPerMillionTokens = 10.00m,
        };

        a.Should().Be(b);
    }

    [Fact]
    public void CostRateEntry_RecordInequality()
    {
        var a = new CostRateEntry
        {
            Provider = "OpenAI",
            Model = "gpt-4o",
            InputCostPerMillionTokens = 2.50m,
            OutputCostPerMillionTokens = 10.00m,
        };
        var b = new CostRateEntry
        {
            Provider = "OpenAI",
            Model = "gpt-4o-mini",
            InputCostPerMillionTokens = 0.15m,
            OutputCostPerMillionTokens = 0.60m,
        };

        a.Should().NotBe(b);
    }

    [Fact]
    public void CostRateEntry_DefaultCosts_AreZero()
    {
        var entry = new CostRateEntry
        {
            Provider = "Local",
            Model = "test",
        };

        entry.InputCostPerMillionTokens.Should().Be(0m);
        entry.OutputCostPerMillionTokens.Should().Be(0m);
    }

    // ── CostRateProvider additional coverage ─────────────────────────────

    [Fact]
    public void CostRateProvider_SetRate_AddNew_ThenRetrieve()
    {
        var provider = new CostRateProvider();
        provider.SetRate("CustomProvider", "custom-model", 5.0m, 25.0m);

        var (input, output) = provider.GetRate("CustomProvider", "custom-model");
        input.Should().Be(5.0m / 1_000_000m);
        output.Should().Be(25.0m / 1_000_000m);
    }

    [Fact]
    public void CostRateProvider_CalculateCost_ZeroTokens_ReturnsZero()
    {
        var provider = new CostRateProvider();
        var cost = provider.CalculateCost("Claude Code", "claude-opus-4.6", 0, 0);
        cost.Should().Be(0m);
    }
}
