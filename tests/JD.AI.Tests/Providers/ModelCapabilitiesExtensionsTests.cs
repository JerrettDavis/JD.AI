using FluentAssertions;
using JD.AI.Core.Providers;

namespace JD.AI.Tests.Providers;

public sealed class ModelCapabilitiesExtensionsTests
{
    [Fact]
    public void ToBadge_None_ReturnsDimQuestionMark() =>
        ModelCapabilities.None.ToBadge().Should().Contain("?");

    [Fact]
    public void ToBadge_ChatOnly_ReturnsChatEmoji() =>
        ModelCapabilities.Chat.ToBadge().Should().Contain("\U0001F4AC");

    [Fact]
    public void ToBadge_ChatAndTools_ContainsBothEmojis()
    {
        var badge = (ModelCapabilities.Chat | ModelCapabilities.ToolCalling).ToBadge();
        badge.Should().Contain("\U0001F4AC");
        badge.Should().Contain("\U0001F527");
    }

    [Fact]
    public void ToBadge_AllFlags_ContainsAllEmojis()
    {
        var all = ModelCapabilities.Chat
                  | ModelCapabilities.ToolCalling
                  | ModelCapabilities.Vision
                  | ModelCapabilities.Embeddings;
        var badge = all.ToBadge();
        badge.Should().Contain("\U0001F4AC");
        badge.Should().Contain("\U0001F527");
        badge.Should().Contain("\U0001F441");
        badge.Should().Contain("\U0001F4D0");
    }

    [Fact]
    public void ToLabel_None_ReturnsUnknown() =>
        ModelCapabilities.None.ToLabel().Should().Be("Unknown");

    [Fact]
    public void ToLabel_ChatOnly_ReturnsChat() =>
        ModelCapabilities.Chat.ToLabel().Should().Be("Chat");

    [Fact]
    public void ToLabel_ChatAndTools_ReturnsChatTools() =>
        (ModelCapabilities.Chat | ModelCapabilities.ToolCalling)
            .ToLabel().Should().Be("Chat, Tools");

    [Fact]
    public void ToLabel_AllFlags_ReturnsAll()
    {
        var all = ModelCapabilities.Chat
                  | ModelCapabilities.ToolCalling
                  | ModelCapabilities.Vision
                  | ModelCapabilities.Embeddings;
        all.ToLabel().Should().Be("Chat, Tools, Vision, Embeddings");
    }
}
