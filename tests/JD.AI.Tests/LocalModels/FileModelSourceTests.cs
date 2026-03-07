using FluentAssertions;
using JD.AI.Core.LocalModels.Sources;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.LocalModels;

public class FileModelSourceTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ScanAsync_ValidGguf_ReturnsSingleModel()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "test-model-Q5_K_M.gguf");
        await File.WriteAllBytesAsync(path, new byte[512]);

        var source = new FileModelSource(path);
        var models = await source.ScanAsync();

        models.Should().ContainSingle();
        models[0].FilePath.Should().Be(path);
        models[0].FileSizeBytes.Should().Be(512);
        models[0].Quantization.Should().Be(Core.LocalModels.QuantizationType.Q5_K_M);
    }

    [Fact]
    public async Task ScanAsync_NonexistentFile_ReturnsEmpty()
    {
        var source = new FileModelSource("/nonexistent.gguf");
        var models = await source.ScanAsync();
        models.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_NonGgufFile_ReturnsEmpty()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "readme.txt");
        await File.WriteAllTextAsync(path, "hello");

        var source = new FileModelSource(path);
        var models = await source.ScanAsync();
        models.Should().BeEmpty();
    }
}
