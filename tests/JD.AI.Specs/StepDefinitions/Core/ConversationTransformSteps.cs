using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel.ChatCompletion;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ConversationTransformSteps
{
    private readonly ScenarioContext _context;

    public ConversationTransformSteps(ScenarioContext context) => _context = context;

    [Given(@"the session has conversation history with (\d+) messages")]
    public void GivenTheSessionHasConversationHistoryWithMessages(int count)
    {
        var session = _context.Get<AgentSession>();
        for (var i = 0; i < count; i++)
        {
            if (i % 2 == 0)
                session.History.AddUserMessage($"User message {i}");
            else
                session.History.AddAssistantMessage($"Assistant message {i}");
        }
    }

    [When(@"the conversation is transformed with mode ""(.*)"" for model ""(.*)"" on provider ""(.*)""")]
    public async Task WhenTheConversationIsTransformedWithMode(string modeStr, string modelId, string providerName)
    {
        var session = _context.Get<AgentSession>();
        var targetModel = new ProviderModelInfo(modelId, modelId, providerName);
        var transformer = new ConversationTransformer();

        if (!Enum.TryParse<SwitchMode>(modeStr, true, out var mode))
            throw new ArgumentException($"Unknown switch mode: {modeStr}", nameof(modeStr));

        try
        {
            var (history, briefing) = await transformer.TransformAsync(
                session.History, session.Kernel, targetModel, mode);
            _context.Set(history, "transformedHistory");
            _context.Set(briefing, "briefing");
        }
        catch (OperationCanceledException ex)
        {
            _context.Set(ex, "thrownException");
        }
    }

    [Then(@"the transformed history should contain (\d+) messages")]
    public void ThenTheTransformedHistoryShouldContainMessages(int count)
    {
        var history = _context.Get<ChatHistory>("transformedHistory");
        history.Should().HaveCount(count);
    }

    [Then(@"the transformed history should be empty")]
    public void ThenTheTransformedHistoryShouldBeEmpty()
    {
        var history = _context.Get<ChatHistory>("transformedHistory");
        history.Should().BeEmpty();
    }

    [Then(@"the transformed history should contain a system message with the summary")]
    public void ThenTheTransformedHistoryShouldContainSystemMessageWithSummary()
    {
        var history = _context.Get<ChatHistory>("transformedHistory");
        history.Should().Contain(m => m.Role == AuthorRole.System);
    }

    [Then(@"the transformed history should contain a system message")]
    public void ThenTheTransformedHistoryShouldContainSystemMessage()
    {
        var history = _context.Get<ChatHistory>("transformedHistory");
        history.Should().Contain(m => m.Role == AuthorRole.System);
    }

    [Then(@"an OperationCanceledException should be thrown")]
    public void ThenAnOperationCanceledExceptionShouldBeThrown()
    {
        _context.TryGetValue<OperationCanceledException>("thrownException", out var ex)
            .Should().BeTrue("an OperationCanceledException should have been thrown");
        ex.Should().NotBeNull();
    }

    [Then(@"the briefing should not be null")]
    public void ThenTheBriefingShouldNotBeNull()
    {
        var briefing = _context.Get<string?>("briefing");
        briefing.Should().NotBeNull();
    }
}
