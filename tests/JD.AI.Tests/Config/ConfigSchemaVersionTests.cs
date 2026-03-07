using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Config;

public sealed class ConfigSchemaVersionTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void GetStoredVersion_NoFile_ReturnsZero()
    {
        var sv = new ConfigSchemaVersion(_fixture.DirectoryPath);

        sv.GetStoredVersion().Should().Be(0);
    }

    [Fact]
    public void StampCurrentVersion_WritesFile()
    {
        var sv = new ConfigSchemaVersion(_fixture.DirectoryPath);

        sv.StampCurrentVersion();

        File.Exists(Path.Combine(_fixture.DirectoryPath, "schema-version.json")).Should().BeTrue();
        sv.GetStoredVersion().Should().Be(ConfigSchemaVersion.CurrentVersion);
    }

    [Fact]
    public void NeedsMigration_NoVersionFile_ReturnsTrue()
    {
        var sv = new ConfigSchemaVersion(_fixture.DirectoryPath);

        sv.NeedsMigration().Should().BeTrue();
    }

    [Fact]
    public void NeedsMigration_AfterStamp_ReturnsFalse()
    {
        var sv = new ConfigSchemaVersion(_fixture.DirectoryPath);
        sv.StampCurrentVersion();

        sv.NeedsMigration().Should().BeFalse();
    }

    [Fact]
    public void ApplyMigrations_FreshInstall_AppliesAndStamps()
    {
        var sv = new ConfigSchemaVersion(_fixture.DirectoryPath);

        var applied = sv.ApplyMigrations();

        applied.Should().BeGreaterThan(0);
        sv.NeedsMigration().Should().BeFalse();
    }

    [Fact]
    public void ApplyMigrations_AlreadyCurrent_ReturnsZero()
    {
        var sv = new ConfigSchemaVersion(_fixture.DirectoryPath);
        sv.StampCurrentVersion();

        var applied = sv.ApplyMigrations();

        applied.Should().Be(0);
    }

    [Fact]
    public void GetStoredVersion_CorruptFile_ReturnsZero()
    {
        File.WriteAllText(Path.Combine(_fixture.DirectoryPath, "schema-version.json"), "not valid json");

        var sv = new ConfigSchemaVersion(_fixture.DirectoryPath);

        sv.GetStoredVersion().Should().Be(0);
    }
}
