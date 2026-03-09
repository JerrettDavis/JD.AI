using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class TurnProgressTests : IDisposable
{
    // ── FormatElapsed ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "0.0s")]
    [InlineData(5, "5.0s")]
    [InlineData(30, "30.0s")]
    [InlineData(59.9, "59.9s")]
    public void FormatElapsed_UnderOneMinute_ShowsSeconds(double seconds, string expected) =>
        TurnProgress.FormatElapsed(TimeSpan.FromSeconds(seconds)).Should().Be(expected);

    [Theory]
    [InlineData(60, "1m 00s")]
    [InlineData(90, "1m 30s")]
    [InlineData(125, "2m 05s")]
    [InlineData(600, "10m 00s")]
    public void FormatElapsed_OverOneMinute_ShowsMinutesAndSeconds(double seconds, string expected) =>
        TurnProgress.FormatElapsed(TimeSpan.FromSeconds(seconds)).Should().Be(expected);

    // ── BuildProgressBar ─────────────────────────────────────────────────

    [Fact]
    public void BuildProgressBar_Returns10Characters()
    {
        var bar = TurnProgress.BuildProgressBar(TimeSpan.FromMilliseconds(500));
        bar.Length.Should().Be(10);
    }

    [Fact]
    public void BuildProgressBar_ContainsExactlyOneHighlightChar()
    {
        var bar = TurnProgress.BuildProgressBar(TimeSpan.FromMilliseconds(0));
        bar.Count(c => c == '━').Should().Be(1);
    }

    [Fact]
    public void BuildProgressBar_DifferentTimesProduceDifferentBars()
    {
        var bar1 = TurnProgress.BuildProgressBar(TimeSpan.FromMilliseconds(0));
        var bar2 = TurnProgress.BuildProgressBar(TimeSpan.FromMilliseconds(300));
        bar1.Should().NotBe(bar2);
    }

    [Fact]
    public void BuildProgressBar_BouncesWithinWidth()
    {
        // Sample many time values and ensure highlight stays in bounds
        for (var i = 0; i < 100; i++)
        {
            var bar = TurnProgress.BuildProgressBar(TimeSpan.FromMilliseconds(i * 150));
            bar.Length.Should().Be(10);
            bar.Count(c => c == '━').Should().Be(1);
        }
    }

    // ── Format methods ───────────────────────────────────────────────────

    [Fact]
    public void FormatMinimal_ContainsElapsed()
    {
        using var progress = new TurnProgress(SpinnerStyle.Minimal);
        var result = progress.FormatMinimal(TimeSpan.FromSeconds(5));
        result.Should().Contain("5.0s");
    }

    [Fact]
    public void FormatNormal_ContainsThinking()
    {
        using var progress = new TurnProgress(SpinnerStyle.Normal);
        var result = progress.FormatNormal(TimeSpan.FromSeconds(10));
        result.Should().Contain("Thinking");
    }

    [Fact]
    public void FormatNormal_WithThinkingPreview_IncludesPreview()
    {
        using var progress = new TurnProgress(SpinnerStyle.Normal);
        progress.SetThinkingPreview("Analyzing auth middleware");
        var result = progress.FormatNormal(TimeSpan.FromSeconds(2));
        result.Should().Contain("Analyzing auth middleware");
    }

    [Fact]
    public void FormatRich_ContainsThinkingAndProgressBar()
    {
        using var progress = new TurnProgress(SpinnerStyle.Rich);
        var result = progress.FormatRich(TimeSpan.FromSeconds(5));
        result.Should().Contain("Thinking");
        result.Should().Contain("5.0s");
    }

    [Fact]
    public void FormatNerdy_ContainsModelName()
    {
        using var progress = new TurnProgress(SpinnerStyle.Nerdy, "gpt-4o");
        var result = progress.FormatNerdy(TimeSpan.FromSeconds(3));
        result.Should().Contain("gpt-4o");
        result.Should().Contain("awaiting first token");
    }

    [Fact]
    public void FormatNerdy_WithoutModel_OmitsModelSection()
    {
        using var progress = new TurnProgress(SpinnerStyle.Nerdy);
        var result = progress.FormatNerdy(TimeSpan.FromSeconds(1));
        result.Should().Contain("Thinking");
        result.Should().NotContain("│ \x1b[33m");
    }

    [Fact]
    public void FormatRich_WithLongPreview_TruncatesPreview()
    {
        using var progress = new TurnProgress(SpinnerStyle.Rich);
        var longText = new string('x', 240);
        progress.SetThinkingPreview(longText);
        var result = progress.FormatRich(TimeSpan.FromSeconds(1));
        result.Should().Contain("...");
    }

    // ── State transitions ────────────────────────────────────────────────

    [Fact]
    public void Stop_SetsTimeToFirstTokenMs()
    {
        using var progress = new TurnProgress(SpinnerStyle.None);
        progress.TimeToFirstTokenMs.Should().Be(-1);
        progress.Stop();
        progress.TimeToFirstTokenMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Stop_IsIdempotent()
    {
        using var progress = new TurnProgress(SpinnerStyle.None);
        progress.Stop();
        var first = progress.TimeToFirstTokenMs;
        progress.Stop();
        progress.TimeToFirstTokenMs.Should().Be(first);
    }

    [Fact]
    public void Dispose_ImpliesStop()
    {
        var progress = new TurnProgress(SpinnerStyle.None);
        progress.Dispose();
        progress.TimeToFirstTokenMs.Should().BeGreaterThanOrEqualTo(0);
    }

    public void Dispose()
    {
        // Test cleanup — no shared resources
    }
}
