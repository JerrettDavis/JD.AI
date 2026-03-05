// Licensed under the MIT License.

using FluentAssertions;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Infrastructure;

public sealed class TempDirectoryFixtureTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    [Fact]
    public void Constructor_CreatesDirectory()
    {
        Directory.Exists(_fixture.DirectoryPath).Should().BeTrue();
    }

    [Fact]
    public void Path_ContainsJdaiTestPrefix()
    {
        _fixture.DirectoryPath.Should().Contain("jdai-test-");
    }

    [Fact]
    public void CreateFile_CreatesFileWithContent()
    {
        var path = _fixture.CreateFile("test.txt", "hello");

        File.Exists(path).Should().BeTrue();
        File.ReadAllText(path).Should().Be("hello");
    }

    [Fact]
    public void CreateFile_CreatesNestedDirectories()
    {
        var path = _fixture.CreateFile("sub/dir/test.txt", "nested");

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void CreateSubdirectory_CreatesDirectory()
    {
        var path = _fixture.CreateSubdirectory("subdir");

        Directory.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void GetPath_ReturnsCombinedPath()
    {
        var path = _fixture.GetPath("file.txt");

        path.Should().StartWith(_fixture.DirectoryPath);
        path.Should().EndWith("file.txt");
    }

    [Fact]
    public void Dispose_DeletesDirectory()
    {
        var tempPath = _fixture.DirectoryPath;
        _fixture.CreateFile("test.txt", "content");

        _fixture.Dispose();

        Directory.Exists(tempPath).Should().BeFalse();
    }

    public void Dispose() => _fixture.Dispose();
}
