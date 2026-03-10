using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Core.Agents;

/// <summary>
/// Builds provider-appropriate prompt execution settings for agent loops.
/// </summary>
internal static class AgentExecutionSettingsFactory
{
    private const int DefaultMaxTokens = 4096;

    /// <summary>
    /// Creates execution settings for the given model, enabling tool invocation
    /// only when explicitly requested by caller logic.
    /// </summary>
    internal static PromptExecutionSettings Create(
        ProviderModelInfo? model,
        bool enableTools)
    {
        var maxTokens = model?.MaxOutputTokens is > 0
            ? model.MaxOutputTokens
            : DefaultMaxTokens;

        if (IsMistral(model))
        {
#pragma warning disable SKEXP0070
            return new MistralAIPromptExecutionSettings
            {
                ModelId = model?.Id,
                MaxTokens = maxTokens,
                ToolCallBehavior = enableTools
                    ? MistralAIToolCallBehavior.AutoInvokeKernelFunctions
                    : null,
            };
#pragma warning restore SKEXP0070
        }

        return new OpenAIPromptExecutionSettings
        {
            ModelId = model?.Id,
            MaxTokens = maxTokens,
            FunctionChoiceBehavior = enableTools
                ? FunctionChoiceBehavior.Auto(autoInvoke: true)
                : null,
        };
    }

    /// <summary>
    /// Returns true when the given settings currently include tools/functions.
    /// </summary>
    internal static bool HasToolsEnabled(PromptExecutionSettings settings)
    {
#pragma warning disable SKEXP0070
        if (settings is MistralAIPromptExecutionSettings mistral)
            return mistral.ToolCallBehavior is not null;
#pragma warning restore SKEXP0070

        return settings.FunctionChoiceBehavior is not null;
    }

    private static bool IsMistral(ProviderModelInfo? model) =>
        string.Equals(model?.ProviderName, "Mistral", StringComparison.OrdinalIgnoreCase);
}
