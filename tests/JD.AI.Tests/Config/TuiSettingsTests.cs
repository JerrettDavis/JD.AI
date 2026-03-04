using JD.AI.Core.Config;
using JD.AI.Core.PromptCaching;
using Xunit;

namespace JD.AI.Tests.Config;

[Collection("DataDirectories")]
public sealed class TuiSettingsTests : IDisposable
{
    private readonly string _tempDir;

    public TuiSettingsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        DataDirectories.SetRoot(_tempDir);
    }

    public void Dispose()
    {
        DataDirectories.Reset();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var settings = TuiSettings.Load();

        Assert.Equal(SpinnerStyle.Normal, settings.SpinnerStyle);
        Assert.True(settings.PromptCacheEnabled);
        Assert.Equal(PromptCacheTtl.FiveMinutes, settings.PromptCacheTtl);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var settings = new TuiSettings
        {
            SpinnerStyle = SpinnerStyle.Nerdy,
            PromptCacheEnabled = false,
            PromptCacheTtl = PromptCacheTtl.OneHour,
        };
        settings.Save();

        var loaded = TuiSettings.Load();

        Assert.Equal(SpinnerStyle.Nerdy, loaded.SpinnerStyle);
        Assert.False(loaded.PromptCacheEnabled);
        Assert.Equal(PromptCacheTtl.OneHour, loaded.PromptCacheTtl);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "tui-settings.json"), "{{not valid json}}");

        var settings = TuiSettings.Load();

        Assert.Equal(SpinnerStyle.Normal, settings.SpinnerStyle);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "tui-settings.json"), "");

        var settings = TuiSettings.Load();

        Assert.Equal(SpinnerStyle.Normal, settings.SpinnerStyle);
    }

    [Fact]
    public void Save_CreatesJsonFile()
    {
        var settings = new TuiSettings { SpinnerStyle = SpinnerStyle.Rich };
        settings.Save();

        var path = Path.Combine(_tempDir, "tui-settings.json");
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
}
