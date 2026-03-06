using JD.AI.Core.Tools;
using JD.AI.Core.Providers;

namespace JD.AI.Tests;

public sealed class UsageToolsTests
{
    [Fact]
    public void GetUsage_WithNoRecords_ShowsZeros()
    {
        var tools = new UsageTools();

        var result = tools.GetUsage();

        Assert.Contains("Turns: 0", result);
        Assert.Contains("Prompt tokens: 0", result);
        Assert.Contains("Total tokens: 0", result);
    }

    [Fact]
    public void GetUsage_AfterRecording_ShowsAccumulated()
    {
        var tools = new UsageTools();
        tools.RecordUsage(100, 50, 3);
        tools.RecordUsage(200, 100, 2);

        var result = tools.GetUsage();

        Assert.Contains("Turns: 2", result);
        Assert.Contains("Prompt tokens: 300", result);
        Assert.Contains("Completion tokens: 150", result);
        Assert.Contains("Total tokens: 450", result);
        Assert.Contains("Tool calls: 5", result);
    }

    [Fact]
    public void ResetUsage_ClearsCounters()
    {
        var tools = new UsageTools();
        tools.RecordUsage(500, 250, 10);

        var resetResult = tools.ResetUsage();
        Assert.Contains("reset", resetResult, StringComparison.OrdinalIgnoreCase);

        var result = tools.GetUsage();
        Assert.Contains("Turns: 0", result);
        Assert.Contains("Total tokens: 0", result);
    }

    [Fact]
    public void GetUsage_WithoutCurrentModel_ShowsModelRequiredMessage()
    {
        var tools = new UsageTools();
        tools.RecordUsage(1000, 500, 5);

        var result = tools.GetUsage();

        Assert.Contains("Current model not set", result);
    }

    [Fact]
    public void GetUsage_WithCurrentModel_UsesCentralCostEstimator()
    {
        var tools = new UsageTools();
        tools.SetModel(new ProviderModelInfo("gpt-4.1", "gpt-4.1", "OpenAI"));
        tools.RecordUsage(1000, 500, 5);

        var result = tools.GetUsage();

        Assert.Contains("Rate source: cost-rate-provider", result);
        Assert.Contains("Total:  $0.006000", result);
    }
}
