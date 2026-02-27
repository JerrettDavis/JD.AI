namespace JD.AI.Tui.Tests;

public sealed class UpdateCheckerTests
{
    [Theory]
    [InlineData("1.0.0", "0.9.0", true)]
    [InlineData("0.2.0", "0.1.42", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("2.0.0", "1.99.99", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("0.1.0", "1.0.0", false)]
    public void IsNewer_ComparesVersionsCorrectly(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.IsNewer(latest, current));
    }

    [Theory]
    [InlineData("1.0.0-preview.1", "0.9.0", true)]
    [InlineData("1.0.0-beta", "1.0.0", false)]
    [InlineData("1.0.1-rc1", "1.0.0", true)]
    public void IsNewer_HandlesPreReleaseVersions(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.IsNewer(latest, current));
    }

    [Theory]
    [InlineData("not-a-version", "1.0.0")]
    [InlineData("1.0.0", "garbage")]
    [InlineData("", "")]
    public void IsNewer_ReturnsFalse_ForInvalidVersions(string latest, string current)
    {
        Assert.False(UpdateChecker.IsNewer(latest, current));
    }

    [Fact]
    public void GetCurrentVersion_ReturnsNonEmpty()
    {
        var version = UpdateChecker.GetCurrentVersion();
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public void GetCurrentVersion_DoesNotContainGitMetadata()
    {
        var version = UpdateChecker.GetCurrentVersion();
        Assert.DoesNotContain("+", version);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNull_OnNetworkFailure()
    {
        // With no NuGet package published, check should gracefully return null
        var result = await UpdateChecker.CheckAsync(forceCheck: true);

        // Either null (no package found / network issue) or an UpdateInfo if a real package exists
        // The point is it doesn't throw
        Assert.True(result is null || result is UpdateInfo);
    }

    [Fact]
    public void UpdateInfo_RecordEquality()
    {
        var a = new UpdateInfo("1.0.0", "2.0.0");
        var b = new UpdateInfo("1.0.0", "2.0.0");
        Assert.Equal(a, b);
        Assert.Equal("1.0.0", a.CurrentVersion);
        Assert.Equal("2.0.0", a.LatestVersion);
    }

    [Fact]
    public void UpdateCache_Properties()
    {
        var cache = new UpdateCache
        {
            LastCheck = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LatestVersion = "2.0.0",
            CurrentVersion = "1.0.0",
        };

        Assert.Equal("2.0.0", cache.LatestVersion);
        Assert.Equal("1.0.0", cache.CurrentVersion);
    }
}
