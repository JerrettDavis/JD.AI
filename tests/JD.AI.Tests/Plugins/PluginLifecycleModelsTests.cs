using FluentAssertions;
using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;

namespace JD.AI.Tests.Plugins;

public sealed class PluginLifecycleModelsTests
{
    // ── InstalledPluginRecord ────────────────────────────────────────────

    [Fact]
    public void InstalledPluginRecord_RequiredProperties()
    {
        var record = new InstalledPluginRecord
        {
            Id = "my-plugin",
            Name = "My Plugin",
            Version = "2.0.0",
            InstallPath = "/plugins/my-plugin",
            EntryAssemblyPath = "/plugins/my-plugin/MyPlugin.dll",
            ManifestPath = "/plugins/my-plugin/manifest.json",
            Source = "nuget:my-plugin",
        };
        record.Id.Should().Be("my-plugin");
        record.Version.Should().Be("2.0.0");
        record.Source.Should().Be("nuget:my-plugin");
    }

    [Fact]
    public void InstalledPluginRecord_Defaults()
    {
        var record = new InstalledPluginRecord
        {
            Id = "p",
            Name = "P",
            Version = "1.0.0",
            InstallPath = "/p",
            EntryAssemblyPath = "/p/p.dll",
            ManifestPath = "/p/m.json",
            Source = "local",
        };
        record.Publisher.Should().BeNull();
        record.Permissions.Should().BeEmpty();
        record.Enabled.Should().BeTrue();
        record.InstalledAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        record.LastEnabledAtUtc.Should().BeNull();
        record.LastError.Should().BeNull();
    }

    [Fact]
    public void InstalledPluginRecord_MutableProperties()
    {
        var record = new InstalledPluginRecord
        {
            Id = "p",
            Name = "P",
            Version = "1.0.0",
            InstallPath = "/p",
            EntryAssemblyPath = "/p/p.dll",
            ManifestPath = "/p/m.json",
            Source = "local",
        };
        record.Publisher = "JD";
        record.Permissions = ["fs:read", "net:outbound"];
        record.Enabled = false;
        record.LastEnabledAtUtc = DateTimeOffset.UtcNow;
        record.LastError = "load failed";

        record.Publisher.Should().Be("JD");
        record.Permissions.Should().HaveCount(2);
        record.Enabled.Should().BeFalse();
        record.LastEnabledAtUtc.Should().NotBeNull();
        record.LastError.Should().Be("load failed");
    }

    // ── PluginStatusInfo ────────────────────────────────────────────────

    [Fact]
    public void PluginStatusInfo_Roundtrip()
    {
        var now = DateTimeOffset.UtcNow;
        var info = new PluginStatusInfo(
            "id", "name", "1.0.0", true, true,
            "/install", "/entry.dll", "nuget", now, now, null);
        info.Id.Should().Be("id");
        info.Loaded.Should().BeTrue();
        info.LastError.Should().BeNull();
    }

    [Fact]
    public void PluginStatusInfo_RecordEquality()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new PluginStatusInfo("id", "n", "1.0", true, false, "/p", "/e", "s", now, null, null);
        var b = new PluginStatusInfo("id", "n", "1.0", true, false, "/p", "/e", "s", now, null, null);
        a.Should().Be(b);
    }

    // ── PluginInstallArtifact ───────────────────────────────────────────

    [Fact]
    public void PluginInstallArtifact_Roundtrip()
    {
        var manifest = new PluginManifest
        {
            Id = "test",
            Name = "Test",
            Version = "1.0.0",
        };
        var artifact = new PluginInstallArtifact(manifest, "/install", "/entry.dll", "/manifest.json", "local:/path");
        artifact.Manifest.Id.Should().Be("test");
        artifact.InstallPath.Should().Be("/install");
        artifact.Source.Should().Be("local:/path");
    }
}
