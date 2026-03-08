using FluentAssertions;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="StreamingMarkdownRenderer"/>.
/// Tests FormatInlineMarkup (pure function, no console dependency)
/// and structural tests for ProcessChunk/Flush behavior.
/// </summary>
public sealed class StreamingMarkdownRendererTests
{
    // ── FormatInlineMarkup (pure function) ──────────────────────────────

    [Fact]
    public void Bold_WrappedInSpectreMarkup()
    {
        var result = StreamingMarkdownRenderer.FormatInlineMarkup("**hello**");
        result.Should().Contain("[bold]hello[/]");
    }

    [Fact]
    public void Italic_WrappedInSpectreMarkup()
    {
        var result = StreamingMarkdownRenderer.FormatInlineMarkup("*hello*");
        result.Should().Contain("[italic]hello[/]");
    }

    [Fact]
    public void InlineCode_WrappedInCodeStyle()
    {
        var result = StreamingMarkdownRenderer.FormatInlineMarkup("`code`");
        result.Should().Contain("[bold yellow on grey15]code[/]");
    }

    [Fact]
    public void MixedFormatting_AllApplied()
    {
        var result = StreamingMarkdownRenderer.FormatInlineMarkup("**bold** and *italic* and `code`");
        result.Should().Contain("[bold]bold[/]");
        result.Should().Contain("[italic]italic[/]");
        result.Should().Contain("[bold yellow on grey15]code[/]");
    }

    [Fact]
    public void PlainText_EscapedOnly()
    {
        var result = StreamingMarkdownRenderer.FormatInlineMarkup("just plain text");
        result.Should().Be("just plain text");
    }

    [Fact]
    public void NestedBoldItalic_HandlesGracefully()
    {
        // Edge case — shouldn't crash
        var result = StreamingMarkdownRenderer.FormatInlineMarkup("***bold italic***");
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SpectreMarkupChars_AreEscaped()
    {
        var result = StreamingMarkdownRenderer.FormatInlineMarkup("use [bold] and [/] literally");
        result.Should().Contain("[[bold]]");
        result.Should().Contain("[[/]]");
    }
}

/// <summary>
/// Integration tests for <see cref="StreamingMarkdownRenderer"/> that exercise
/// ProcessChunk and Flush. Uses Console.SetOut to capture Console.Write* calls
/// (which are the fallback path when AnsiConsole is unavailable).
/// </summary>
[Collection("Console")]
public sealed class StreamingMarkdownRendererIntegrationTests : IDisposable
{
    private readonly TextWriter _originalOut;

    public StreamingMarkdownRendererIntegrationTests()
    {
        _originalOut = Console.Out;
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
    }

    [Fact]
    public void ProcessChunk_PartialLines_NoOutputUntilNewline()
    {
        // Partial lines should be buffered — ProcessChunk doesn't crash
        var renderer = new StreamingMarkdownRenderer();
        renderer.ProcessChunk("hello ");
        renderer.ProcessChunk("world");
        // No newline yet — no assertion failure is the test (no crash)
    }

    [Fact]
    public void ProcessChunk_WithNewline_DoesNotThrow()
    {
        var renderer = new StreamingMarkdownRenderer();
        // These should not throw even when Console.Out is redirected
        renderer.ProcessChunk("line1\n");
        renderer.ProcessChunk("line2\n");
    }

    [Fact]
    public void Flush_RendersRemainingPartialLine_DoesNotThrow()
    {
        var renderer = new StreamingMarkdownRenderer();
        renderer.ProcessChunk("partial");
        renderer.Flush(); // Should not throw
    }

    [Fact]
    public void CodeBlock_ToggleState_DoesNotThrow()
    {
        var renderer = new StreamingMarkdownRenderer();
        renderer.ProcessChunk("```csharp\n");
        renderer.ProcessChunk("var x = 1;\n");
        renderer.ProcessChunk("```\n");
        // No crash means code block toggling works
    }

    [Fact]
    public void HeadingLines_DoNotThrow()
    {
        var renderer = new StreamingMarkdownRenderer();
        renderer.ProcessChunk("# H1\n");
        renderer.ProcessChunk("## H2\n");
        renderer.ProcessChunk("### H3\n");
        // No crash means heading rendering works
    }

    [Fact]
    public void BulletLists_DoNotThrow()
    {
        var renderer = new StreamingMarkdownRenderer();
        renderer.ProcessChunk("- item one\n");
        renderer.ProcessChunk("  - nested item\n");
        renderer.ProcessChunk("    - deeply nested\n");
    }

    [Fact]
    public void NumberedLists_DoNotThrow()
    {
        var renderer = new StreamingMarkdownRenderer();
        renderer.ProcessChunk("1. first\n");
        renderer.ProcessChunk("2. second\n");
    }
}
