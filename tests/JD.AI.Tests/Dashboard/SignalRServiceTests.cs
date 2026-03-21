using System.Reflection;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;

namespace JD.AI.Tests.Dashboard;

public sealed class SignalRServiceTests
{
    [Fact]
    public void Constructor_TrimsTrailingSlash()
    {
        var service = new SignalRService("http://localhost:15790/");

        var baseUrl = GetPrivateField<string>(service, "_baseUrl");

        Assert.Equal("http://localhost:15790", baseUrl);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task StreamChatAsync_WhenAgentHubIsNotConnected_YieldsNoChunks()
    {
        var service = new SignalRService("http://localhost:15790");

        var chunks = new List<AgentStreamChunk>();
        await foreach (var chunk in service.StreamChatAsync("a1", "hello"))
        {
            chunks.Add(chunk);
        }

        Assert.Empty(chunks);
    }

    [Fact]
    public async Task DisposeAsync_WhenNeverConnected_DoesNotThrow()
    {
        var service = new SignalRService("http://localhost:15790");

        await service.DisposeAsync();
    }

    [Fact]
    public void EventArgs_WrapExpectedPayload()
    {
        var activity = new ActivityEvent { EventType = "channel.connected", SourceId = "discord" };

        var activityArgs = new ActivityEventArgs(activity);
        var channelArgs = new ChannelStatusEventArgs("discord", connected: true);
        var agentArgs = new AgentMessageEventArgs("jdai-default", "hello");

        Assert.Equal(activity, activityArgs.Activity);
        Assert.Equal("discord", channelArgs.Channel);
        Assert.True(channelArgs.Connected);
        Assert.Equal("jdai-default", agentArgs.AgentId);
        Assert.Equal("hello", agentArgs.Message);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(instance)!;
    }
}
