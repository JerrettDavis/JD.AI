using FluentAssertions;
using JD.AI.Core.LocalModels;

namespace JD.AI.Tests.LocalModels;

public sealed class LocalModelEnumTests
{
    // ── GpuBackend ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(GpuBackend.Cpu, 0)]
    [InlineData(GpuBackend.Cuda, 1)]
    [InlineData(GpuBackend.Vulkan, 2)]
    [InlineData(GpuBackend.Metal, 3)]
    public void GpuBackend_Values(GpuBackend backend, int expected) =>
        ((int)backend).Should().Be(expected);

    // ── ModelSourceKind ───────────────────────────────────────────────────

    [Theory]
    [InlineData(ModelSourceKind.LocalFile, 0)]
    [InlineData(ModelSourceKind.DirectoryScan, 1)]
    [InlineData(ModelSourceKind.HuggingFace, 2)]
    [InlineData(ModelSourceKind.RemoteUrl, 3)]
    public void ModelSourceKind_Values(ModelSourceKind kind, int expected) =>
        ((int)kind).Should().Be(expected);

    // ── ModelMetadata defaults ────────────────────────────────────────────

    [Fact]
    public void ModelMetadata_OptionalDefaults()
    {
        var meta = new ModelMetadata
        {
            Id = "test",
            DisplayName = "Test",
            FilePath = "/path/test.gguf",
        };
        meta.FileSizeBytes.Should().Be(0);
        meta.Quantization.Should().Be(QuantizationType.Unknown);
        meta.ParameterSize.Should().BeNull();
        meta.Source.Should().Be(ModelSourceKind.LocalFile);
        meta.SourceUri.Should().BeNull();
        meta.FileHash.Should().BeNull();
        meta.AddedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── ModelRegistry ─────────────────────────────────────────────────────

    [Fact]
    public void ModelRegistry_Defaults()
    {
        var registry = new ModelRegistry();
        registry.Version.Should().Be(1);
        registry.Models.Should().BeEmpty();
    }

    [Fact]
    public void ModelRegistry_WithModels()
    {
        var registry = new ModelRegistry
        {
            Models =
            [
                new ModelMetadata { Id = "m1", DisplayName = "M1", FilePath = "/m1.gguf" },
                new ModelMetadata { Id = "m2", DisplayName = "M2", FilePath = "/m2.gguf" },
            ],
        };
        registry.Models.Should().HaveCount(2);
    }
}
