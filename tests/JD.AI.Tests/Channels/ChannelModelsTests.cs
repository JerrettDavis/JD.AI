using FluentAssertions;
using JD.AI.Core.Channels;

namespace JD.AI.Tests.Channels;

public sealed class ChannelModelsTests
{
    // ── ChannelMessage ──────────────────────────────────────────────────

    [Fact]
    public void ChannelMessage_RequiredProperties()
    {
        var msg = new ChannelMessage
        {
            Id = "msg-1",
            ChannelId = "ch-1",
            SenderId = "user-1",
            Content = "Hello world",
        };
        msg.Id.Should().Be("msg-1");
        msg.ChannelId.Should().Be("ch-1");
        msg.SenderId.Should().Be("user-1");
        msg.Content.Should().Be("Hello world");
    }

    [Fact]
    public void ChannelMessage_OptionalDefaults()
    {
        var msg = new ChannelMessage
        {
            Id = "m",
            ChannelId = "c",
            SenderId = "s",
            Content = "text",
        };
        msg.SenderDisplayName.Should().BeNull();
        msg.ThreadId.Should().BeNull();
        msg.ReplyToMessageId.Should().BeNull();
        msg.Attachments.Should().BeEmpty();
        msg.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ChannelMessage_AllOptionalProperties()
    {
        var msg = new ChannelMessage
        {
            Id = "m",
            ChannelId = "c",
            SenderId = "s",
            Content = "text",
            SenderDisplayName = "Alice",
            ThreadId = "thread-1",
            ReplyToMessageId = "msg-0",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "val" },
        };
        msg.SenderDisplayName.Should().Be("Alice");
        msg.ThreadId.Should().Be("thread-1");
        msg.ReplyToMessageId.Should().Be("msg-0");
        msg.Metadata.Should().ContainKey("key");
    }

    // ── ChannelAttachment ───────────────────────────────────────────────

    [Fact]
    public void ChannelAttachment_Roundtrip()
    {
        var attachment = new ChannelAttachment(
            "image.png",
            "image/png",
            1024,
            _ => Task.FromResult<Stream>(new MemoryStream()));
        attachment.FileName.Should().Be("image.png");
        attachment.ContentType.Should().Be("image/png");
        attachment.SizeBytes.Should().Be(1024);
        attachment.OpenReadAsync.Should().NotBeNull();
    }

    [Fact]
    public async Task ChannelAttachment_OpenReadAsync_ReturnsStream()
    {
        var data = "hello"u8.ToArray();
        var attachment = new ChannelAttachment(
            "test.txt",
            "text/plain",
            data.Length,
            _ => Task.FromResult<Stream>(new MemoryStream(data)));

        using var stream = await attachment.OpenReadAsync(CancellationToken.None);
        stream.Should().NotBeNull();
        stream.Length.Should().Be(data.Length);
    }
}
