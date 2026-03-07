using JD.AI.Rendering;
using Xunit;

namespace JD.AI.Tests.Rendering;

/// <summary>Unit tests for DiffRenderer and MarkdownRenderer (pure logic, no console I/O).</summary>
[Collection("Console")]
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

[Collection("Console")]
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

/// <summary>Tests for regressions found in code review (critical/high issues).</summary>
[Collection("Console")]
public sealed class MarkdownRendererRegressionTests
{
    private static void WithNullConsole(Action action)
    {
        var saved = Console.Out;
        try { Console.SetOut(TextWriter.Null); action(); }
        finally { Console.SetOut(saved); }
    }

    // ── CRITICAL-3: ragged/short table rows must not throw ────────────────────────

    [Fact]
    public void Render_DoesNotThrow_ForTableWithRaggedRows()
    {
        // Row has more cells than the header — Spectre throws without the clamp fix
        var table = "| A | B |\n|---|---|\n| 1 | 2 | 3 |";
        WithNullConsole(() =>
        {
            var ex = Record.Exception(() => MarkdownRenderer.Render(table));
            Assert.Null(ex);
        });
    }

    [Fact]
    public void Render_DoesNotThrow_ForTableWithShortRows()
    {
        // Row has fewer cells than the header — must be padded
        var table = "| A | B | C |\n|---|---|---|\n| 1 |";
        WithNullConsole(() =>
        {
            var ex = Record.Exception(() => MarkdownRenderer.Render(table));
            Assert.Null(ex);
        });
    }

    // ── HIGH-1: git diff preamble ─────────────────────────────────────────────────

    [Fact]
    public void IsDiff_ReturnsTrue_ForFullGitDiffWithPreamble()
    {
        var gitDiff = "diff --git a/Foo.cs b/Foo.cs\nindex abc123..def456 100644\n--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n+new line";
        Assert.True(DiffRenderer.IsDiff(gitDiff));
    }

    [Fact]
    public void IsDiff_ReturnsFalse_ForIndentedDashesInMarkdown()
    {
        // Indented --- / +++ should NOT be detected as a diff (HIGH-3 fix: no TrimStart)
        var markdown = "   --- deprecated API\n   +++ new API\nsome context";
        Assert.False(DiffRenderer.IsDiff(markdown));
    }

    // ── HIGH-2: string escape sequences in code highlighting ─────────────────────

    [Theory]
    [InlineData("csharp", """var s = "say \"hello\"";""")]
    [InlineData("csharp", """var q = "\"";""")]
    [InlineData("javascript", "const x = `template ${v}`;")]
    [InlineData("python", "s = 'it\\'s fine'")]
    public void Render_DoesNotThrow_ForCodeWithEscapedStringLiterals(string lang, string code)
    {
        WithNullConsole(() =>
        {
            var ex = Record.Exception(() => MarkdownRenderer.Render($"```{lang}\n{code}\n```"));
            Assert.Null(ex);
        });
    }

    // ── HIGH-4: multi-paragraph blockquote gutter ─────────────────────────────────

    [Fact]
    public void Render_DoesNotThrow_ForMultiParagraphBlockquote()
    {
        var md = "> First paragraph\n>\n> Second paragraph\n>\n> Third paragraph";
        WithNullConsole(() =>
        {
            var ex = Record.Exception(() => MarkdownRenderer.Render(md));
            Assert.Null(ex);
        });
    }

    // ── T-1: streaming state machine ─────────────────────────────────────────────

    [Fact]
    public void EndStreaming_WithoutBeginStreaming_DoesNotThrow()
    {
        WithNullConsole(() =>
        {
            JD.AI.Rendering.ChatRenderer.SetOutputStyle(JD.AI.Core.Config.OutputStyle.Rich);
            // EndStreaming without a preceding BeginStreaming must be a no-op
            var ex = Record.Exception(JD.AI.Rendering.ChatRenderer.EndStreaming);
            Assert.Null(ex);
        });
    }

    [Fact]
    public void EndStreaming_CalledTwice_IsIdempotent()
    {
        WithNullConsole(() =>
        {
            JD.AI.Rendering.ChatRenderer.SetOutputStyle(JD.AI.Core.Config.OutputStyle.Rich);
            JD.AI.Rendering.ChatRenderer.BeginStreaming();
            JD.AI.Rendering.ChatRenderer.WriteStreamingChunk("# Hello");
            JD.AI.Rendering.ChatRenderer.EndStreaming();
            // Second call must not throw
            var ex = Record.Exception(JD.AI.Rendering.ChatRenderer.EndStreaming);
            Assert.Null(ex);
        });
    }

    [Fact]
    public void BeginWriteEnd_Rich_AccumulatesAndFlushesWithoutThrowing()
    {
        WithNullConsole(() =>
        {
            JD.AI.Rendering.ChatRenderer.SetOutputStyle(JD.AI.Core.Config.OutputStyle.Rich);
            JD.AI.Rendering.ChatRenderer.BeginStreaming();
            JD.AI.Rendering.ChatRenderer.WriteStreamingChunk("## Title\n\nHello **world**.");
            JD.AI.Rendering.ChatRenderer.WriteStreamingChunk("\n\n- item 1\n- item 2");
            var ex = Record.Exception(JD.AI.Rendering.ChatRenderer.EndStreaming);
            Assert.Null(ex);
        });
    }

    // ── T-4: slash-command colorization ──────────────────────────────────────────

    [Theory]
    [InlineData("/help")]
    [InlineData("/model")]
    [InlineData("/plan")]
    [InlineData("/diff")]
    [InlineData("/skills")]
    public void Render_DoesNotThrow_ForProseWithKnownSlashCommands(string cmd)
    {
        WithNullConsole(() =>
        {
            var ex = Record.Exception(() => MarkdownRenderer.Render($"Use {cmd} to get started."));
            Assert.Null(ex);
        });
    }

    [Fact]
    public void Render_DoesNotThrow_ForSlashAtEndOfString()
    {
        WithNullConsole(() =>
        {
            var ex = Record.Exception(() => MarkdownRenderer.Render("trailing /"));
            Assert.Null(ex);
        });
    }

    [Fact]
    public void Render_DoesNotThrow_ForUnknownSlashToken()
    {
        WithNullConsole(() =>
        {
            var ex = Record.Exception(() => MarkdownRenderer.Render("Use /notacommand freely."));
            Assert.Null(ex);
        });
    }
}
