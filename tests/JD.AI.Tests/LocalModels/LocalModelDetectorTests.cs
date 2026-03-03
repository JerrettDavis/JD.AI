using FluentAssertions;
using JD.AI.Core.LocalModels;
using JD.AI.Core.Providers;

namespace JD.AI.Tests.LocalModels;

public class LocalModelDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public LocalModelDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-detector-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task DetectAsync_NoModels_NotAvailable()
    {
        var registry = new LocalModelRegistry(_tempDir);
        var detector = new LocalModelDetector(registry);

        var info = await detector.DetectAsync();

        info.Name.Should().Be("Local");
        info.IsAvailable.Should().BeFalse();
        info.Models.Should().BeEmpty();
        info.StatusMessage.Should().Contain("No models found");
    }

    [Fact]
    public async Task DetectAsync_WithModels_Available()
    {
        // Create a dummy GGUF file
        var ggufPath = Path.Combine(_tempDir, "test-7b-Q4_K_M.gguf");
        await File.WriteAllBytesAsync(ggufPath, new byte[1024]);

        var registry = new LocalModelRegistry(_tempDir);
        var detector = new LocalModelDetector(registry);

        var info = await detector.DetectAsync();

        info.Name.Should().Be("Local");
        info.IsAvailable.Should().BeTrue();
        info.Models.Should().HaveCount(1);
        info.StatusMessage.Should().Contain("1 model(s)");
    }

    [Fact]
    public void ProviderName_IsLocal()
    {
        var detector = new LocalModelDetector();
        detector.ProviderName.Should().Be("Local");
    }

    [Fact]
    public async Task DetectAsync_MultipleModels_ReturnsAll()
    {
        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "model-a-Q4_K_M.gguf"), new byte[512]);
        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "model-b-Q5_K_S.gguf"), new byte[768]);

        var registry = new LocalModelRegistry(_tempDir);
        var detector = new LocalModelDetector(registry);

        var info = await detector.DetectAsync();

        info.IsAvailable.Should().BeTrue();
        info.Models.Should().HaveCount(2);
    }

    [Fact]
    public async Task DetectAsync_PersistsRegistry()
    {
        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "persist-test.gguf"), new byte[256]);

        var registry = new LocalModelRegistry(_tempDir);
        var detector = new LocalModelDetector(registry);
        await detector.DetectAsync();

        // Registry file should exist now
        File.Exists(Path.Combine(_tempDir, "registry.json")).Should().BeTrue();
    }
}
