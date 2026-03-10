using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Tests.Agents;

public sealed class AgentExecutionSettingsFactoryTests
{
    [Fact]
    public void Create_NonMistralToolCapableModel_UsesOpenAiFunctionChoiceBehavior()
    {
        var model = new ProviderModelInfo(
            "gpt-4.1",
            "GPT-4.1",
            "OpenAI",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var settings = AgentExecutionSettingsFactory.Create(model, enableTools: true);

        var openAiSettings = Assert.IsType<OpenAIPromptExecutionSettings>(settings);
        Assert.NotNull(openAiSettings.FunctionChoiceBehavior);
    }

    [Fact]
    public void Create_MistralToolCapableModel_UsesMistralToolCallBehavior()
    {
        var model = new ProviderModelInfo(
            "mistral-large-pixtral-2411",
            "Mistral Large Pixtral 2411",
            "Mistral",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var settings = AgentExecutionSettingsFactory.Create(model, enableTools: true);

#pragma warning disable SKEXP0070
        var mistralSettings = Assert.IsType<MistralAIPromptExecutionSettings>(settings);
        Assert.NotNull(mistralSettings.ToolCallBehavior);
#pragma warning restore SKEXP0070
    }

    [Fact]
    public void HasToolsEnabled_OpenAiSettings_ReflectsFunctionChoiceBehavior()
    {
        var model = new ProviderModelInfo(
            "gpt-4.1",
            "GPT-4.1",
            "OpenAI",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var withTools = AgentExecutionSettingsFactory.Create(model, enableTools: true);
        var withoutTools = AgentExecutionSettingsFactory.Create(model, enableTools: false);

        Assert.True(AgentExecutionSettingsFactory.HasToolsEnabled(withTools));
        Assert.False(AgentExecutionSettingsFactory.HasToolsEnabled(withoutTools));
    }

    [Fact]
    public void HasToolsEnabled_MistralSettings_ReflectsToolCallBehavior()
    {
        var model = new ProviderModelInfo(
            "mistral-large-pixtral-2411",
            "Mistral Large Pixtral 2411",
            "Mistral",
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

        var withTools = AgentExecutionSettingsFactory.Create(model, enableTools: true);
        var withoutTools = AgentExecutionSettingsFactory.Create(model, enableTools: false);

        Assert.True(AgentExecutionSettingsFactory.HasToolsEnabled(withTools));
        Assert.False(AgentExecutionSettingsFactory.HasToolsEnabled(withoutTools));
    }
}
