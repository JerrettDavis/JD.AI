namespace JD.AI.Daemon.Tests.Config;

public sealed class UpdateConfigTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var config = new UpdateConfig();

        Assert.Equal(TimeSpan.FromHours(24), config.CheckInterval);
        Assert.False(config.AutoApply);
        Assert.True(config.NotifyChannels);
        Assert.False(config.PreRelease);
        Assert.Equal(TimeSpan.FromSeconds(30), config.DrainTimeout);
        Assert.Equal("JD.AI.Daemon", config.PackageId);
        Assert.Equal("https://api.nuget.org/v3-flatcontainer/", config.NuGetFeedUrl);
    }

    [Fact]
    public void Binding_PreservesUnsetDefaults()
    {
        var config = new UpdateConfig();

        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Updates:CheckInterval"] = "00:15:00",
                ["Updates:AutoApply"] = "true",
                ["Updates:PackageId"] = "Custom.Package",
            })
            .Build()
            .GetSection("Updates")
            .Bind(config);

        Assert.Equal(TimeSpan.FromMinutes(15), config.CheckInterval);
        Assert.True(config.AutoApply);
        Assert.True(config.NotifyChannels);
        Assert.False(config.PreRelease);
        Assert.Equal(TimeSpan.FromSeconds(30), config.DrainTimeout);
        Assert.Equal("Custom.Package", config.PackageId);
        Assert.Equal("https://api.nuget.org/v3-flatcontainer/", config.NuGetFeedUrl);
    }
}
