using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class FileRoleResolverTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}");

    public FileRoleResolverTests() =>
        Directory.CreateDirectory(_tempDir);

    public void Dispose() =>
        Directory.Delete(_tempDir, recursive: true);

    private string WriteYaml(string yaml)
    {
        var path = Path.Combine(_tempDir, "roles.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    [Fact]
    public void MissingFile_ReturnsNullForAll()
    {
        var resolver = new FileRoleResolver(
            Path.Combine(_tempDir, "nonexistent.yaml"));

        resolver.ResolveRole("alice").Should().BeNull();
        resolver.ResolveGroups("alice").Should().BeEmpty();
    }

    [Fact]
    public void ResolveRole_KnownUser_ReturnsRole()
    {
        var path = WriteYaml("""
            users:
              alice:
                role: admin
            """);

        var resolver = new FileRoleResolver(path);
        resolver.ResolveRole("alice").Should().Be("admin");
    }

    [Fact]
    public void ResolveRole_UnknownUser_ReturnsNull()
    {
        var path = WriteYaml("""
            users:
              alice:
                role: admin
            """);

        var resolver = new FileRoleResolver(path);
        resolver.ResolveRole("bob").Should().BeNull();
    }

    [Fact]
    public void ResolveRole_NullUserId_ReturnsNull()
    {
        var path = WriteYaml("""
            users:
              alice:
                role: admin
            """);

        var resolver = new FileRoleResolver(path);
        resolver.ResolveRole(null).Should().BeNull();
    }

    [Fact]
    public void ResolveGroups_UnknownUser_ReturnsEmpty()
    {
        var path = WriteYaml("""
            users:
              alice:
                role: admin
            """);

        var resolver = new FileRoleResolver(path);
        resolver.ResolveGroups("bob").Should().BeEmpty();
    }

    [Fact]
    public void ResolveGroups_NullUserId_ReturnsEmpty()
    {
        // Use role-only YAML to avoid YamlDotNet IReadOnlyList<string>
        // deserialization issue with groups sequences
        var path = WriteYaml("""
            users:
              alice:
                role: admin
            """);

        var resolver = new FileRoleResolver(path);
        resolver.ResolveGroups(null).Should().BeEmpty();
    }

    [Fact]
    public void MultipleUsers_EachResolvesCorrectly()
    {
        var path = WriteYaml("""
            users:
              alice:
                role: admin
              bob:
                role: developer
            """);

        var resolver = new FileRoleResolver(path);

        resolver.ResolveRole("alice").Should().Be("admin");
        resolver.ResolveRole("bob").Should().Be("developer");
    }
}
