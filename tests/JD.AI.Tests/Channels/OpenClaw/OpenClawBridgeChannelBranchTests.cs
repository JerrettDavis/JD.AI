using System.Reflection;
using FluentAssertions;
using JD.AI.Channels.OpenClaw;
using JD.AI.Core.Channels;

namespace JD.AI.Tests.Channels.OpenClaw;

public sealed class OpenClawBridgeChannelBranchTests
{
    [Fact]
    public async Task DeleteSessionsByPrefixAsync_WhenPrefixesAreNull_ThrowsArgumentNullException()
    {
        var (channel, _) = OpenClawTestHelpers.MakeChannel();

        Func<Task> act = () => channel.DeleteSessionsByPrefixAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DeleteSessionsByPrefixAsync_WhenAllFiltersAreBlank_ReturnsZero()
    {
        var (channel, _) = OpenClawTestHelpers.MakeChannel();

        var deleted = await channel.DeleteSessionsByPrefixAsync(["   ", "\t"], [" "]);

        deleted.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(IgnoredEvents))]
    public async Task OnEvent_IgnoresNonAssistantOrIncompletePayloads(global::JD.AI.Channels.OpenClaw.OpenClawEvent evt)
    {
        var (channel, _) = OpenClawTestHelpers.MakeChannel();
        var received = false;
        channel.MessageReceived += _ =>
        {
            received = true;
            return Task.CompletedTask;
        };

        InvokeOnEvent(channel, evt);

        await Task.Delay(100);

        received.Should().BeFalse();
    }

    [Fact]
    public async Task OnEvent_EmitsAssistantMessage_AndGeneratesRunIdWhenMissing()
    {
        var (channel, _) = OpenClawTestHelpers.MakeChannel();
        var tcs = new TaskCompletionSource<ChannelMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        ChannelMessage? received = null;

        channel.MessageReceived += message =>
        {
            received = message;
            tcs.TrySetResult(message);
            return Task.CompletedTask;
        };

        InvokeOnEvent(channel, OpenClawTestHelpers.MakeEvent("chat", new
        {
            stream = "assistant",
            sessionKey = "agent:test:main",
            data = new { text = "Hello from OpenClaw" },
        }));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(500));

        completed.Should().Be(tcs.Task);
        received.Should().NotBeNull();
        received!.ChannelId.Should().Be("openclaw-test");
        received.SenderId.Should().Be("openclaw-assistant");
        received.SenderDisplayName.Should().Be("OpenClaw");
        received.Content.Should().Be("Hello from OpenClaw");
        received.Metadata["session_key"].Should().Be("agent:test:main");
        received.Metadata["stream"].Should().Be("assistant");
        Guid.TryParse(received.Id, out _).Should().BeTrue();
    }

    public static IEnumerable<object[]> IgnoredEvents()
    {
        yield return [new global::JD.AI.Channels.OpenClaw.OpenClawEvent { EventName = "agent" }];
        yield return [OpenClawTestHelpers.MakeEvent("chat", new
        {
            stream = "user",
            sessionKey = "agent:test:main",
            data = new { text = "ignored" },
        })];
        yield return [OpenClawTestHelpers.MakeEvent("chat", new
        {
            stream = "assistant",
            sessionKey = "agent:test:main",
            data = new { },
        })];
        yield return [OpenClawTestHelpers.MakeEvent("chat", new
        {
            stream = "assistant",
            sessionKey = "agent:test:main",
            data = new { text = "" },
        })];
    }

    private static void InvokeOnEvent(global::JD.AI.Channels.OpenClaw.OpenClawBridgeChannel channel, global::JD.AI.Channels.OpenClaw.OpenClawEvent evt)
    {
        var method = typeof(global::JD.AI.Channels.OpenClaw.OpenClawBridgeChannel).GetMethod(
            "OnEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method!.Invoke(channel, [evt]);
    }
}
