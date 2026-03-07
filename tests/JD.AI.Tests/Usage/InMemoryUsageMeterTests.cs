using JD.AI.Core.Usage;

namespace JD.AI.Tests.Usage;

public sealed class InMemoryUsageMeterTests
{
    private readonly InMemoryUsageMeter _sut = new();

    private static TurnUsageRecord MakeRecord(
        string sessionId = "session-1",
        string projectPath = "/test",
        long prompt = 100,
        long completion = 50,
        int tools = 1,
        DateTimeOffset? timestamp = null) =>
        new()
        {
            SessionId = sessionId,
            ProviderId = "test-provider",
            ModelId = "test-model",
            PromptTokens = prompt,
            CompletionTokens = completion,
            ToolCalls = tools,
            ProjectPath = projectPath,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        };

    [Fact]
    public async Task RecordTurn_IncrementsCount()
    {
        await _sut.RecordTurnAsync(MakeRecord());
        Assert.Equal(1, _sut.RecordCount);
    }

    [Fact]
    public async Task GetSessionUsage_SumsCorrectly()
    {
        await _sut.RecordTurnAsync(MakeRecord(prompt: 100, completion: 50, tools: 2));
        await _sut.RecordTurnAsync(MakeRecord(prompt: 200, completion: 100, tools: 3));
        await _sut.RecordTurnAsync(MakeRecord(sessionId: "other", prompt: 999, completion: 999));

        var usage = await _sut.GetSessionUsageAsync("session-1");

        Assert.Equal(2, usage.TotalTurns);
        Assert.Equal(300, usage.TotalPromptTokens);
        Assert.Equal(150, usage.TotalCompletionTokens);
        Assert.Equal(450, usage.TotalTokens);
        Assert.Equal(5, usage.TotalToolCalls);
    }

    [Fact]
    public async Task GetSessionUsage_CaseInsensitive()
    {
        await _sut.RecordTurnAsync(MakeRecord(sessionId: "Session-1"));

        var usage = await _sut.GetSessionUsageAsync("session-1");
        Assert.Equal(1, usage.TotalTurns);
    }

    [Fact]
    public async Task GetProjectUsage_FiltersByProject()
    {
        await _sut.RecordTurnAsync(MakeRecord(projectPath: "/project-a", prompt: 100));
        await _sut.RecordTurnAsync(MakeRecord(projectPath: "/project-b", prompt: 200));

        var usage = await _sut.GetProjectUsageAsync("/project-a");

        Assert.Equal(1, usage.TotalTurns);
        Assert.Equal(100, usage.TotalPromptTokens);
    }

    [Fact]
    public async Task GetPeriodUsage_FiltersByTimeRange()
    {
        var now = DateTimeOffset.UtcNow;
        await _sut.RecordTurnAsync(MakeRecord(timestamp: now.AddHours(-2), prompt: 100));
        await _sut.RecordTurnAsync(MakeRecord(timestamp: now, prompt: 200));
        await _sut.RecordTurnAsync(MakeRecord(timestamp: now.AddHours(2), prompt: 300));

        var usage = await _sut.GetPeriodUsageAsync(now.AddHours(-1), now.AddHours(1));

        Assert.Equal(1, usage.TotalTurns);
        Assert.Equal(200, usage.TotalPromptTokens);
    }

    [Fact]
    public async Task GetTotalUsage_IncludesAllRecords()
    {
        await _sut.RecordTurnAsync(MakeRecord(sessionId: "a", prompt: 100));
        await _sut.RecordTurnAsync(MakeRecord(sessionId: "b", prompt: 200));

        var usage = await _sut.GetTotalUsageAsync();

        Assert.Equal(2, usage.TotalTurns);
        Assert.Equal(300, usage.TotalPromptTokens);
    }

    [Fact]
    public async Task CheckBudget_ReturnsPeriod()
    {
        await _sut.RecordTurnAsync(MakeRecord());

        var status = await _sut.CheckBudgetAsync(BudgetPeriod.Daily);
        Assert.Equal(BudgetPeriod.Daily, status.Period);
    }

    [Fact]
    public async Task ExportAsync_ReturnsRecordCount()
    {
        await _sut.RecordTurnAsync(MakeRecord());
        await _sut.RecordTurnAsync(MakeRecord());

        var export = await _sut.ExportAsync(UsageExportFormat.Csv);
        Assert.Contains("2 records", export);
    }

    [Fact]
    public async Task EmptyMeter_ReturnsZeroSummary()
    {
        var usage = await _sut.GetTotalUsageAsync();

        Assert.Equal(0, usage.TotalTurns);
        Assert.Equal(0, usage.TotalPromptTokens);
        Assert.Equal(0, usage.TotalCompletionTokens);
    }
}
