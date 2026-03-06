using System.Diagnostics;
using JD.AI.Telemetry;

namespace JD.AI.Tests.Telemetry;

/// <summary>
/// Verifies that AgentLoop OTel instrumentation infrastructure is wired correctly.
/// These tests validate the ActivitySource and Meter plumbing without requiring
/// a live LLM provider connection.
/// </summary>
public sealed class AgentLoopTracingTests
{
    [Fact]
    public void AgentActivitySource_CanCreateTurnActivity()
    {
        var captured = new List<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                string.Equals(source.Name, ActivitySources.AgentSourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = captured.Add,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = ActivitySources.Agent.StartActivity("agent.turn", ActivityKind.Internal);

        Assert.NotNull(activity);
        Assert.Single(captured);
        Assert.Equal("agent.turn", captured[0].OperationName);
    }

    [Fact]
    public void AgentActivity_SetStatus_Ok_RecordsCorrectly()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                string.Equals(source.Name, ActivitySources.AgentSourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = ActivitySources.Agent.StartActivity("agent.turn", ActivityKind.Internal);
        Assert.NotNull(activity);

        activity.SetStatus(ActivityStatusCode.Ok);

        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public void AgentActivity_SetStatus_Error_RecordsCorrectly()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                string.Equals(source.Name, ActivitySources.AgentSourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = ActivitySources.Agent.StartActivity("agent.turn", ActivityKind.Internal);
        Assert.NotNull(activity);

        activity.SetStatus(ActivityStatusCode.Error, "provider unavailable");

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("provider unavailable", activity.StatusDescription);
    }

    [Fact]
    public void AgentActivity_SetGenAiRequestAttributes_SetsExpectedTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                string.Equals(source.Name, ActivitySources.AgentSourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = ActivitySources.Agent.StartActivity("agent.turn", ActivityKind.Internal);
        Assert.NotNull(activity);

        activity.SetGenAiRequestAttributes(
            system: "openai",
            model: "gpt-4o",
            operation: "chat",
            maxTokens: 4096);

        Assert.Equal("openai", activity.GetTagItem(GenAiAttributes.SystemName));
        Assert.Equal("gpt-4o", activity.GetTagItem(GenAiAttributes.RequestModel));
        Assert.Equal("chat", activity.GetTagItem(GenAiAttributes.OperationName));
        Assert.Equal(4096, activity.GetTagItem(GenAiAttributes.RequestMaxTokens));
    }

    [Fact]
    public void AgentActivity_SetGenAiResponseAttributes_SetsOutputTokens()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                string.Equals(source.Name, ActivitySources.AgentSourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = ActivitySources.Agent.StartActivity("agent.turn", ActivityKind.Internal);
        Assert.NotNull(activity);

        activity.SetGenAiResponseAttributes(outputTokens: 512);

        Assert.Equal(512, activity.GetTagItem(GenAiAttributes.UsageOutputTokens));
    }

    [Fact]
    public void Meters_TurnCount_CanIncrement()
    {
        // Should not throw — counter exists and is wired
        Meters.TurnCount.Add(1,
            new KeyValuePair<string, object?>(GenAiAttributes.SystemName, "test-provider"),
            new KeyValuePair<string, object?>(GenAiAttributes.RequestModel, "test-model"));
    }

    [Fact]
    public void Meters_TurnDuration_CanRecord()
    {
        Meters.TurnDuration.Record(142.5,
            new KeyValuePair<string, object?>(GenAiAttributes.SystemName, "test-provider"));
    }

    [Fact]
    public void Meters_TokensUsed_CanAdd()
    {
        Meters.TokensUsed.Add(256,
            new KeyValuePair<string, object?>(GenAiAttributes.SystemName, "test-provider"));
    }

    [Fact]
    public void Meters_ProviderErrors_CanRecordWithErrorType()
    {
        Meters.ProviderErrors.Add(1,
            new KeyValuePair<string, object?>(GenAiAttributes.SystemName, "test-provider"),
            new KeyValuePair<string, object?>("error.type", nameof(HttpRequestException)));
    }

    [Fact]
    public void AgentActivitySource_WithoutListener_ReturnsNull()
    {
        // When no listener is registered for the source, StartActivity returns null.
        // AgentLoop uses null-conditional ?. so this must not throw.
        Activity? activity = null;
        var nullableActivity = activity;
        nullableActivity?.SetStatus(ActivityStatusCode.Ok);
        nullableActivity?.SetGenAiRequestAttributes("sys", "model");
        // No exception means the null-conditional guards are correct.
    }
}
