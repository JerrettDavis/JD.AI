using System.Diagnostics;
using FluentAssertions;
using JD.AI.Telemetry;
using Xunit;

namespace JD.AI.Tests.Telemetry;

public sealed class GenAiAttributesTests
{
    private static readonly string[] ExpectedStopReason = ["stop"];
    private static Activity CreateSampledActivity(string operationName = "test-op")
    {
        using var source = new ActivitySource("GenAiAttributesTests");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = source.StartActivity(operationName);
        activity.Should().NotBeNull("a sampled ActivityListener is registered");
        return activity!;
    }

    [Fact]
    public void SetGenAiRequestAttributes_NullActivity_DoesNotThrow()
    {
        Activity? activity = null;

        var result = activity.SetGenAiRequestAttributes("openai", "gpt-4");

        result.Should().BeNull();
    }

    [Fact]
    public void SetGenAiRequestAttributes_SetsRequiredTags()
    {
        using var activity = CreateSampledActivity();

        activity.SetGenAiRequestAttributes("openai", "gpt-4");

        activity.GetTagItem(GenAiAttributes.SystemName).Should().Be("openai");
        activity.GetTagItem(GenAiAttributes.RequestModel).Should().Be("gpt-4");
        activity.GetTagItem(GenAiAttributes.OperationName).Should().Be("chat");
    }

    [Fact]
    public void SetGenAiRequestAttributes_CustomOperation_SetsOperationTag()
    {
        using var activity = CreateSampledActivity();

        activity.SetGenAiRequestAttributes("anthropic", "claude-3", operation: "completion");

        activity.GetTagItem(GenAiAttributes.OperationName).Should().Be("completion");
    }

    [Fact]
    public void SetGenAiRequestAttributes_WithOptionalParameters_SetsAllTags()
    {
        using var activity = CreateSampledActivity();

        activity.SetGenAiRequestAttributes(
            "openai",
            "gpt-4",
            maxTokens: 1024,
            temperature: 0.7,
            topP: 0.9);

        activity.GetTagItem(GenAiAttributes.RequestMaxTokens).Should().Be(1024);
        activity.GetTagItem(GenAiAttributes.RequestTemperature).Should().Be(0.7);
        activity.GetTagItem(GenAiAttributes.RequestTopP).Should().Be(0.9);
    }

    [Fact]
    public void SetGenAiRequestAttributes_WithoutOptionalParameters_DoesNotSetOptionalTags()
    {
        using var activity = CreateSampledActivity();

        activity.SetGenAiRequestAttributes("openai", "gpt-4");

        activity.GetTagItem(GenAiAttributes.RequestMaxTokens).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.RequestTemperature).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.RequestTopP).Should().BeNull();
    }

    [Fact]
    public void SetGenAiRequestAttributes_ReturnsActivity()
    {
        using var activity = CreateSampledActivity();

        var result = activity.SetGenAiRequestAttributes("openai", "gpt-4");

        result.Should().BeSameAs(activity);
    }

    [Fact]
    public void SetGenAiResponseAttributes_NullActivity_DoesNotThrow()
    {
        Activity? activity = null;

        var result = activity.SetGenAiResponseAttributes(
            responseModel: "gpt-4",
            inputTokens: 100,
            outputTokens: 50,
            finishReason: "stop");

        result.Should().BeNull();
    }

    [Fact]
    public void SetGenAiResponseAttributes_SetsAllTags()
    {
        using var activity = CreateSampledActivity();

        activity.SetGenAiResponseAttributes(
            responseModel: "gpt-4-turbo",
            inputTokens: 150,
            outputTokens: 75,
            finishReason: "stop");

        activity.GetTagItem(GenAiAttributes.ResponseModel).Should().Be("gpt-4-turbo");
        activity.GetTagItem(GenAiAttributes.UsageInputTokens).Should().Be(150);
        activity.GetTagItem(GenAiAttributes.UsageOutputTokens).Should().Be(75);
        var finishReasons = activity.GetTagItem(GenAiAttributes.ResponseFinishReasons);
        finishReasons.Should().BeEquivalentTo(ExpectedStopReason);
    }

    [Fact]
    public void SetGenAiResponseAttributes_SkipsNullOptionalParameters()
    {
        using var activity = CreateSampledActivity();

        activity.SetGenAiResponseAttributes();

        activity.GetTagItem(GenAiAttributes.ResponseModel).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.UsageInputTokens).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.UsageOutputTokens).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.ResponseFinishReasons).Should().BeNull();
    }

    [Fact]
    public void SetGenAiResponseAttributes_PartialParameters_SetsOnlyProvided()
    {
        using var activity = CreateSampledActivity();

        activity.SetGenAiResponseAttributes(inputTokens: 200);

        activity.GetTagItem(GenAiAttributes.UsageInputTokens).Should().Be(200);
        activity.GetTagItem(GenAiAttributes.ResponseModel).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.UsageOutputTokens).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.ResponseFinishReasons).Should().BeNull();
    }

    [Fact]
    public void SetGenAiResponseAttributes_ReturnsActivity()
    {
        using var activity = CreateSampledActivity();

        var result = activity.SetGenAiResponseAttributes(responseModel: "gpt-4");

        result.Should().BeSameAs(activity);
    }

    [Theory]
    [InlineData(GenAiAttributes.SystemName, "gen_ai.system")]
    [InlineData(GenAiAttributes.RequestModel, "gen_ai.request.model")]
    [InlineData(GenAiAttributes.RequestMaxTokens, "gen_ai.request.max_tokens")]
    [InlineData(GenAiAttributes.RequestTemperature, "gen_ai.request.temperature")]
    [InlineData(GenAiAttributes.RequestTopP, "gen_ai.request.top_p")]
    [InlineData(GenAiAttributes.ResponseModel, "gen_ai.response.model")]
    [InlineData(GenAiAttributes.ResponseFinishReasons, "gen_ai.response.finish_reasons")]
    [InlineData(GenAiAttributes.UsageInputTokens, "gen_ai.usage.input_tokens")]
    [InlineData(GenAiAttributes.UsageOutputTokens, "gen_ai.usage.output_tokens")]
    [InlineData(GenAiAttributes.OperationName, "gen_ai.operation.name")]
    public void Constants_HaveCorrectValues(string actual, string expected)
    {
        actual.Should().Be(expected);
    }
}
