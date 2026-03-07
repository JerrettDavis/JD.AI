using JD.AI.Startup;
using Xunit;

namespace JD.AI.Tests.Startup;

public sealed class WelcomeMotdProviderTests
{
    [Fact]
    public void NormalizeMotd_ReturnsNull_WhenEmpty()
    {
        var result = WelcomeMotdProvider.NormalizeMotd("   ", 80);
        Assert.Null(result);
    }

    [Fact]
    public void NormalizeMotd_UsesFirstNonEmptyLine()
    {
        var result = WelcomeMotdProvider.NormalizeMotd("\n\nHello operators\nSecond line", 80);
        Assert.Equal("Hello operators", result);
    }

    [Fact]
    public void NormalizeMotd_Truncates_WhenLongerThanMax()
    {
        var raw = "This is a very long message that should be truncated for the welcome panel.";
        var result = WelcomeMotdProvider.NormalizeMotd(raw, 32);

        Assert.NotNull(result);
        Assert.True(result!.Length <= 32);
        Assert.EndsWith("...", result, StringComparison.Ordinal);
    }
}
