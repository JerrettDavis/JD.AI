using FluentAssertions;
using JD.AI.Core.LocalModels;

namespace JD.AI.Tests.LocalModels;

public class ModelMetadataTests
{
    [Theory]
    [InlineData("Meta-Llama-3-8B-Instruct-Q4_K_M.gguf", QuantizationType.Q4_K_M, "8B")]
    [InlineData("mistral-7b-instruct-v0.2.Q5_K_S.gguf", QuantizationType.Q5_K_S, "7B")]
    [InlineData("phi-3-mini-4k-instruct-q4_0.gguf", QuantizationType.Q4_0, null)]
    [InlineData("tinyllama-1.1b-chat-v1.0.Q2_K.gguf", QuantizationType.Q2_K, "1.1B")]
    [InlineData("model-no-quant.gguf", QuantizationType.Unknown, null)]
    public void ParseFilename_ExtractsQuantizationAndSize(
        string filename,
        QuantizationType expectedQuant,
        string? expectedSize)
    {
        var (quant, paramSize) = ModelMetadata.ParseFilename(filename);

        quant.Should().Be(expectedQuant);
        if (expectedSize is not null)
            paramSize.Should().Be(expectedSize);
        else
            paramSize.Should().BeNull();
    }

    [Theory]
    [InlineData("Meta-Llama-3-8B.gguf", "Meta Llama 3 8B")]
    [InlineData("tiny_model.gguf", "tiny model")]
    public void DisplayNameFromFilename_FormatsCorrectly(string filename, string expected)
    {
        ModelMetadata.DisplayNameFromFilename(filename).Should().Be(expected);
    }

    [Fact]
    public void ModelMetadata_RecordEquality()
    {
        var a = new ModelMetadata
        {
            Id = "test",
            DisplayName = "Test",
            FilePath = "/path/test.gguf",
        };
        var b = a with { };

        a.Should().Be(b);
    }

    [Fact]
    public void QuantizationType_AllValuesExist()
    {
        Enum.GetValues<QuantizationType>().Should().HaveCountGreaterThan(10);
    }
}
