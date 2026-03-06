using FluentAssertions;
using JD.AI.Core.Providers;

namespace JD.AI.Tests.Providers;

public sealed class ModelCapabilityHeuristicsTests
{
    [Theory]
    [InlineData("llama-3.1-8b-instruct")]
    [InlineData("llama3.2-3b")]
    [InlineData("llama-4-scout")]
    [InlineData("qwen2.5-72b")]
    [InlineData("qwen-3-32b")]
    [InlineData("qwq-32b")]
    [InlineData("mistral-large-latest")]
    [InlineData("mistral-nemo")]
    [InlineData("mixtral-8x7b")]
    [InlineData("gemma2-27b")]
    [InlineData("command-r-plus")]
    [InlineData("phi-3-mini")]
    [InlineData("phi4")]
    [InlineData("deepseek-v3")]
    [InlineData("deepseek-r1")]
    [InlineData("hermes-3-llama")]
    [InlineData("functionary-v3")]
    [InlineData("firefunction-v2")]
    [InlineData("nexusraven-v2")]
    [InlineData("glm-4-9b")]
    [InlineData("granite-3b")]
    public void InferFromName_ToolCapableModels_IncludeToolCalling(
        string modelName)
    {
        var caps = ModelCapabilityHeuristics.InferFromName(modelName);
        caps.HasFlag(ModelCapabilities.ToolCalling).Should().BeTrue();
        caps.HasFlag(ModelCapabilities.Chat).Should().BeTrue();
    }

    [Theory]
    [InlineData("llava-v1.6")]
    [InlineData("bakllava")]
    [InlineData("moondream2")]
    [InlineData("pixtral-12b")]
    [InlineData("llama-3.2-vision-11b")]
    [InlineData("llama3.2-vision")]
    [InlineData("minicpm-v-2.6")]
    [InlineData("gemma3-4b")]
    [InlineData("gemma-3-27b")]
    public void InferFromName_VisionCapableModels_IncludeVision(
        string modelName)
    {
        var caps = ModelCapabilityHeuristics.InferFromName(modelName);
        caps.HasFlag(ModelCapabilities.Vision).Should().BeTrue();
        caps.HasFlag(ModelCapabilities.Chat).Should().BeTrue();
    }

    [Theory]
    [InlineData("pixtral-12b")]
    [InlineData("gemma3-4b")]
    [InlineData("gemma-3-27b")]
    public void InferFromName_ToolAndVisionModel_ReturnsBoth(
        string modelName)
    {
        var caps = ModelCapabilityHeuristics.InferFromName(modelName);
        caps.HasFlag(ModelCapabilities.ToolCalling).Should().BeTrue();
        caps.HasFlag(ModelCapabilities.Vision).Should().BeTrue();
    }

    [Theory]
    [InlineData("unknown-model-xyz")]
    [InlineData("my-custom-gguf")]
    [InlineData("orca-mini")]
    [InlineData("vicuna-13b")]
    public void InferFromName_UnknownModels_ReturnsChatOnly(
        string modelName)
    {
        var caps = ModelCapabilityHeuristics.InferFromName(modelName);
        caps.Should().Be(ModelCapabilities.Chat);
    }

    [Fact]
    public void InferFromName_CaseInsensitive()
    {
        var lower = ModelCapabilityHeuristics.InferFromName("llama-3.1-8b");
        var upper = ModelCapabilityHeuristics.InferFromName("LLAMA-3.1-8B");
        var mixed = ModelCapabilityHeuristics.InferFromName("LLaMA-3.1-8b");

        lower.Should().Be(upper);
        upper.Should().Be(mixed);
        lower.HasFlag(ModelCapabilities.ToolCalling).Should().BeTrue();
    }
}
