using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Config;

public sealed class FileRemoteConfigSourceTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task FetchAsync_ExistingFile_ReturnsContent()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "config.yaml");
        await File.WriteAllTextAsync(path, "apiVersion: jdai/v1");

        var source = new FileRemoteConfigSource(path);
        var result = await source.FetchAsync();

        result.Should().NotBeNull();
        result!.Content.Should().Be("apiVersion: jdai/v1");
    }

    [Fact]
    public async Task FetchAsync_NonExistentFile_ReturnsNull()
    {
        var source = new FileRemoteConfigSource(Path.Combine(_fixture.DirectoryPath, "missing.yaml"));
        var result = await source.FetchAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_UnchangedFile_ReturnsNullOnSecondCall()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "config.yaml");
        await File.WriteAllTextAsync(path, "content: stable");

        var source = new FileRemoteConfigSource(path);
        var first = await source.FetchAsync();
        var second = await source.FetchAsync();

        first.Should().NotBeNull();
        second.Should().BeNull("content has not changed");
    }

    [Fact]
    public async Task FetchAsync_ChangedFile_ReturnsNewContent()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "config.yaml");
        await File.WriteAllTextAsync(path, "version: 1");

        var source = new FileRemoteConfigSource(path);
        await source.FetchAsync(); // First read

        await File.WriteAllTextAsync(path, "version: 2");
        var result = await source.FetchAsync();

        result.Should().NotBeNull();
        result!.Content.Should().Be("version: 2");
    }

    [Fact]
    public async Task FetchAsync_YamlExtension_SetsContentType()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "config.yaml");
        await File.WriteAllTextAsync(path, "key: value");

        var source = new FileRemoteConfigSource(path);
        var result = await source.FetchAsync();

        result!.ContentType.Should().Be("yaml");
    }

    [Fact]
    public async Task FetchAsync_JsonExtension_SetsContentType()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "config.json");
        await File.WriteAllTextAsync(path, "{}");

        var source = new FileRemoteConfigSource(path);
        var result = await source.FetchAsync();

        result!.ContentType.Should().Be("json");
    }

    [Fact]
    public async Task FetchAsync_SetsVersionAsHash()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "config.yaml");
        await File.WriteAllTextAsync(path, "data: test");

        var source = new FileRemoteConfigSource(path);
        var result = await source.FetchAsync();

        result!.Version.Should().NotBeNullOrEmpty();
        result.Version.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Name_ReturnsFile()
    {
        var source = new FileRemoteConfigSource("/tmp/test.yaml");
        source.Name.Should().Be("file");
    }
}
