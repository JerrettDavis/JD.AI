using FluentAssertions;
using JD.AI.Rendering;
using Xunit;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Comprehensive unit tests for <see cref="DiffRenderer"/>.
/// Covers IsDiff detection and Render output.
/// </summary>
[Collection("Console")]
public sealed class DiffRendererComprehensiveTests
{
    // ── IsDiff: Valid unified diffs ──────────────────────────────────

    [Fact]
    public void IsDiff_ReturnsTrue_ForMinimalUnifiedDiff()
    {
        var text = "--- a/file.cs\n+++ b/file.cs";
        DiffRenderer.IsDiff(text).Should().BeTrue();
    }

    [Fact]
    public void IsDiff_ReturnsTrue_ForFullUnifiedDiffWithHunks()
    {
        var text = "--- a/file.cs\n+++ b/file.cs\n@@ -1,3 +1,4 @@\n foo\n+bar\n baz";
        DiffRenderer.IsDiff(text).Should().BeTrue();
    }

    [Fact]
    public void IsDiff_ReturnsTrue_ForWindowsLineEndings()
    {
        var text = "--- a/file.cs\r\n+++ b/file.cs\r\n@@ -1 +1 @@\r\n+new";
        DiffRenderer.IsDiff(text).Should().BeTrue();
    }

    [Fact]
    public void IsDiff_ReturnsTrue_ForDiffWithBlankLinesBeforeMarkers()
    {
        var text = "\n\n--- a/file.cs\n+++ b/file.cs\n@@";
        DiffRenderer.IsDiff(text).Should().BeTrue();
    }

    // ── IsDiff: Preamble lines ──────────────────────────────────────

    [Fact]
    public void IsDiff_ReturnsTrue_ForFullGitDiffWithPreamble()
    {
        var text = "diff --git a/Foo.cs b/Foo.cs\nindex abc123..def456 100644\n--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n+new line";
        DiffRenderer.IsDiff(text).Should().BeTrue();
    }

    [Fact]
    public void IsDiff_ReturnsTrue_ForDiffWithOnlyDiffGitPreamble()
    {
        var text = "diff --git a/x b/x\n--- a/x\n+++ b/x";
        DiffRenderer.IsDiff(text).Should().BeTrue();
    }

    [Fact]
    public void IsDiff_ReturnsTrue_ForDiffWithMultiplePreambleLines()
    {
        var text = "diff --git a/x b/x\nold mode 100644\nnew mode 100755\nindex abc..def\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-old\n+new";
        DiffRenderer.IsDiff(text).Should().BeTrue();
    }

    // ── IsDiff: Returns false ───────────────────────────────────────

    [Fact]
    public void IsDiff_ReturnsFalse_ForNull()
    {
        DiffRenderer.IsDiff(null!).Should().BeFalse();
    }

    [Fact]
    public void IsDiff_ReturnsFalse_ForEmptyString()
    {
        DiffRenderer.IsDiff(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void IsDiff_ReturnsFalse_ForWhitespaceOnly()
    {
        DiffRenderer.IsDiff("   \n   \n  ").Should().BeFalse();
    }

    [Fact]
    public void IsDiff_ReturnsFalse_ForPlainText()
    {
        DiffRenderer.IsDiff("This is just plain text\nWith multiple lines").Should().BeFalse();
    }

    [Fact]
    public void IsDiff_ReturnsFalse_ForMarkdown()
    {
        DiffRenderer.IsDiff("# Heading\n\nSome paragraph").Should().BeFalse();
    }

    [Fact]
    public void IsDiff_ReturnsFalse_WhenOnlyMinusMarkerExists()
    {
        DiffRenderer.IsDiff("--- only minus header\nsome line").Should().BeFalse();
    }

    [Fact]
    public void IsDiff_ReturnsFalse_WhenPlusPlusDoesNotFollowMinusMinus()
    {
        DiffRenderer.IsDiff("--- a/file.cs\nsome text in between\n+++ b/file.cs").Should().BeFalse();
    }

    [Fact]
    public void IsDiff_ReturnsFalse_ForIndentedDashesInMarkdown()
    {
        // Indented --- / +++ should not be detected as a diff
        var markdown = "   --- deprecated API\n   +++ new API\nsome context";
        DiffRenderer.IsDiff(markdown).Should().BeFalse();
    }

    [Theory]
    [InlineData("--- a/f\n+++ b/f", true)]
    [InlineData("plain text", false)]
    [InlineData("", false)]
    [InlineData("   \n   ", false)]
    [InlineData("--- not a diff\nsome other line", false)]
    public void IsDiff_Theory_DetectsCorrectly(string text, bool expected)
    {
        DiffRenderer.IsDiff(text).Should().Be(expected);
    }

    // ── Render: output verification ─────────────────────────────────

    [Fact]
    public void Render_DoesNotThrow_ForValidDiff()
    {
        var diff = "--- a/file.cs\n+++ b/file.cs\n@@ -1,2 +1,3 @@\n context\n+added\n-removed";

        var saved = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => DiffRenderer.Render(diff));
            ex.Should().BeNull();
        }
        finally
        {
            Console.SetOut(saved);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForDiffWithAllLineTypes()
    {
        var diff = "--- a/file.cs\n+++ b/file.cs\n@@ -1,5 +1,5 @@\n context line\n+added line\n-removed line\n another context";

        var saved = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => DiffRenderer.Render(diff));
            ex.Should().BeNull();
        }
        finally
        {
            Console.SetOut(saved);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForEmptyDiff()
    {
        var saved = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => DiffRenderer.Render(string.Empty));
            ex.Should().BeNull();
        }
        finally
        {
            Console.SetOut(saved);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForDiffContainingMarkupCharacters()
    {
        // Ensure Spectre markup characters like [ and ] are escaped properly
        var diff = "--- a/file.cs\n+++ b/file.cs\n@@ -1 +1 @@\n-var x = arr[0];\n+var x = arr[1];";

        var saved = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => DiffRenderer.Render(diff));
            ex.Should().BeNull();
        }
        finally
        {
            Console.SetOut(saved);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForDiffWithWindowsLineEndings()
    {
        var diff = "--- a/f\r\n+++ b/f\r\n@@ -1 +1 @@\r\n-old\r\n+new\r\n context";

        var saved = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => DiffRenderer.Render(diff));
            ex.Should().BeNull();
        }
        finally
        {
            Console.SetOut(saved);
        }
    }

    [Fact]
    public void Render_DoesNotThrow_ForDiffWithOnlyContextLines()
    {
        var diff = "--- a/f\n+++ b/f\n@@ -1,2 +1,2 @@\n context1\n context2";

        var saved = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var ex = Record.Exception(() => DiffRenderer.Render(diff));
            ex.Should().BeNull();
        }
        finally
        {
            Console.SetOut(saved);
        }
    }
}
