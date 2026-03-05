using FluentAssertions;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Config;

public sealed class ConfigSchemaVersionTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigSchemaVersionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-csv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void GetStoredVersion_NoFile_ReturnsZero()
    {
        var sv = new ConfigSchemaVersion(_tempDir);

        sv.GetStoredVersion().Should().Be(0);
    }

    [Fact]
    public void StampCurrentVersion_WritesFile()
    {
        var sv = new ConfigSchemaVersion(_tempDir);

        sv.StampCurrentVersion();

        File.Exists(Path.Combine(_tempDir, "schema-version.json")).Should().BeTrue();
        sv.GetStoredVersion().Should().Be(ConfigSchemaVersion.CurrentVersion);
    }

    [Fact]
    public void NeedsMigration_NoVersionFile_ReturnsTrue()
    {
        var sv = new ConfigSchemaVersion(_tempDir);

        sv.NeedsMigration().Should().BeTrue();
    }

    [Fact]
    public void NeedsMigration_AfterStamp_ReturnsFalse()
    {
        var sv = new ConfigSchemaVersion(_tempDir);
        sv.StampCurrentVersion();

        sv.NeedsMigration().Should().BeFalse();
    }

    [Fact]
    public void ApplyMigrations_FreshInstall_AppliesAndStamps()
    {
        var sv = new ConfigSchemaVersion(_tempDir);

        var applied = sv.ApplyMigrations();

        applied.Should().BeGreaterThan(0);
        sv.NeedsMigration().Should().BeFalse();
    }

    [Fact]
    public void ApplyMigrations_AlreadyCurrent_ReturnsZero()
    {
        var sv = new ConfigSchemaVersion(_tempDir);
        sv.StampCurrentVersion();

        var applied = sv.ApplyMigrations();

        applied.Should().Be(0);
    }

    [Fact]
    public void GetStoredVersion_CorruptFile_ReturnsZero()
    {
        File.WriteAllText(Path.Combine(_tempDir, "schema-version.json"), "not valid json");

        var sv = new ConfigSchemaVersion(_tempDir);

        sv.GetStoredVersion().Should().Be(0);
    }
}
