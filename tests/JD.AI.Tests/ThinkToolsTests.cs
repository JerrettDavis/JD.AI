using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class ThinkToolsTests
{
    [Fact]
    public void Think_ReturnsThought()
    {
        var result = ThinkTools.Think("I should check the file first");
        Assert.Contains("Thought recorded", result, StringComparison.Ordinal);
        Assert.Contains("I should check the file first", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Think_EmptyThought_StillReturns()
    {
        var result = ThinkTools.Think("");
        Assert.Contains("Thought recorded", result, StringComparison.Ordinal);
    }
}
