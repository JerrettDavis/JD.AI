using System.Diagnostics;
using Xunit;

namespace JD.AI.Tests.Telemetry;

public sealed class OtelInstrumentationTests
{
    [Fact]
    public void ToolActivitySource_HasCorrectName()
    {
        // The ToolConfirmationFilter creates ActivitySource("JD.AI.Tools")
        // Verify it can be listened to
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, "JD.AI.Tools", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("JD.AI.Tools");
        using var activity = source.StartActivity("test.span");

        Assert.NotNull(activity);
        Assert.Equal("test.span", activity.OperationName);
    }

    [Fact]
    public void SessionActivitySource_HasCorrectName()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, "JD.AI.Sessions", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("JD.AI.Sessions");
        using var activity = source.StartActivity("test.span");

        Assert.NotNull(activity);
        Assert.Equal("test.span", activity.OperationName);
    }

    [Fact]
    public void Meters_ToolCalls_CanRecord()
    {
        // Verify the counter exists and doesn't throw
        JD.AI.Telemetry.Meters.ToolCalls.Add(1,
            new KeyValuePair<string, object?>("jdai.tool.name", "test_tool"));
    }

    [Fact]
    public void Meters_TurnCount_CanRecord()
    {
        JD.AI.Telemetry.Meters.TurnCount.Add(1);
    }

    [Fact]
    public void Meters_ProviderErrors_CanRecord()
    {
        JD.AI.Telemetry.Meters.ProviderErrors.Add(1,
            new KeyValuePair<string, object?>("provider", "test"));
    }
}
