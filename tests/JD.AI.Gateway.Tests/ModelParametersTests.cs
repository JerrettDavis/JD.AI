using FluentAssertions;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;

namespace JD.AI.Gateway.Tests;

/// <summary>
/// Tests for <see cref="ModelParameters"/> configuration and the
/// <see cref="AgentPoolService.BuildExecutionSettings"/> mapping.
/// </summary>
public sealed class ModelParametersTests
{
    [Fact]
    public void BuildExecutionSettings_NullParameters_ReturnsDefaults()
    {
        var settings = AgentPoolService.BuildExecutionSettings(null);

        settings.MaxTokens.Should().Be(4096);
        settings.Temperature.Should().BeNull();
        settings.TopP.Should().BeNull();
        settings.FrequencyPenalty.Should().BeNull();
        settings.PresencePenalty.Should().BeNull();
        settings.Seed.Should().BeNull();
        settings.StopSequences.Should().BeNull();
        settings.ExtensionData.Should().BeNull();
    }

    [Fact]
    public void BuildExecutionSettings_EmptyParameters_ReturnsDefaults()
    {
        var p = new ModelParameters();

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.MaxTokens.Should().Be(4096);
        settings.Temperature.Should().BeNull();
        settings.ExtensionData.Should().BeNull();
    }

    [Fact]
    public void BuildExecutionSettings_Temperature_MapsCorrectly()
    {
        var p = new ModelParameters { Temperature = 0.7 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.Temperature.Should().Be(0.7);
    }

    [Fact]
    public void BuildExecutionSettings_TopP_MapsCorrectly()
    {
        var p = new ModelParameters { TopP = 0.9 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.TopP.Should().Be(0.9);
    }

    [Fact]
    public void BuildExecutionSettings_MaxTokens_OverridesDefault()
    {
        var p = new ModelParameters { MaxTokens = 8192 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.MaxTokens.Should().Be(8192);
    }

    [Fact]
    public void BuildExecutionSettings_FrequencyAndPresencePenalty_Map()
    {
        var p = new ModelParameters { FrequencyPenalty = 0.5, PresencePenalty = -0.3 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.FrequencyPenalty.Should().Be(0.5);
        settings.PresencePenalty.Should().Be(-0.3);
    }

    [Fact]
    public void BuildExecutionSettings_Seed_MapsCorrectly()
    {
        var p = new ModelParameters { Seed = 42 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.Seed.Should().Be(42);
    }

    [Fact]
    public void BuildExecutionSettings_StopSequences_MapsWhenNonEmpty()
    {
        var p = new ModelParameters { StopSequences = ["<|end|>", "[DONE]"] };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.StopSequences.Should().BeEquivalentTo(["<|end|>", "[DONE]"]);
    }

    [Fact]
    public void BuildExecutionSettings_StopSequences_SkippedWhenEmpty()
    {
        var p = new ModelParameters { StopSequences = [] };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.StopSequences.Should().BeNull();
    }

    [Fact]
    public void BuildExecutionSettings_OllamaTopK_GoesToExtensionData()
    {
        var p = new ModelParameters { TopK = 40 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.ExtensionData.Should().ContainKey("top_k");
        settings.ExtensionData!["top_k"].Should().Be(40);
    }

    [Fact]
    public void BuildExecutionSettings_OllamaContextWindow_GoesToExtensionData()
    {
        var p = new ModelParameters { ContextWindowSize = 32768 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.ExtensionData.Should().ContainKey("num_ctx");
        settings.ExtensionData!["num_ctx"].Should().Be(32768);
    }

    [Fact]
    public void BuildExecutionSettings_ContextWindowZero_SkippedInExtensionData()
    {
        var p = new ModelParameters { ContextWindowSize = 0 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.ExtensionData.Should().BeNull();
    }

    [Fact]
    public void BuildExecutionSettings_OllamaRepeatPenalty_GoesToExtensionData()
    {
        var p = new ModelParameters { RepeatPenalty = 1.2 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.ExtensionData.Should().ContainKey("repeat_penalty");
        settings.ExtensionData!["repeat_penalty"].Should().Be(1.2);
    }

    [Fact]
    public void BuildExecutionSettings_AllOllamaParams_CombinedInExtensionData()
    {
        var p = new ModelParameters { TopK = 50, ContextWindowSize = 16384, RepeatPenalty = 1.1 };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.ExtensionData.Should().HaveCount(3);
        settings.ExtensionData!["top_k"].Should().Be(50);
        settings.ExtensionData["num_ctx"].Should().Be(16384);
        settings.ExtensionData["repeat_penalty"].Should().Be(1.1);
    }

    [Fact]
    public void BuildExecutionSettings_FullConfig_AllFieldsMapped()
    {
        var p = new ModelParameters
        {
            Temperature = 0.8,
            TopP = 0.95,
            TopK = 40,
            MaxTokens = 2048,
            ContextWindowSize = 65536,
            FrequencyPenalty = 0.3,
            PresencePenalty = 0.1,
            RepeatPenalty = 1.15,
            Seed = 123,
            StopSequences = ["STOP"],
        };

        var settings = AgentPoolService.BuildExecutionSettings(p);

        settings.Temperature.Should().Be(0.8);
        settings.TopP.Should().Be(0.95);
        settings.MaxTokens.Should().Be(2048);
        settings.FrequencyPenalty.Should().Be(0.3);
        settings.PresencePenalty.Should().Be(0.1);
        settings.Seed.Should().Be(123);
        settings.StopSequences.Should().ContainSingle("STOP");
        settings.ExtensionData.Should().ContainKey("top_k");
        settings.ExtensionData.Should().ContainKey("num_ctx");
        settings.ExtensionData.Should().ContainKey("repeat_penalty");
    }

    [Fact]
    public void ModelParameters_DefaultValues_AllNull()
    {
        var p = new ModelParameters();

        p.Temperature.Should().BeNull();
        p.TopP.Should().BeNull();
        p.TopK.Should().BeNull();
        p.MaxTokens.Should().BeNull();
        p.ContextWindowSize.Should().BeNull();
        p.FrequencyPenalty.Should().BeNull();
        p.PresencePenalty.Should().BeNull();
        p.RepeatPenalty.Should().BeNull();
        p.Seed.Should().BeNull();
        p.StopSequences.Should().BeEmpty();
    }

    [Fact]
    public void AgentDefinition_DefaultParameters_NotNull()
    {
        var def = new AgentDefinition();

        def.Parameters.Should().NotBeNull();
        def.Parameters.Temperature.Should().BeNull();
    }
}
