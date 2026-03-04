using System.Runtime.CompilerServices;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Specs.Support;
using JD.AI.Specs.Support.Actors;
using JD.AI.Specs.Support.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class AgentLifecycleSteps
{
    private readonly ScenarioContext _context;

    public AgentLifecycleSteps(ScenarioContext context) => _context = context;

    [Given(@"a provider registry with a ""(.*)"" provider")]
    public void GivenAProviderRegistryWithProvider(string providerName)
    {
        var registry = Substitute.For<IProviderRegistry>();
        _context.Set(registry);
        _context.Set(providerName, "providerName");
    }

    [Given(@"a mock chat service that returns ""(.*)""")]
    public void GivenAMockChatServiceThatReturns(string response)
    {
        var chatService = Substitute.For<IChatCompletionService>();

        // Use GetChatMessageContentsAsync (plural) - the actual interface method.
        // GetChatMessageContentAsync (singular) is an extension method and cannot be mocked directly.
        IReadOnlyList<ChatMessageContent> chatResults = [new ChatMessageContent(AuthorRole.Assistant, response)];
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResults);

        // Also set up streaming for orchestration tests that use streaming internally
        chatService.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateAsyncEnumerable([response]));

        _context.Set(chatService);
        _context.Set(response, "expectedResponse");
    }

    [Given(@"a mock chat service that throws ""(.*)""")]
    public void GivenAMockChatServiceThatThrows(string errorMessage)
    {
        var chatService = Substitute.For<IChatCompletionService>();

        // Use GetChatMessageContentsAsync (plural) - the actual interface method
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ChatMessageContent>>(_ => throw new InvalidOperationException(errorMessage));

        // Also throw on streaming
        chatService.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateThrowingEnumerable(new InvalidOperationException(errorMessage)));

        _context.Set(chatService);
    }

    [Given(@"an agent session with model ""(.*)"" from provider ""(.*)""")]
    public void GivenAnAgentSessionWithModelFromProvider(string modelId, string providerName)
    {
        var chatService = _context.Get<IChatCompletionService>();
        var registry = _context.Get<IProviderRegistry>();
        var model = new ProviderModelInfo(modelId, modelId, providerName);

        var kernel = AgentSessionBuilder.BuildKernelWithChatService(chatService);

        registry.BuildKernel(Arg.Any<ProviderModelInfo>()).Returns(kernel);
        var modelList = Task.FromResult<IReadOnlyList<ProviderModelInfo>>([model]);
        registry.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(modelList);

        var session = new AgentSession(registry, kernel, model)
        {
            NoSessionPersistence = true,
        };

        var user = new UserActor(session);
        _context.Set(session);
        _context.Set(user);
        _context.Set(model);
    }

    [When(@"the user sends ""(.*)""")]
    public async Task WhenTheUserSends(string message)
    {
        var user = _context.Get<UserActor>();
        await user.SendMessageAsync(message);
        _context.Set(user.LastResponse ?? string.Empty, "lastResponse");
    }

    [When(@"the user clears the history")]
    public void WhenTheUserClearsTheHistory()
    {
        var session = _context.Get<AgentSession>();
        session.ClearHistory();
    }

    [Then(@"the session history should be empty")]
    public void ThenTheSessionHistoryShouldBeEmpty()
    {
        var session = _context.Get<AgentSession>();
        session.History.Should().BeEmpty();
    }

    [Then(@"the current model should be ""(.*)""")]
    public void ThenTheCurrentModelShouldBe(string modelId)
    {
        var session = _context.Get<AgentSession>();
        session.CurrentModel!.Id.Should().Be(modelId);
    }

    [Then(@"the response should be ""(.*)""")]
    public void ThenTheResponseShouldBe(string expected)
    {
        var response = _context.Get<string>("lastResponse");
        response.Should().Be(expected);
    }

    [Then(@"the response should contain ""(.*)""")]
    public void ThenTheResponseShouldContain(string expected)
    {
        var response = _context.Get<string>("lastResponse");
        response.Should().Contain(expected);
    }

    [Then(@"the session history should contain (\d+) messages")]
    public void ThenTheSessionHistoryShouldContainMessages(int count)
    {
        var session = _context.Get<AgentSession>();
        session.History.Should().HaveCount(count);
    }

    [Then(@"the session history should contain a user message ""(.*)""")]
    public void ThenTheSessionHistoryShouldContainUserMessage(string message)
    {
        var session = _context.Get<AgentSession>();
        session.History.Should().Contain(m =>
            m.Role == AuthorRole.User && m.Content == message);
    }

    [Then(@"the session history should contain an assistant message ""(.*)""")]
    public void ThenTheSessionHistoryShouldContainAssistantMessage(string message)
    {
        var session = _context.Get<AgentSession>();
        session.History.Should().Contain(m =>
            m.Role == AuthorRole.Assistant && m.Content == message);
    }

    [Then(@"the agent output should have rendered an error")]
    public void ThenTheAgentOutputShouldHaveRenderedAnError()
    {
        var spy = _context.Get<SpyAgentOutput>();
        spy.ErrorMessages.Should().NotBeEmpty();
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> CreateAsyncEnumerable(
        IReadOnlyList<string> chunks,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, chunk);
            await Task.Yield();
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async IAsyncEnumerable<StreamingChatMessageContent> CreateThrowingEnumerable(
        Exception ex,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        throw ex;
        yield break; // Required to make this an async enumerator
    }
#pragma warning restore CS1998
}
