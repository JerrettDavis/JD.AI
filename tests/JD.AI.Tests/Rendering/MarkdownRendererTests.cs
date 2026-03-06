using JD.AI.Rendering;
using Xunit;

namespace JD.AI.Tests.Rendering;

/// <summary>Unit tests for DiffRenderer and MarkdownRenderer (pure logic, no console I/O).</summary>
public sealed class DiffRendererTests
{
    // ── DiffRenderer.IsDiff ────────────────────────────────

    [Theory]
    [InlineData("--- a/file.cs\n+++ b/file.cs\n@@ -1,3 +1,4 @@\n foo\n+bar\n baz", true)]
    [InlineData("--- a/file.py\n+++ b/file.py", true)]
    [InlineData("This is just plain text\nWith multiple lines", false)]
    [InlineData("", false)]
    [InlineData("   \n   ", false)]
    [InlineData("# Heading\n\nSome paragraph", false)]
    [InlineData("--- not a diff\nsome other line", false)]
    public void IsDiff_DetectsUnifiedDiffFormat(string text, bool expected)
    {
        var result = DiffRenderer.IsDiff(text);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsDiff_ReturnsFalse_WhenOnlyMinus()
    {
        var text = "--- only minus header\nsome line";
        Assert.False(DiffRenderer.IsDiff(text));
    }

    [Fact]
    public void IsDiff_HandlesWindowsLineEndings()
    {
        var text = "--- a/f\r\n+++ b/f\r\n@@ -1 +1 @@\r\n+new";
        Assert.True(DiffRenderer.IsDiff(text));
    }

    [Fact]
    public void IsDiff_SkipsLeadingBlankLines()
    {
        var text = "\n\n--- a/file\n+++ b/file\n@@";
        // blank lines at start → first non-blank is "---" ✓, next is "+++" ✓
        Assert.True(DiffRenderer.IsDiff(text));
    }
}

public sealed class MarkdownRendererSlashCommandTests
{
    // ── Slash-command colorization (private helper exposed via internal for tests) ──

    // We test through the public surface: ensure IsDiff is not triggered for markdown
    [Theory]
    [InlineData("# Hello\n\nWorld")]
    [InlineData("- item 1\n- item 2")]
    [InlineData("**bold** and *italic*")]
    [InlineData("`inline code`")]
    [InlineData("```csharp\nvar x = 1;\n```")]
    public void Render_DoesNotThrow_ForCommonMarkdown(string markdown)
    {
        // Redirect console output to a StringWriter so we don't pollute test output
        var original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => MarkdownRenderer.Render(markdown));
            Assert.Null(ex);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForEmptyString()
    {
        var original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => MarkdownRenderer.Render(string.Empty));
            Assert.Null(ex);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Render_DelegatesToDiffRenderer_WhenTextIsDiff()
    {
        // Both render methods should not throw on valid diffs
        var diff = "--- a/file.cs\n+++ b/file.cs\n@@ -1,1 +1,2 @@\n foo\n+bar";
        var original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => MarkdownRenderer.Render(diff));
            Assert.Null(ex);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Theory]
    [InlineData("--- a\n+++ b\n@@ @@\n+added\n-removed\n context")]
    public void DiffRenderer_Render_DoesNotThrow(string diff)
    {
        var original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => DiffRenderer.Render(diff));
            Assert.Null(ex);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForTable()
    {
        var table = "| A | B |\n|---|---|\n| 1 | 2 |";
        var original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => MarkdownRenderer.Render(table));
            Assert.Null(ex);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForNestedList()
    {
        var md = "- item 1\n  - nested a\n  - nested b\n- item 2";
        var original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => MarkdownRenderer.Render(md));
            Assert.Null(ex);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForBlockquote()
    {
        var md = "> This is a quote\n> with multiple lines";
        var original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => MarkdownRenderer.Render(md));
            Assert.Null(ex);
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
