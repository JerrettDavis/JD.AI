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
    /// Tool calls are still handled by <see cref="ToolConfirmationFilter"/> between chunks.
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
            ChatRenderer.BeginStreaming();

            await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
                _session.History, settings, _session.Kernel, ct).ConfigureAwait(false))
            {
                if (chunk.Content is { Length: > 0 } text)
                {
                    fullResponse.Append(text);
                    ChatRenderer.WriteStreamingChunk(text);
                }
            }

            ChatRenderer.EndStreaming();

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
