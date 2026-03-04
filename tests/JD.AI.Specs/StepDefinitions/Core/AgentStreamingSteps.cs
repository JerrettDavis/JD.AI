using System.Runtime.CompilerServices;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Providers;
using JD.AI.Specs.Support;
using JD.AI.Specs.Support.Actors;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class AgentStreamingSteps
{
    private readonly ScenarioContext _context;

    public AgentStreamingSteps(ScenarioContext context) => _context = context;

    [Given(@"a mock streaming chat service that yields chunks (.+)")]
    public void GivenAMockStreamingChatServiceThatYieldsChunks(string chunksSpec)
    {
        // Handles both: single chunk in quotes and multiple quoted chunks
        var chunks = ParseQuotedChunks(chunksSpec);
        var chatService = CreateStreamingChatService(chunks);
        _context.Set(chatService);
    }

    [Given(@"a mock streaming chat service with reasoning metadata ""(.*)"" and content ""(.*)""")]
    public void GivenAMockStreamingChatServiceWithReasoningMetadata(string reasoning, string content)
    {
        var chatService = Substitute.For<IChatCompletionService>();
        chatService.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateReasoningStream(reasoning, content));

        // Also set up non-streaming fallback using the actual interface method (plural)
        IReadOnlyList<ChatMessageContent> chatResults = [new ChatMessageContent(AuthorRole.Assistant, content)];
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResults);

        _context.Set(chatService);
    }

    [Given(@"a mock streaming chat service that yields no content")]
    public void GivenAMockStreamingChatServiceThatYieldsNoContent()
    {
        var chatService = CreateStreamingChatService([]);
        _context.Set(chatService);
    }

    [When(@"the user sends ""(.*)"" in streaming mode")]
    public async Task WhenTheUserSendsInStreamingMode(string message)
    {
        var user = _context.Get<UserActor>();
        await user.SendMessageStreamingAsync(message);
        _context.Set(user.LastResponse ?? string.Empty, "lastResponse");
    }

    [Then(@"streaming output should have started and ended")]
    public void ThenStreamingOutputShouldHaveStartedAndEnded()
    {
        var spy = _context.Get<SpyAgentOutput>();
        spy.StreamingStarted.Should().BeTrue("streaming should have started");
        spy.StreamingEnded.Should().BeTrue("streaming should have ended");
    }

    [Then(@"thinking output should have started")]
    public void ThenThinkingOutputShouldHaveStarted()
    {
        var spy = _context.Get<SpyAgentOutput>();
        spy.ThinkingStarted.Should().BeTrue("thinking output should have started");
    }

    [Then(@"the captured thinking content should contain ""(.*)""")]
    public void ThenTheCapturedThinkingContentShouldContain(string expected)
    {
        var spy = _context.Get<SpyAgentOutput>();
        spy.ThinkingContent.Should().Contain(expected);
    }

    [Then(@"the turn metrics should have been recorded")]
    public void ThenTheTurnMetricsShouldHaveBeenRecorded()
    {
        var spy = _context.Get<SpyAgentOutput>();
        spy.TurnEnded.Should().BeTrue();
        spy.LastTurnMetrics.Should().NotBeNull();
    }

    private static IChatCompletionService CreateStreamingChatService(
        IReadOnlyList<string> chunks)
    {
        var chatService = Substitute.For<IChatCompletionService>();

        // Set up streaming
        chatService.GetStreamingChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateAsyncEnumerable(chunks));

        // Set up non-streaming fallback using actual interface method (plural)
        var combined = string.Join("", chunks);
        IReadOnlyList<ChatMessageContent> chatResults = [new ChatMessageContent(AuthorRole.Assistant, combined)];
        chatService.GetChatMessageContentsAsync(
                Arg.Any<ChatHistory>(),
                Arg.Any<PromptExecutionSettings>(),
                Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResults);

        return chatService;
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

    private static async IAsyncEnumerable<StreamingChatMessageContent> CreateReasoningStream(
        string reasoning, string content,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // First yield reasoning metadata chunk
        var reasoningMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ReasoningContent"] = reasoning };
        ct.ThrowIfCancellationRequested();
        yield return new StreamingChatMessageContent(
            AuthorRole.Assistant, null, metadata: reasoningMeta);
        await Task.Yield();

        // Then yield content chunk
        ct.ThrowIfCancellationRequested();
        yield return new StreamingChatMessageContent(
            AuthorRole.Assistant, content);
        await Task.Yield();
    }

    private static string[] ParseQuotedChunks(string spec)
    {
        var chunks = new List<string>();
        var inQuote = false;
        var current = new System.Text.StringBuilder();

        foreach (var c in spec)
        {
            if (c == '"')
            {
                if (inQuote)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }
                inQuote = !inQuote;
            }
            else if (inQuote)
            {
                current.Append(c);
            }
        }

        return chunks.Count > 0 ? [.. chunks] : [spec];
    }
}
