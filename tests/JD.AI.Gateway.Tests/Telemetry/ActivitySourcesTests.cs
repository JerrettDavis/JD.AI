using System.Diagnostics;
using FluentAssertions;
using JD.AI.Telemetry;

namespace JD.AI.Gateway.Tests.Telemetry;

public sealed class ActivitySourcesTests
{
    [Fact]
    public void Agent_HasCorrectName()
    {
        ActivitySources.Agent.Name.Should().Be(ActivitySources.AgentSourceName);
        ActivitySources.AgentSourceName.Should().Be("JD.AI.Agent");
    }

    [Fact]
    public void Tools_HasCorrectName()
    {
        ActivitySources.Tools.Name.Should().Be(ActivitySources.ToolsSourceName);
        ActivitySources.ToolsSourceName.Should().Be("JD.AI.Tools");
    }

    [Fact]
    public void Providers_HasCorrectName()
    {
        ActivitySources.Providers.Name.Should().Be(ActivitySources.ProvidersSourceName);
        ActivitySources.ProvidersSourceName.Should().Be("JD.AI.Providers");
    }

    [Fact]
    public void Sessions_HasCorrectName()
    {
        ActivitySources.Sessions.Name.Should().Be(ActivitySources.SessionsSourceName);
        ActivitySources.SessionsSourceName.Should().Be("JD.AI.Sessions");
    }

    [Fact]
    public void AllSourceNames_ContainsAllFourSources()
    {
        var names = ActivitySources.AllSourceNames;
        names.Should().HaveCount(4);
        names.Should().Contain(ActivitySources.AgentSourceName);
        names.Should().Contain(ActivitySources.ToolsSourceName);
        names.Should().Contain(ActivitySources.ProvidersSourceName);
        names.Should().Contain(ActivitySources.SessionsSourceName);
    }

    [Fact]
    public void Agent_StartsActivity_WhenListenerAttached()
    {
        Activity? captured = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, ActivitySources.AgentSourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStarted = a => captured = a,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = ActivitySources.Agent.StartActivity("test.span");

        activity.Should().NotBeNull();
        captured.Should().NotBeNull();
        captured!.OperationName.Should().Be("test.span");
    }
}
