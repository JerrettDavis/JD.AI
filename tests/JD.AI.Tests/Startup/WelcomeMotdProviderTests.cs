using FluentAssertions;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class WelcomeMotdProviderTests
{
    [Fact]
    public void NormalizeMotd_Null_ReturnsNull() =>
        WelcomeMotdProvider.NormalizeMotd(null, 100).Should().BeNull();

    [Fact]
    public void NormalizeMotd_ReturnsNull_WhenEmpty() =>
        WelcomeMotdProvider.NormalizeMotd("   ", 80).Should().BeNull();

    [Fact]
    public void NormalizeMotd_UsesFirstNonEmptyLine() =>
        WelcomeMotdProvider.NormalizeMotd(
            "\n\nHello operators\nSecond line", 80)
            .Should().Be("Hello operators");

    [Fact]
    public void NormalizeMotd_Truncates_WhenLongerThanMax()
    {
        var raw = "This is a very long message that should be truncated for the welcome panel.";
        var result = WelcomeMotdProvider.NormalizeMotd(raw, 32);

        result.Should().NotBeNull();
        result!.Length.Should().BeLessThanOrEqualTo(32);
        result.Should().EndWith("...");
    }

    [Fact]
    public void NormalizeMotd_TabsReplacedWithSpaces() =>
        WelcomeMotdProvider.NormalizeMotd("hello\tworld", 100)
            .Should().Be("hello world");

    [Fact]
    public void NormalizeMotd_ExactlyMaxLength_NoTruncation() =>
        WelcomeMotdProvider.NormalizeMotd("abcde", 5)
            .Should().Be("abcde");

    [Fact]
    public void NormalizeMotd_WindowsLineEndings_Handled() =>
        WelcomeMotdProvider.NormalizeMotd("line1\r\nline2\r\n", 100)
            .Should().Be("line1");

    [Fact]
    public void NormalizeMotd_OnlyWhitespaceLines_ReturnsNull() =>
        WelcomeMotdProvider.NormalizeMotd("\n  \n\t\n  ", 100)
            .Should().BeNull();
}
