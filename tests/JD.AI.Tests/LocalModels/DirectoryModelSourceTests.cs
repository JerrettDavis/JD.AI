using FluentAssertions;
using JD.AI.Core.LocalModels.Sources;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.LocalModels;

public class DirectoryModelSourceTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ScanAsync_EmptyDir_ReturnsEmpty()
    {
        var source = new DirectoryModelSource(_fixture.DirectoryPath);
        var models = await source.ScanAsync();
        models.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_FindsGgufFiles()
    {
        await File.WriteAllBytesAsync(Path.Combine(_fixture.DirectoryPath, "model-a-Q4_K_M.gguf"), new byte[128]);
        await File.WriteAllBytesAsync(Path.Combine(_fixture.DirectoryPath, "model-b.gguf"), new byte[64]);
        await File.WriteAllBytesAsync(Path.Combine(_fixture.DirectoryPath, "readme.txt"), new byte[32]);

        var source = new DirectoryModelSource(_fixture.DirectoryPath);
        var models = await source.ScanAsync();

        models.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanAsync_ParsesQuantization()
    {
        await File.WriteAllBytesAsync(Path.Combine(_fixture.DirectoryPath, "llama-7b-Q4_K_M.gguf"), new byte[128]);

        var source = new DirectoryModelSource(_fixture.DirectoryPath);
        var models = await source.ScanAsync();

        models.Should().ContainSingle();
        models[0].Quantization.Should().Be(Core.LocalModels.QuantizationType.Q4_K_M);
    }

    [Fact]
    public async Task ScanAsync_RecursiveSubdirs()
    {
        var subDir = Path.Combine(_fixture.DirectoryPath, "subfolder");
        Directory.CreateDirectory(subDir);
        await File.WriteAllBytesAsync(Path.Combine(subDir, "nested.gguf"), new byte[64]);

        var source = new DirectoryModelSource(_fixture.DirectoryPath);
        var models = await source.ScanAsync();

        models.Should().ContainSingle();
    }

    [Fact]
    public async Task ScanAsync_NonexistentDir_ReturnsEmpty()
    {
        var source = new DirectoryModelSource(Path.Combine(_fixture.DirectoryPath, "nope"));
        var models = await source.ScanAsync();
        models.Should().BeEmpty();
    }
}
