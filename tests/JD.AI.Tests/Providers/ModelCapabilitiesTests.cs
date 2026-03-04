using FluentAssertions;
using JD.AI.Core.Providers;

namespace JD.AI.Tests.Providers;

public sealed class ModelCapabilitiesTests
{
    // ── Enum flag combinations ──────────────────────────────────────────

    [Fact]
    public void Flags_ChatAndToolCalling_CombineCorrectly()
    {
        var caps = ModelCapabilities.Chat | ModelCapabilities.ToolCalling;

        caps.HasFlag(ModelCapabilities.Chat).Should().BeTrue();
        caps.HasFlag(ModelCapabilities.ToolCalling).Should().BeTrue();
        caps.HasFlag(ModelCapabilities.Vision).Should().BeFalse();
    }

    [Fact]
    public void Flags_AllCombined_ContainsEveryFlag()
    {
        var all = ModelCapabilities.Chat
                | ModelCapabilities.ToolCalling
                | ModelCapabilities.Vision
                | ModelCapabilities.Embeddings;

        all.HasFlag(ModelCapabilities.Chat).Should().BeTrue();
        all.HasFlag(ModelCapabilities.ToolCalling).Should().BeTrue();
        all.HasFlag(ModelCapabilities.Vision).Should().BeTrue();
        all.HasFlag(ModelCapabilities.Embeddings).Should().BeTrue();
    }

    // ── ToBadge() ───────────────────────────────────────────────────────

    [Fact]
    public void ToBadge_None_ReturnsDimQuestionMark()
    {
        ModelCapabilities.None.ToBadge().Should().Be("[dim]?[/]");
    }

    [Fact]
    public void ToBadge_ChatOnly_ReturnsChatEmoji()
    {
        ModelCapabilities.Chat.ToBadge().Should().Be("💬");
    }

    [Fact]
    public void ToBadge_ChatAndToolCalling_ReturnsChatToolEmoji()
    {
        (ModelCapabilities.Chat | ModelCapabilities.ToolCalling)
            .ToBadge().Should().Be("💬🔧");
    }

    [Fact]
    public void ToBadge_ChatToolCallingVision_ReturnsThreeEmojis()
    {
        (ModelCapabilities.Chat | ModelCapabilities.ToolCalling | ModelCapabilities.Vision)
            .ToBadge().Should().Be("💬🔧👁");
    }

    [Fact]
    public void ToBadge_AllFlags_ReturnsFourEmojis()
    {
        (ModelCapabilities.Chat | ModelCapabilities.ToolCalling
            | ModelCapabilities.Vision | ModelCapabilities.Embeddings)
            .ToBadge().Should().Be("💬🔧👁📐");
    }

    // ── ToLabel() ───────────────────────────────────────────────────────

    [Fact]
    public void ToLabel_None_ReturnsUnknown()
    {
        ModelCapabilities.None.ToLabel().Should().Be("Unknown");
    }

    [Fact]
    public void ToLabel_ChatOnly_ReturnsChat()
    {
        ModelCapabilities.Chat.ToLabel().Should().Be("Chat");
    }

    [Fact]
    public void ToLabel_ChatAndToolCalling_ReturnsChatTools()
    {
        (ModelCapabilities.Chat | ModelCapabilities.ToolCalling)
            .ToLabel().Should().Be("Chat, Tools");
    }

    [Fact]
    public void ToLabel_AllFlags_ReturnsAllLabels()
    {
        (ModelCapabilities.Chat | ModelCapabilities.ToolCalling
            | ModelCapabilities.Vision | ModelCapabilities.Embeddings)
            .ToLabel().Should().Be("Chat, Tools, Vision, Embeddings");
    }

    // ── ModelCapabilityHeuristics.InferFromName() ───────────────────────

    [Theory]
    [InlineData("llama3.1:8b")]
    [InlineData("qwen2.5:7b")]
    [InlineData("phi-3-mini")]
    public void InferFromName_ToolCapableModel_ReturnsChatAndToolCalling(string modelName)
    {
        var caps = ModelCapabilityHeuristics.InferFromName(modelName);

        caps.Should().HaveFlag(ModelCapabilities.Chat);
        caps.Should().HaveFlag(ModelCapabilities.ToolCalling);
    }

    [Fact]
    public void InferFromName_CodeLlama_ReturnsChatOnly()
    {
        var caps = ModelCapabilityHeuristics.InferFromName("codellama:13b");

        caps.Should().HaveFlag(ModelCapabilities.Chat);
        caps.Should().NotHaveFlag(ModelCapabilities.ToolCalling);
        caps.Should().NotHaveFlag(ModelCapabilities.Vision);
    }

    [Fact]
    public void InferFromName_Llava_ReturnsChatAndVision()
    {
        var caps = ModelCapabilityHeuristics.InferFromName("llava:7b");

        caps.Should().HaveFlag(ModelCapabilities.Chat);
        caps.Should().HaveFlag(ModelCapabilities.Vision);
    }

    [Fact]
    public void InferFromName_Llama32Vision_ReturnsChatToolCallingVision()
    {
        var caps = ModelCapabilityHeuristics.InferFromName("llama3.2-vision:11b");

        caps.Should().HaveFlag(ModelCapabilities.Chat);
        caps.Should().HaveFlag(ModelCapabilities.ToolCalling);
        caps.Should().HaveFlag(ModelCapabilities.Vision);
    }

    [Fact]
    public void InferFromName_Gemma3_ReturnsChatToolCallingVision()
    {
        var caps = ModelCapabilityHeuristics.InferFromName("gemma-3:4b");

        caps.Should().HaveFlag(ModelCapabilities.Chat);
        caps.Should().HaveFlag(ModelCapabilities.ToolCalling);
        caps.Should().HaveFlag(ModelCapabilities.Vision);
    }

    [Fact]
    public void InferFromName_UnknownModel_ReturnsChatOnly()
    {
        var caps = ModelCapabilityHeuristics.InferFromName("random-model");

        caps.Should().Be(ModelCapabilities.Chat);
    }
}
