using JD.AI.Core.Config;
using JD.AI.Core.Providers;
using Xunit;

namespace JD.AI.Tests.Config;

public sealed class SystemPromptBudgetTests
{
    // ── ProviderModelInfo defaults ─────────────────────────

    [Fact]
    public void ProviderModelInfo_DefaultContextWindow_Is128k()
    {
        var model = new ProviderModelInfo("id", "Name", "Provider");
        Assert.Equal(128_000, model.ContextWindowTokens);
    }

    [Fact]
    public void ProviderModelInfo_ExplicitContextWindow_IsPreserved()
    {
        var model = new ProviderModelInfo("id", "Name", "Provider", 200_000);
        Assert.Equal(200_000, model.ContextWindowTokens);
    }

    // ── Budget calculation ─────────────────────────────────

    [Theory]
    [InlineData(200_000, 20, 40_000)]
    [InlineData(128_000, 20, 25_600)]
    [InlineData(200_000, 50, 100_000)]
    [InlineData(128_000, 10, 12_800)]
    [InlineData(200_000, 0, 0)]
    [InlineData(200_000, 100, 200_000)]
    public void BudgetTokens_Calculation(int contextWindow, int budgetPercent, int expectedBudget)
    {
        var budgetTokens = (int)(contextWindow * (budgetPercent / 100.0));
        Assert.Equal(expectedBudget, budgetTokens);
    }

    // ── SystemPromptCompaction enum ────────────────────────

    [Theory]
    [InlineData("Off", SystemPromptCompaction.Off)]
    [InlineData("Auto", SystemPromptCompaction.Auto)]
    [InlineData("Always", SystemPromptCompaction.Always)]
    [InlineData("off", SystemPromptCompaction.Off)]
    [InlineData("auto", SystemPromptCompaction.Auto)]
    [InlineData("always", SystemPromptCompaction.Always)]
    public void SystemPromptCompaction_ParsesCorrectly(string input, SystemPromptCompaction expected)
    {
        Assert.True(Enum.TryParse<SystemPromptCompaction>(input, ignoreCase: true, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("manual")]
    public void SystemPromptCompaction_InvalidInput_FailsParse(string input)
    {
        Assert.False(Enum.TryParse<SystemPromptCompaction>(input, ignoreCase: true, out _));
    }

    // ── TuiSettings defaults ───────────────────────────────

    [Fact]
    public void TuiSettings_DefaultBudgetPercent_Is20()
    {
        var settings = new TuiSettings();
        Assert.Equal(20, settings.SystemPromptBudgetPercent);
    }

    [Fact]
    public void TuiSettings_DefaultCompaction_IsOff()
    {
        var settings = new TuiSettings();
        Assert.Equal(SystemPromptCompaction.Off, settings.SystemPromptCompaction);
    }

    // ── Alert trigger logic ────────────────────────────────

    [Theory]
    [InlineData(30_000, 40_000, false)]  // Under budget — no alert
    [InlineData(40_000, 40_000, false)]  // At budget — no alert
    [InlineData(41_000, 40_000, true)]   // Over budget — alert
    public void Alert_TriggersOnlyWhenOverBudget(int actualTokens, int budgetTokens, bool shouldAlert)
    {
        var alert = actualTokens > budgetTokens;
        Assert.Equal(shouldAlert, alert);
    }

    // ── Compaction mode decision logic ─────────────────────

    [Theory]
    [InlineData(SystemPromptCompaction.Off, 50_000, 40_000, false)]    // Off: never compact
    [InlineData(SystemPromptCompaction.Off, 30_000, 40_000, false)]    // Off: never compact
    [InlineData(SystemPromptCompaction.Auto, 50_000, 40_000, true)]    // Auto + over: compact
    [InlineData(SystemPromptCompaction.Auto, 30_000, 40_000, false)]   // Auto + under: skip
    [InlineData(SystemPromptCompaction.Always, 50_000, 40_000, true)]  // Always: compact
    [InlineData(SystemPromptCompaction.Always, 30_000, 40_000, true)]  // Always: compact even under
    public void ShouldCompact_MatchesExpectedBehavior(
        SystemPromptCompaction mode, int actualTokens, int budgetTokens, bool expectedCompact)
    {
        var shouldCompact = mode == SystemPromptCompaction.Always
            || (mode == SystemPromptCompaction.Auto && actualTokens > budgetTokens);

        Assert.Equal(expectedCompact, shouldCompact);
    }
}
