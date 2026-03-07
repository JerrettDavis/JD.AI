using FluentAssertions;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Config;

public sealed class WelcomePanelSettingsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var settings = new WelcomePanelSettings();
        settings.ShowModelSummary.Should().BeTrue();
        settings.ShowServices.Should().BeTrue();
        settings.ShowWorkingDirectory.Should().BeTrue();
        settings.ShowVersion.Should().BeTrue();
        settings.ShowMotd.Should().BeFalse();
        settings.MotdUrl.Should().BeNull();
        settings.MotdTimeoutMs.Should().Be(700);
        settings.MotdMaxLength.Should().Be(160);
    }

    [Fact]
    public void Normalize_Null_ReturnsDefaults()
    {
        var result = WelcomePanelSettings.Normalize(null);
        result.Should().NotBeNull();
        result.MotdTimeoutMs.Should().Be(700);
        result.MotdMaxLength.Should().Be(160);
    }

    [Fact]
    public void Normalize_TimeoutBelowMin_ClampsTo100()
    {
        var settings = new WelcomePanelSettings { MotdTimeoutMs = 10 };
        var result = WelcomePanelSettings.Normalize(settings);
        result.MotdTimeoutMs.Should().Be(100);
    }

    [Fact]
    public void Normalize_TimeoutAboveMax_ClampsTo5000()
    {
        var settings = new WelcomePanelSettings { MotdTimeoutMs = 99_999 };
        var result = WelcomePanelSettings.Normalize(settings);
        result.MotdTimeoutMs.Should().Be(5_000);
    }

    [Fact]
    public void Normalize_MaxLengthBelowMin_ClampsTo40()
    {
        var settings = new WelcomePanelSettings { MotdMaxLength = 5 };
        var result = WelcomePanelSettings.Normalize(settings);
        result.MotdMaxLength.Should().Be(40);
    }

    [Fact]
    public void Normalize_MaxLengthAboveMax_ClampsTo1000()
    {
        var settings = new WelcomePanelSettings { MotdMaxLength = 50_000 };
        var result = WelcomePanelSettings.Normalize(settings);
        result.MotdMaxLength.Should().Be(1_000);
    }

    [Fact]
    public void Normalize_WhitespaceUrl_BecomesNull()
    {
        var settings = new WelcomePanelSettings { MotdUrl = "   " };
        var result = WelcomePanelSettings.Normalize(settings);
        result.MotdUrl.Should().BeNull();
    }

    [Fact]
    public void Normalize_EmptyUrl_BecomesNull()
    {
        var settings = new WelcomePanelSettings { MotdUrl = "" };
        var result = WelcomePanelSettings.Normalize(settings);
        result.MotdUrl.Should().BeNull();
    }

    [Fact]
    public void Normalize_ValidUrl_Trimmed()
    {
        var settings = new WelcomePanelSettings { MotdUrl = "  https://example.com/motd  " };
        var result = WelcomePanelSettings.Normalize(settings);
        result.MotdUrl.Should().Be("https://example.com/motd");
    }

    [Fact]
    public void Normalize_ValidValues_Unchanged()
    {
        var settings = new WelcomePanelSettings
        {
            MotdTimeoutMs = 500,
            MotdMaxLength = 200,
            MotdUrl = "https://example.com",
        };
        var result = WelcomePanelSettings.Normalize(settings);
        result.MotdTimeoutMs.Should().Be(500);
        result.MotdMaxLength.Should().Be(200);
        result.MotdUrl.Should().Be("https://example.com");
    }
}
