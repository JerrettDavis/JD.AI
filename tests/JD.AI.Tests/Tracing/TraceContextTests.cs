using JD.AI.Core.Tracing;
using Xunit;
using ExecutionContext = JD.AI.Core.Tracing.ExecutionContext;

namespace JD.AI.Tests.Tracing;

public sealed class TraceContextTests
{
    [Fact]
    public void StartTurn_SetsCurrentContext()
    {
        var ctx = TraceContext.StartTurn("sess-1", 3);

        Assert.Equal("sess-1", ctx.SessionId);
        Assert.Equal(3, ctx.TurnIndex);
        Assert.NotEmpty(ctx.TraceId);
        Assert.NotEmpty(ctx.SpanId);
        Assert.Same(ctx, TraceContext.CurrentContext);
    }

    [Fact]
    public void CurrentContext_ReturnsEmpty_WhenNotSet()
    {
        // Reset any prior context
        TraceContext.CurrentContext = ExecutionContext.Empty;
        var ctx = TraceContext.CurrentContext;

        Assert.NotNull(ctx);
        Assert.Same(ExecutionContext.Empty, ctx);
    }

    [Fact]
    public void StartChildSpan_CreatesNewSpanId()
    {
        var ctx = TraceContext.StartTurn("sess-1", 0);
        var originalSpan = ctx.SpanId;

        var childSpan = TraceContext.StartChildSpan();
        var childCtx = TraceContext.CurrentContext;

        Assert.NotEqual(originalSpan, childSpan);
        Assert.NotSame(ctx, childCtx);
        Assert.Equal(childSpan, childCtx.SpanId);
        Assert.Equal(originalSpan, childCtx.ParentSpanId);
        Assert.Equal(ctx.TraceId, childCtx.TraceId);
        Assert.Equal(ctx.SessionId, childCtx.SessionId);
        Assert.Equal(ctx.TurnIndex, childCtx.TurnIndex);
    }

    [Fact]
    public void ExecutionContext_HasTimeline()
    {
        var ctx = TraceContext.StartTurn("sess-1", 0);
        Assert.NotNull(ctx.Timeline);
        Assert.Empty(ctx.Timeline.Entries);
    }
}

public sealed class ExecutionTimelineTests
{
    [Fact]
    public void BeginOperation_RecordsEntry()
    {
        var timeline = new ExecutionTimeline();
        var entry = timeline.BeginOperation("tool.read_file");

        Assert.Single(timeline.Entries);
        Assert.Equal("tool.read_file", entry.Operation);
        Assert.Equal("ok", entry.Status);
    }

    [Fact]
    public void Complete_SetsEndTimeAndStatus()
    {
        var timeline = new ExecutionTimeline();
        var entry = timeline.BeginOperation("tool.grep");

        entry.Complete("ok");

        Assert.True(entry.EndTime >= entry.StartTime);
        Assert.Equal("ok", entry.Status);
    }

    [Fact]
    public void Complete_WithError_SetsErrorMessage()
    {
        var timeline = new ExecutionTimeline();
        var entry = timeline.BeginOperation("tool.run_command");

        entry.Complete("error", "Command timed out");

        Assert.Equal("error", entry.Status);
        Assert.Equal("Command timed out", entry.ErrorMessage);
    }

    [Fact]
    public void Entries_OrderedByStartTime()
    {
        var timeline = new ExecutionTimeline();
        var e1 = timeline.BeginOperation("first");
        var e2 = timeline.BeginOperation("second");
        var e3 = timeline.BeginOperation("third");

        var entries = timeline.Entries;
        Assert.Equal(3, entries.Count);
        Assert.True(entries[0].StartTime <= entries[1].StartTime);
        Assert.True(entries[1].StartTime <= entries[2].StartTime);
    }

    [Fact]
    public void TotalDuration_ReflectsSpan()
    {
        var timeline = new ExecutionTimeline();
        var entry = timeline.BeginOperation("long_op");
        // Simulate some passage of time
        System.Threading.Thread.Sleep(10);
        entry.Complete();

        Assert.True(timeline.TotalDuration.TotalMilliseconds >= 5);
    }

