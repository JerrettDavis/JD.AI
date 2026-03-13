using FluentAssertions;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Config;

public sealed class FooterSettingsTests
{
    private const string DefaultTemplate =
        "{folder} │ {branch?} │ {pr?} │ {context} │ {provider} │ {model} │ {turns}";

    [Fact]
    public void Default_HasExpectedValues()
    {
        var settings = new FooterSettings();
        settings.Enabled.Should().BeTrue();
        settings.Lines.Should().Be(1);
        settings.Template.Should().Be(DefaultTemplate);
        settings.WarnThresholdPercent.Should().Be(15);
        settings.Segments.Should().NotBeNull();
        settings.Segments.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_Null_ReturnsDefaults()
    {
        var result = FooterSettings.Normalize(null);
        result.Should().NotBeNull();
        result.Enabled.Should().BeTrue();
        result.Lines.Should().Be(1);
        result.Template.Should().Be(DefaultTemplate);
        result.WarnThresholdPercent.Should().Be(15);
        result.Segments.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_WarnThresholdPercentBelowMin_ClampsTo1()
    {
        var settings = new FooterSettings { WarnThresholdPercent = 0 };
        var result = FooterSettings.Normalize(settings);
        result.WarnThresholdPercent.Should().Be(1);
    }

    [Fact]
    public void Normalize_WarnThresholdPercentAboveMax_ClampsTo50()
    {
        var settings = new FooterSettings { WarnThresholdPercent = 99 };
        var result = FooterSettings.Normalize(settings);
        result.WarnThresholdPercent.Should().Be(50);
    }

    [Fact]
    public void Normalize_WarnThresholdPercentInRange_Unchanged()
    {
        var settings = new FooterSettings { WarnThresholdPercent = 25 };
        var result = FooterSettings.Normalize(settings);
        result.WarnThresholdPercent.Should().Be(25);
    }

    [Fact]
    public void Normalize_LinesBelowMin_ClampsTo1()
    {
        var settings = new FooterSettings { Lines = 0 };
        var result = FooterSettings.Normalize(settings);
        result.Lines.Should().Be(1);
    }

    [Fact]
    public void Normalize_LinesAboveMax_ClampsTo3()
    {
        var settings = new FooterSettings { Lines = 10 };
        var result = FooterSettings.Normalize(settings);
        result.Lines.Should().Be(3);
    }

    [Fact]
    public void Normalize_LinesInRange_Unchanged()
    {
        var settings = new FooterSettings { Lines = 2 };
        var result = FooterSettings.Normalize(settings);
        result.Lines.Should().Be(2);
    }

    [Fact]
    public void Normalize_EmptyTemplate_FallsBackToDefault()
    {
        var settings = new FooterSettings { Template = "" };
        var result = FooterSettings.Normalize(settings);
        result.Template.Should().Be(DefaultTemplate);
    }

    [Fact]
    public void Normalize_WhitespaceTemplate_FallsBackToDefault()
    {
        var settings = new FooterSettings { Template = "   " };
        var result = FooterSettings.Normalize(settings);
        result.Template.Should().Be(DefaultTemplate);
    }

    [Fact]
    public void Normalize_ValidTemplate_Unchanged()
    {
        var settings = new FooterSettings { Template = "{model} │ {turns}" };
        var result = FooterSettings.Normalize(settings);
        result.Template.Should().Be("{model} │ {turns}");
    }

    [Fact]
    public void SegmentVisibilityOverride_Default_HasExpectedValues()
    {
        var segment = new SegmentVisibilityOverride();
        segment.Visible.Should().Be("auto");
        segment.WarnPercent.Should().BeNull();
    }

    [Fact]
    public void Normalize_NullSegments_ReturnsEmptyDictionary()
    {
        var settings = new FooterSettings { Segments = null! };
        var result = FooterSettings.Normalize(settings);
        result.Segments.Should().NotBeNull();
        result.Segments.Should().BeEmpty();
    }
}
