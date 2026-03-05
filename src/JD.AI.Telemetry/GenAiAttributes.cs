using System.Diagnostics;

namespace JD.AI.Telemetry;

/// <summary>
/// OpenTelemetry Gen AI semantic convention attribute names.
/// See: https://opentelemetry.io/docs/specs/semconv/gen-ai/
/// </summary>
public static class GenAiAttributes
{
    // --- Request attributes ---
    public const string SystemName = "gen_ai.system";
    public const string RequestModel = "gen_ai.request.model";
    public const string RequestMaxTokens = "gen_ai.request.max_tokens";
    public const string RequestTemperature = "gen_ai.request.temperature";
    public const string RequestTopP = "gen_ai.request.top_p";

    // --- Response attributes ---
    public const string ResponseModel = "gen_ai.response.model";
    public const string ResponseFinishReasons = "gen_ai.response.finish_reasons";

    // --- Usage attributes ---
    public const string UsageInputTokens = "gen_ai.usage.input_tokens";
    public const string UsageOutputTokens = "gen_ai.usage.output_tokens";

    // --- Operation attributes ---
    public const string OperationName = "gen_ai.operation.name";

    /// <summary>
    /// Sets Gen AI semantic convention attributes on an <see cref="Activity"/> span.
    /// </summary>
    public static Activity? SetGenAiRequestAttributes(
        this Activity? activity,
        string system,
        string model,
        string operation = "chat",
        int? maxTokens = null,
        double? temperature = null,
        double? topP = null)
    {
        if (activity is null) return null;

        activity.SetTag(SystemName, system);
        activity.SetTag(RequestModel, model);
        activity.SetTag(OperationName, operation);

        if (maxTokens.HasValue)
            activity.SetTag(RequestMaxTokens, maxTokens.Value);
        if (temperature.HasValue)
            activity.SetTag(RequestTemperature, temperature.Value);
        if (topP.HasValue)
            activity.SetTag(RequestTopP, topP.Value);

        return activity;
    }

    /// <summary>
    /// Sets Gen AI response attributes on an <see cref="Activity"/> span.
    /// </summary>
    public static Activity? SetGenAiResponseAttributes(
        this Activity? activity,
        string? responseModel = null,
        int? inputTokens = null,
        int? outputTokens = null,
        string? finishReason = null)
    {
        if (activity is null) return null;

        if (responseModel is not null)
            activity.SetTag(ResponseModel, responseModel);
        if (inputTokens.HasValue)
            activity.SetTag(UsageInputTokens, inputTokens.Value);
        if (outputTokens.HasValue)
            activity.SetTag(UsageOutputTokens, outputTokens.Value);
        if (finishReason is not null)
            activity.SetTag(ResponseFinishReasons, new[] { finishReason });

        return activity;
    }
}
