using System.Text;
using JD.AI.Tui.Rendering;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Tui.Agent;

/// <summary>
/// The core agent interaction loop: read input → LLM → tools → render.
/// </summary>
public sealed class AgentLoop
{
    private readonly AgentSession _session;

    public AgentLoop(AgentSession session)
    {
        _session = session;
    }

    /// <summary>
    /// Send a user message through the SK chat completion pipeline
    /// with auto-function-calling enabled (non-streaming).
    /// </summary>
    public async Task<string> RunTurnAsync(
        string userMessage, CancellationToken ct = default)
    {
        _session.History.AddUserMessage(userMessage);

        var chat = _session.Kernel.GetRequiredService<IChatCompletionService>();

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4096,
        };

        try
        {
            var result = await chat.GetChatMessageContentAsync(
                _session.History,
                settings,
                _session.Kernel,
                ct).ConfigureAwait(false);

            var response = result.Content ?? "(no response)";
            _session.History.AddAssistantMessage(response);

            // Update token count estimate
            _session.TotalTokens += JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                .EstimateTokens(response);

            return response;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var errorMsg = $"Error: {ex.Message}";
            ChatRenderer.RenderError(errorMsg);

            // Inject error into history so agent can self-correct
            _session.History.AddAssistantMessage(
                $"[Error occurred: {ex.Message}. I'll try a different approach.]");

            return errorMsg;
        }
    }

    /// <summary>
    /// Send a user message with streaming output — tokens appear as they arrive.
    /// Thinking/reasoning content (via &lt;think&gt; tags or metadata) is rendered
    /// as dim gray text, separate from the response content.
    /// </summary>
    public async Task<string> RunTurnStreamingAsync(
        string userMessage, CancellationToken ct = default)
    {
        _session.History.AddUserMessage(userMessage);

        var chat = _session.Kernel.GetRequiredService<IChatCompletionService>();

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4096,
        };

        try
        {
            var fullResponse = new StringBuilder();
            var parser = new StreamingContentParser();
            var contentStarted = false;
            var thinkingActive = false;

            await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
                _session.History, settings, _session.Kernel, ct).ConfigureAwait(false))
            {
                // Check metadata for reasoning content (OpenAI o1/o3, future providers)
                if (chunk.Metadata is { } meta &&
                    meta.TryGetValue("ReasoningContent", out var reasonObj) &&
                    reasonObj is string { Length: > 0 } reasonText)
                {
                    if (!thinkingActive)
                    {
                        ChatRenderer.BeginThinking();
                        thinkingActive = true;
                    }
                    ChatRenderer.WriteThinkingChunk(reasonText);
                    continue;
                }

                if (chunk.Content is not { Length: > 0 } text)
                    continue;

                // Parse chunk for <think> tags and classify segments
                foreach (var seg in parser.ProcessChunk(text))
                {
                    switch (seg.Kind)
                    {
                        case StreamSegmentKind.EnterThinking:
                            ChatRenderer.BeginThinking();
                            thinkingActive = true;
                            break;

                        case StreamSegmentKind.Thinking:
                            if (!thinkingActive)
                            {
                                ChatRenderer.BeginThinking();
                                thinkingActive = true;
                            }
                            ChatRenderer.WriteThinkingChunk(seg.Text);
                            break;

                        case StreamSegmentKind.ExitThinking:
                            ChatRenderer.EndThinking();
                            thinkingActive = false;
                            break;

                        case StreamSegmentKind.Content:
                            if (thinkingActive)
                            {
                                ChatRenderer.EndThinking();
                                thinkingActive = false;
                            }
                            if (!contentStarted)
                            {
                                ChatRenderer.BeginStreaming();
                                contentStarted = true;
                            }
                            fullResponse.Append(seg.Text);
                            ChatRenderer.WriteStreamingChunk(seg.Text);
                            break;
                    }
                }
            }

            // Flush any buffered tag remnants
            foreach (var seg in parser.Flush())
            {
                if (seg.Kind == StreamSegmentKind.Thinking)
                {
                    ChatRenderer.WriteThinkingChunk(seg.Text);
                }
                else if (seg.Kind == StreamSegmentKind.Content)
                {
                    if (!contentStarted)
                    {
                        ChatRenderer.BeginStreaming();
                        contentStarted = true;
                    }
                    fullResponse.Append(seg.Text);
                    ChatRenderer.WriteStreamingChunk(seg.Text);
                }
            }

            if (thinkingActive) ChatRenderer.EndThinking();
            if (contentStarted) ChatRenderer.EndStreaming();

            var response = fullResponse.Length > 0
                ? fullResponse.ToString()
                : "(no response)";

            _session.History.AddAssistantMessage(response);
            _session.TotalTokens += JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                .EstimateTokens(response);

            return response;
        }
        catch (OperationCanceledException)
        {
            ChatRenderer.EndStreaming();
            throw; // Let caller handle cancellation
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            ChatRenderer.EndStreaming();
            var errorMsg = $"Error: {ex.Message}";
            ChatRenderer.RenderError(errorMsg);

            _session.History.AddAssistantMessage(
                $"[Error occurred: {ex.Message}. I'll try a different approach.]");

            return errorMsg;
        }
    }
}
