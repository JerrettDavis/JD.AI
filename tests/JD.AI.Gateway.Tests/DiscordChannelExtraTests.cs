using System.Reflection;
using JD.AI.Channels.Discord;
using JD.AI.Core.Channels;

namespace JD.AI.Gateway.Tests;

public sealed class DiscordChannelExtraTests
{
    [Fact]
    public async Task SendMessageWithAttachmentsAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var channel = new DiscordChannel("token");
        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.SendMessageWithAttachmentsAsync("123", "hello", [new OutboundAttachment("test.bin", stream)]));
    }

    [Fact]
    public async Task ReactAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var channel = new DiscordChannel("token");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.ReactAsync("123", "456", "✅"));
    }

    [Fact]
    public async Task ConfirmToolCall_WhenNoPrivilegedUsersConfigured_ReturnsTrue()
    {
        var channel = new DiscordChannel("token");
        var method = typeof(DiscordChannel).GetMethod("ConfirmToolCallAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<bool>?)method!.Invoke(channel, ["read", "{\"path\":\"foo.txt\"}"]);

        Assert.NotNull(task);
        Assert.True(await task!);
    }
}
