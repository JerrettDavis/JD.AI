using JD.AI.Core.Config;
using Xunit;

namespace JD.AI.Tests.Config;

public sealed class SpinnerStyleTests
{
    [Theory]
    [InlineData("none", SpinnerStyle.None)]
    [InlineData("minimal", SpinnerStyle.Minimal)]
    [InlineData("normal", SpinnerStyle.Normal)]
    [InlineData("rich", SpinnerStyle.Rich)]
    [InlineData("nerdy", SpinnerStyle.Nerdy)]
    [InlineData("Normal", SpinnerStyle.Normal)]
    [InlineData("RICH", SpinnerStyle.Rich)]
    public void Parse_CaseInsensitive_ReturnsExpected(string input, SpinnerStyle expected)
    {
        Assert.True(Enum.TryParse<SpinnerStyle>(input, ignoreCase: true, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("fancy")]
    public void Parse_Invalid_ReturnsFalse(string input)
    {
        Assert.False(Enum.TryParse<SpinnerStyle>(input, ignoreCase: true, out _));
    }

    [Fact]
    public void AllValues_HaveFiveMembers()
    {
        Assert.Equal(5, Enum.GetValues<SpinnerStyle>().Length);
    }
}
