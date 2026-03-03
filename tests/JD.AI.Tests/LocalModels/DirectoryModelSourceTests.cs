using FluentAssertions;
using JD.AI.Core.LocalModels.Sources;

namespace JD.AI.Tests.LocalModels;

public class DirectoryModelSourceTests : IDisposable
{
    private readonly string _tempDir;

    public DirectoryModelSourceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-dirscan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ScanAsync_EmptyDir_ReturnsEmpty()
    {
        var source = new DirectoryModelSource(_tempDir);
        var models = await source.ScanAsync();
        models.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_FindsGgufFiles()
    {
        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "model-a-Q4_K_M.gguf"), new byte[128]);
        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "model-b.gguf"), new byte[64]);
        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "readme.txt"), new byte[32]);

        var source = new DirectoryModelSource(_tempDir);
        var models = await source.ScanAsync();

        models.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanAsync_ParsesQuantization()
    {
        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "llama-7b-Q4_K_M.gguf"), new byte[128]);

        var source = new DirectoryModelSource(_tempDir);
        var models = await source.ScanAsync();

        models.Should().ContainSingle();
        models[0].Quantization.Should().Be(Core.LocalModels.QuantizationType.Q4_K_M);
    }

    [Fact]
    public async Task ScanAsync_RecursiveSubdirs()
    {
        var subDir = Path.Combine(_tempDir, "subfolder");
        Directory.CreateDirectory(subDir);
        await File.WriteAllBytesAsync(Path.Combine(subDir, "nested.gguf"), new byte[64]);

        var source = new DirectoryModelSource(_tempDir);
        var models = await source.ScanAsync();

        models.Should().ContainSingle();
    }

    [Fact]
    public async Task ScanAsync_NonexistentDir_ReturnsEmpty()
    {
        var source = new DirectoryModelSource(Path.Combine(_tempDir, "nope"));
        var models = await source.ScanAsync();
        models.Should().BeEmpty();
    }
}
