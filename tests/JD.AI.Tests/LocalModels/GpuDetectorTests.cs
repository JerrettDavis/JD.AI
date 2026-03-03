using FluentAssertions;
using JD.AI.Core.LocalModels;

namespace JD.AI.Tests.LocalModels;

public class GpuDetectorTests
{
    [Fact]
    public void Detect_ReturnsDeterministicResult()
    {
        GpuDetector.Reset();
        var result1 = GpuDetector.Detect();
        var result2 = GpuDetector.Detect();

        result1.Should().Be(result2, "cached result should be consistent");
    }

    [Fact]
    public void Detect_ReturnsValidBackend()
    {
        GpuDetector.Reset();
        var backend = GpuDetector.Detect();

        backend.Should().BeOneOf(
            GpuBackend.Cpu,
            GpuBackend.Cuda,
            GpuBackend.Vulkan,
            GpuBackend.Metal);
    }

    [Fact]
    public void RecommendGpuLayers_CpuReturnsZero()
    {
        GpuDetector.RecommendGpuLayers(GpuBackend.Cpu, 1_000_000)
            .Should().Be(0);
    }

    [Fact]
    public void RecommendGpuLayers_GpuReturnsAll()
    {
        GpuDetector.RecommendGpuLayers(GpuBackend.Cuda, 1_000_000)
            .Should().Be(-1, "-1 means offload all layers");
    }

    [Fact]
    public void Reset_ClearsCache()
    {
        GpuDetector.Detect();
        GpuDetector.Reset();
        // Should not throw — just re-detect
        GpuDetector.Detect();
    }
}