    [Fact]
    public void Attributes_PassedOnCreation()
    {
        var timeline = new ExecutionTimeline();
        var entry = timeline.BeginOperation("tool.edit",
            attributes: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["safety_tier"] = "ConfirmOnce",
                ["file"] = "Program.cs",
            });

        Assert.Equal("ConfirmOnce", entry.Attributes["safety_tier"]);
        Assert.Equal("Program.cs", entry.Attributes["file"]);
    }
}

public sealed class DebugLoggerTests
{
    [Fact]
    public void ParseCategories_Null_ReturnsAll()
    {
        var result = DebugLogger.ParseCategories(null);
        Assert.Equal(DebugCategory.All, result);
    }

    [Fact]
    public void ParseCategories_Empty_ReturnsAll()
    {
        var result = DebugLogger.ParseCategories("");
        Assert.Equal(DebugCategory.All, result);
    }

    [Fact]
    public void ParseCategories_Single_ReturnsCategory()
    {
        var result = DebugLogger.ParseCategories("tools");
        Assert.Equal(DebugCategory.Tools, result);
    }

    [Fact]
    public void ParseCategories_Multiple_ReturnsCombined()
    {
        var result = DebugLogger.ParseCategories("tools,providers,sessions");
        Assert.True(result.HasFlag(DebugCategory.Tools));
        Assert.True(result.HasFlag(DebugCategory.Providers));
        Assert.True(result.HasFlag(DebugCategory.Sessions));
        Assert.False(result.HasFlag(DebugCategory.Agents));
    }

    [Fact]
    public void ParseCategories_CaseInsensitive()
    {
        var result = DebugLogger.ParseCategories("Tools,PROVIDERS");
        Assert.True(result.HasFlag(DebugCategory.Tools));
        Assert.True(result.HasFlag(DebugCategory.Providers));
    }

    [Fact]
    public void ParseCategories_AllKeyword()
    {
        var result = DebugLogger.ParseCategories("all");
        Assert.Equal(DebugCategory.All, result);
    }

    [Fact]
    public void IsEnabled_FalseByDefault()
    {
        // Reset
        DebugLogger.Enable(DebugCategory.None);
        Assert.False(DebugLogger.IsEnabled);
    }

    [Fact]
    public void IsEnabled_TrueWhenCategoriesSet()
    {
        DebugLogger.Enable(DebugCategory.Tools);
        Assert.True(DebugLogger.IsEnabled);
        // Clean up
        DebugLogger.Enable(DebugCategory.None);
    }

    [Fact]
    public void ParseCategories_Whitespace_ReturnsAll()
    {
        var result = DebugLogger.ParseCategories("   ");
        Assert.Equal(DebugCategory.All, result);
    }

    [Fact]
    public void ParseCategories_TrimsWhitespace()
    {
        var result = DebugLogger.ParseCategories(" tools , agents ");
        Assert.True(result.HasFlag(DebugCategory.Tools));
        Assert.True(result.HasFlag(DebugCategory.Agents));
    }

    [Fact]
    public void ParseCategories_AllInvalidNames_ReturnsAll()
    {
        var result = DebugLogger.ParseCategories("nonexistent,bogus");
        Assert.Equal(DebugCategory.All, result);
    }

    [Fact]
    public void ParseCategories_MixedValidAndInvalid_ReturnsOnlyValid()
    {
        var result = DebugLogger.ParseCategories("tools,bogus,sessions");
        Assert.Equal(DebugCategory.Tools | DebugCategory.Sessions, result);
    }

    [Theory]
    [InlineData(DebugCategory.None, 0)]
    [InlineData(DebugCategory.Tools, 1)]
    [InlineData(DebugCategory.Providers, 2)]
    [InlineData(DebugCategory.Sessions, 4)]
    [InlineData(DebugCategory.Agents, 8)]
    [InlineData(DebugCategory.Policies, 16)]
    public void DebugCategory_FlagValues(DebugCategory category, int expected) =>
        Assert.Equal(expected, (int)category);

    [Fact]
    public void DebugCategory_All_CombinesAllFlags()
    {
        var combined = DebugCategory.Tools | DebugCategory.Providers |
                       DebugCategory.Sessions | DebugCategory.Agents | DebugCategory.Policies;
        Assert.Equal(DebugCategory.All, combined);
    }
}
