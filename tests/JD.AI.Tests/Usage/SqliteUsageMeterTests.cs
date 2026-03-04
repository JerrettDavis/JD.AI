using JD.AI.Core.Usage;
using Microsoft.Data.Sqlite;

namespace JD.AI.Tests.Usage;

public sealed class SqliteUsageMeterTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteUsageMeter _sut;

    public SqliteUsageMeterTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"jdai-usage-test-{Guid.NewGuid():N}.db");
        _sut = new SqliteUsageMeter(_dbPath);
    }

    public async Task InitializeAsync() => await _sut.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _sut.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    public void Dispose()
    {
        _sut.DisposeAsync().AsTask().GetAwaiter().GetResult();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task RecordTurn_And_GetSessionUsage_ReturnsCorrectTotals()
    {
        var record = new TurnUsageRecord
        {
            SessionId = "session-1",
            ProviderId = "Claude Code",
            ModelId = "claude-sonnet-4.6",
            PromptTokens = 1000,
            CompletionTokens = 500,
            ToolCalls = 3,
            DurationMs = 1200,
            ProjectPath = "/test/project",
        };

        await _sut.RecordTurnAsync(record);
        var usage = await _sut.GetSessionUsageAsync("session-1");

        Assert.Equal(1000, usage.TotalPromptTokens);
        Assert.Equal(500, usage.TotalCompletionTokens);
        Assert.Equal(1500, usage.TotalTokens);
        Assert.Equal(1, usage.TotalTurns);
        Assert.Equal(3, usage.TotalToolCalls);
        Assert.True(usage.EstimatedCostUsd > 0);
    }

    [Fact]
    public async Task MultipleProviders_BreaksDownByProvider()
    {
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "s1", ProviderId = "Claude Code", ModelId = "claude-sonnet-4.6",
            PromptTokens = 500, CompletionTokens = 200,
        });
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "s1", ProviderId = "Ollama", ModelId = "llama3",
            PromptTokens = 1000, CompletionTokens = 400,
        });

        var usage = await _sut.GetSessionUsageAsync("s1");

        Assert.Equal(2, usage.ByProvider.Count);
        Assert.True(usage.ByProvider.ContainsKey("Claude Code"));
        Assert.True(usage.ByProvider.ContainsKey("Ollama"));
        Assert.Equal(500, usage.ByProvider["Claude Code"].PromptTokens);
        Assert.Equal(1000, usage.ByProvider["Ollama"].PromptTokens);
    }

    [Fact]
    public async Task GetTotalUsage_AcrossSessions()
    {
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "a", ProviderId = "Claude Code", ModelId = "claude-sonnet-4.6",
            PromptTokens = 100, CompletionTokens = 50,
        });
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "b", ProviderId = "Claude Code", ModelId = "claude-sonnet-4.6",
            PromptTokens = 200, CompletionTokens = 100,
        });

        var total = await _sut.GetTotalUsageAsync();

        Assert.Equal(300, total.TotalPromptTokens);
        Assert.Equal(150, total.TotalCompletionTokens);
        Assert.Equal(2, total.TotalTurns);
    }

    [Fact]
    public async Task GetProjectUsage_FiltersCorrectly()
    {
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "s1", ProviderId = "Ollama", ModelId = "llama3",
            PromptTokens = 500, CompletionTokens = 200, ProjectPath = "/project/a",
        });
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "s2", ProviderId = "Ollama", ModelId = "llama3",
            PromptTokens = 300, CompletionTokens = 100, ProjectPath = "/project/b",
        });

        var usage = await _sut.GetProjectUsageAsync("/project/a");

        Assert.Equal(500, usage.TotalPromptTokens);
        Assert.Equal(1, usage.TotalTurns);
    }

    [Fact]
    public async Task CheckBudget_NoBudget_ReturnsNotExceeded()
    {
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "s1", ProviderId = "Claude Code", ModelId = "claude-sonnet-4.6",
            PromptTokens = 10000, CompletionTokens = 5000,
        });

        var status = await _sut.CheckBudgetAsync(BudgetPeriod.Monthly);

        Assert.False(status.IsExceeded);
        Assert.Null(status.LimitUsd);
    }

    [Fact]
    public async Task CheckBudget_WithBudget_DetectsExceeded()
    {
        var budget = new BudgetConfig { SessionLimitUsd = 0.001m };
        var dbPath = Path.Combine(Path.GetTempPath(), $"jdai-budget-test-{Guid.NewGuid():N}.db");

        try
        {
            var meter = new SqliteUsageMeter(dbPath, budget: budget);
            await meter.InitializeAsync();

            await meter.RecordTurnAsync(new TurnUsageRecord
            {
                SessionId = "s1", ProviderId = "Claude Code", ModelId = "claude-opus-4.6",
                PromptTokens = 100_000, CompletionTokens = 50_000,
            });

            var total = await meter.GetTotalUsageAsync();
            Assert.True(total.TotalTokens > 0, $"Total tokens: {total.TotalTokens}");
            Assert.True(total.EstimatedCostUsd > 0, $"Total cost: {total.EstimatedCostUsd}");

            var status = await meter.CheckBudgetAsync(BudgetPeriod.Session);

            Assert.True(status.SpentUsd > 0, $"SpentUsd was {status.SpentUsd}, total cost was {total.EstimatedCostUsd}");
            Assert.True(status.IsExceeded, $"Budget not exceeded: spent ${status.SpentUsd}, limit ${status.LimitUsd}");

            await meter.DisposeAsync();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Export_Json_ReturnsValidJson()
    {
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "s1", ProviderId = "Ollama", ModelId = "llama3",
            PromptTokens = 100, CompletionTokens = 50,
        });

        var json = await _sut.ExportAsync(UsageExportFormat.Json);

        Assert.Contains("session_id", json);
        Assert.Contains("s1", json);
    }

    [Fact]
    public async Task Export_Csv_ContainsHeaders()
    {
        await _sut.RecordTurnAsync(new TurnUsageRecord
        {
            SessionId = "s1", ProviderId = "Ollama", ModelId = "llama3",
            PromptTokens = 100, CompletionTokens = 50,
        });

        var csv = await _sut.ExportAsync(UsageExportFormat.Csv);

        Assert.StartsWith("session_id,", csv);
        Assert.Contains("s1", csv);
    }

    [Fact]
    public async Task EmptyDatabase_ReturnsZeroUsage()
    {
        var usage = await _sut.GetTotalUsageAsync();

        Assert.Equal(0, usage.TotalTokens);
        Assert.Equal(0, usage.TotalTurns);
        Assert.Equal(0m, usage.EstimatedCostUsd);
    }
}
