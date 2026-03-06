using FluentAssertions;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

public sealed class TurnSpinnerTests
{
    [Theory]
    [InlineData(0, "0.0s")]
    [InlineData(5, "5.0s")]
    [InlineData(30, "30.0s")]
    [InlineData(59.9, "59.9s")]
    public void FormatElapsed_UnderOneMinute_ShowsSeconds(double seconds, string expected) =>
        TurnSpinner.FormatElapsed(TimeSpan.FromSeconds(seconds)).Should().Be(expected);

    [Theory]
    [InlineData(60, "1m 00s")]
    [InlineData(90, "1m 30s")]
    [InlineData(125, "2m 05s")]
    public void FormatElapsed_OverOneMinute_ShowsMinutesAndSeconds(double seconds, string expected) =>
        TurnSpinner.FormatElapsed(TimeSpan.FromSeconds(seconds)).Should().Be(expected);

    [Fact]
    public void Stop_Idempotent()
    {
        using var spinner = new TurnSpinner();
        spinner.Stop();
        var act = () => spinner.Stop();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var spinner = new TurnSpinner();
        spinner.Dispose();
        var act = () => spinner.Dispose();
        act.Should().NotThrow();
    }
}
