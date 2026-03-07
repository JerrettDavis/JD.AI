using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Additional TUI rendering tests to close issue #241 —
/// covers FormatElapsed boundary values and DiffRenderer IsDiff edge cases.
/// </summary>
public sealed class TuiRenderingAdditionalTests
{
    // ── TurnSpinner.FormatElapsed ──────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, "0.0s")]
    [InlineData(0.1, "0.1s")]
    [InlineData(0.9, "0.9s")]
    [InlineData(1.0, "1.0s")]
    [InlineData(59.9, "59.9s")]
    public void FormatElapsed_SubMinute_UsesSecondsFormat(double seconds, string expected)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        Assert.Equal(expected, TurnSpinner.FormatElapsed(ts));
    }

    [Fact]
    public void FormatElapsed_ExactlyOneMinute_UsesMinutesFormat()
    {
        var ts = TimeSpan.FromMinutes(1);
        var result = TurnSpinner.FormatElapsed(ts);
        Assert.Equal("1m 00s", result);
    }

    [Fact]
    public void FormatElapsed_TwoMinutes_UsesMinutesFormat()
    {
        var ts = TimeSpan.FromMinutes(2);
        var result = TurnSpinner.FormatElapsed(ts);
        Assert.Equal("2m 00s", result);
    }

    [Fact]
    public void FormatElapsed_OneMinuteFifteenSeconds()
    {
        var ts = TimeSpan.FromSeconds(75);
        var result = TurnSpinner.FormatElapsed(ts);
        Assert.Equal("1m 15s", result);
    }

    [Fact]
    public void FormatElapsed_ZeroSeconds_ReturnsZero()
    {
        var result = TurnSpinner.FormatElapsed(TimeSpan.Zero);
        Assert.Equal("0.0s", result);
    }

    [Fact]
    public void FormatElapsed_LargeValue_UsesMinutesFormat()
    {
        var ts = TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59);
        var result = TurnSpinner.FormatElapsed(ts);
        Assert.Equal("59m 59s", result);
    }

    // ── DiffRenderer.IsDiff additional edge cases ──────────────────────────────

    [Fact]
    public void IsDiff_NullString_ReturnsFalse()
    {
        Assert.False(DiffRenderer.IsDiff(null!));
    }

    [Fact]
    public void IsDiff_OnlyPlusLines_ReturnsFalse()
    {
        // +++ without preceding --- is not a valid diff
        Assert.False(DiffRenderer.IsDiff("+++ b/file.cs\n@@ -1 +1 @@\n+new"));
    }

    [Fact]
    public void IsDiff_MultipleFileDiffs_ReturnsTrue()
    {
        var multi = "--- a/file1.cs\n+++ b/file1.cs\n@@\n+line\n--- a/file2.cs\n+++ b/file2.cs\n@@\n+line";
        Assert.True(DiffRenderer.IsDiff(multi));
    }

    [Fact]
    public void IsDiff_SpacePrefixedMinusPlus_ReturnsFalse()
    {
        // Leading space means they're not diff markers (must start at column 0)
        var indented = " --- a/file\n +++ b/file";
        Assert.False(DiffRenderer.IsDiff(indented));
    }

    [Theory]
    [InlineData("--- /dev/null\n+++ b/new-file.txt")]
    [InlineData("--- a/src/MyClass.cs\n+++ b/src/MyClass.cs\n@@ -1,1 +1,2 @@")]
    [InlineData("--- a/README.md\n+++ b/README.md")]
    public void IsDiff_ValidUnifiedDiff_ReturnsTrue(string diff)
    {
        Assert.True(DiffRenderer.IsDiff(diff));
    }

    // ── DiffRenderer.Render does not throw ────────────────────────────────────

    [Theory]
    [InlineData("--- a/f\n+++ b/f\n@@ @@\n+added\n-removed\n context line")]
    [InlineData("--- a/f\n+++ b/f")]  // minimal diff
    [InlineData("--- a/f\n+++ b/f\n@@ -0,0 +1 @@\n+single line")]
    public void Render_ValidDiff_DoesNotThrow(string diff)
    {
        var ex = Record.Exception(() => DiffRenderer.Render(diff));
        Assert.Null(ex);
    }

    [Fact]
    public void Render_EmptyString_DoesNotThrow()
    {
        var ex = Record.Exception(() => DiffRenderer.Render(string.Empty));
        Assert.Null(ex);
    }

    [Fact]
    public void Render_DiffWithSpecialMarkupChars_DoesNotThrow()
    {
        // Lines with Spectre markup chars like [ and ] need escaping
        var diff = "--- a/f\n+++ b/f\n@@ @@\n+var x = arr[0];\n-var x = arr[1];";
        var ex = Record.Exception(() => DiffRenderer.Render(diff));
        Assert.Null(ex);
    }
}
