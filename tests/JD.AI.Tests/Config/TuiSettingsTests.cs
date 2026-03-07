using JD.AI.Core.Config;
using JD.AI.Core.PromptCaching;
using JD.AI.Tests.Fixtures;
using Xunit;

namespace JD.AI.Tests.Config;

[Collection("DataDirectories")]
public sealed class TuiSettingsTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public TuiSettingsTests()
    {
        DataDirectories.SetRoot(_fixture.DirectoryPath);
    }

    public void Dispose()
    {
        DataDirectories.Reset();
        _fixture.Dispose();
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var settings = TuiSettings.Load();

        Assert.Equal(SpinnerStyle.Normal, settings.SpinnerStyle);
        Assert.Equal(OutputStyle.Rich, settings.OutputStyle);
        Assert.True(settings.PromptCacheEnabled);
        Assert.Equal(PromptCacheTtl.FiveMinutes, settings.PromptCacheTtl);
        Assert.NotNull(settings.Welcome);
        Assert.True(settings.Welcome.ShowWorkingDirectory);
        Assert.True(settings.Welcome.ShowVersion);
        Assert.False(settings.Welcome.ShowMotd);
        Assert.True(settings.AutoCompact);
        Assert.Equal(75, settings.CompactThresholdPercent);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var settings = new TuiSettings
        {
            SpinnerStyle = SpinnerStyle.Nerdy,
            PromptCacheEnabled = false,
            PromptCacheTtl = PromptCacheTtl.OneHour,
            Welcome = new WelcomePanelSettings
            {
                ShowMotd = true,
                MotdUrl = "https://example.com/motd.txt",
            },
        };
        settings.Save();

        var loaded = TuiSettings.Load();

        Assert.Equal(SpinnerStyle.Nerdy, loaded.SpinnerStyle);
        Assert.False(loaded.PromptCacheEnabled);
        Assert.Equal(PromptCacheTtl.OneHour, loaded.PromptCacheTtl);
        Assert.True(loaded.Welcome.ShowMotd);
        Assert.Equal("https://example.com/motd.txt", loaded.Welcome.MotdUrl);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_fixture.DirectoryPath, "tui-settings.json"), "{{not valid json}}");

        var settings = TuiSettings.Load();

        Assert.Equal(SpinnerStyle.Normal, settings.SpinnerStyle);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_fixture.DirectoryPath, "tui-settings.json"), "");

        var settings = TuiSettings.Load();

        Assert.Equal(SpinnerStyle.Normal, settings.SpinnerStyle);
    }

    [Fact]
    public void Save_CreatesJsonFile()
    {
        var settings = new TuiSettings { SpinnerStyle = SpinnerStyle.Rich };
        settings.Save();

        var path = Path.Combine(_fixture.DirectoryPath, "tui-settings.json");
        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        Assert.Contains("rich", json);
    }

    [Theory]
    [InlineData(SpinnerStyle.None)]
    [InlineData(SpinnerStyle.Minimal)]
    [InlineData(SpinnerStyle.Normal)]
    [InlineData(SpinnerStyle.Rich)]
    [InlineData(SpinnerStyle.Nerdy)]
    public void SaveAndLoad_AllStyles(SpinnerStyle style)
    {
        var settings = new TuiSettings { SpinnerStyle = style };
        settings.Save();

        var loaded = TuiSettings.Load();

        Assert.Equal(style, loaded.SpinnerStyle);
    }

    [Fact]
    public void SaveAndLoad_OutputStyleJson_NormalizesToRich()
    {
        var settings = new TuiSettings { OutputStyle = OutputStyle.Json };
        settings.Save();

        var loaded = TuiSettings.Load();

        Assert.Equal(OutputStyle.Rich, loaded.OutputStyle);
    }

    [Fact]
    public void AutoCompact_Default_IsTrue()
    {
        var settings = new TuiSettings();
        Assert.True(settings.AutoCompact);
    }

    [Fact]
    public void CompactThresholdPercent_Default_Is75()
    {
        var settings = new TuiSettings();
        Assert.Equal(75, settings.CompactThresholdPercent);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(75, 75)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(200, 100)]
    public void CompactThresholdPercent_Normalize_Clamps(int input, int expected)
    {
        var settings = new TuiSettings { CompactThresholdPercent = input };
        settings.Save();
        var loaded = TuiSettings.Load();
        Assert.Equal(expected, loaded.CompactThresholdPercent);
    }

    [Fact]
    public void SaveAndLoad_CompactSettings_RoundTrips()
    {
        var settings = new TuiSettings { AutoCompact = false, CompactThresholdPercent = 60 };
        settings.Save();
        var loaded = TuiSettings.Load();
        Assert.False(loaded.AutoCompact);
        Assert.Equal(60, loaded.CompactThresholdPercent);
    }
}
