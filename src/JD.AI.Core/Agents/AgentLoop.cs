using System.Diagnostics;
using System.Text;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Tracing;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Core.Agents;

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
        var traceCtx = TraceContext.StartTurn(_session.SessionInfo?.Id, _session.TurnIndex);
        var turnEntry = traceCtx.Timeline.BeginOperation("agent.turn");
        DebugLogger.Log(DebugCategory.Agents, "turn={0} traceId={1}", traceCtx.TurnIndex, traceCtx.TraceId);

        await _session.RecordUserTurnAsync(userMessage).ConfigureAwait(false);
        _session.History.AddUserMessage(userMessage);

        var chat = _session.Kernel.GetRequiredService<IChatCompletionService>();

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4096,
        };
        PromptCachePolicy.Apply(
            settings,
            _session.CurrentModel,
            _session.History,
            _session.PromptCachingEnabled,
            _session.PromptCacheTtl);

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await chat.GetChatMessageContentAsync(
                _session.History,
                settings,
                _session.Kernel,
                ct).ConfigureAwait(false);

            sw.Stop();

            var response = result.Content ?? "(no response)";
            _session.History.AddAssistantMessage(response);

            var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                .EstimateTokens(response);

            await _session.RecordAssistantTurnAsync(
                response, durationMs: sw.ElapsedMilliseconds,
                tokensOut: tokenEstimate).ConfigureAwait(false);

            turnEntry.Attributes["tokens_out"] = tokenEstimate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            turnEntry.Complete();
            _session.LastTimeline = traceCtx.Timeline;

            return response;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            turnEntry.Complete("error", ex.Message);
            _session.LastTimeline = traceCtx.Timeline;

            var errorMsg = $"Error: {ex.Message}";
            AgentOutput.Current.RenderError(errorMsg);

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
        var traceCtx = TraceContext.StartTurn(_session.SessionInfo?.Id, _session.TurnIndex);
        var turnEntry = traceCtx.Timeline.BeginOperation("agent.turn");
        DebugLogger.Log(DebugCategory.Agents, "turn={0} traceId={1} streaming=true", traceCtx.TurnIndex, traceCtx.TraceId);

        await _session.RecordUserTurnAsync(userMessage).ConfigureAwait(false);
        _session.History.AddUserMessage(userMessage);

        var chat = _session.Kernel.GetRequiredService<IChatCompletionService>();

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4096,
        };
        PromptCachePolicy.Apply(
            settings,
            _session.CurrentModel,
            _session.History,
            _session.PromptCachingEnabled,
            _session.PromptCacheTtl);

        var sw = Stopwatch.StartNew();
        var output = AgentOutput.Current;
        output.BeginTurn();
        long totalBytes = 0;

        try
        {
            var fullResponse = new StringBuilder();
            var thinkingCapture = new StringBuilder();
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
                        output.BeginThinking();
                        thinkingActive = true;
                    }
                    output.WriteThinkingChunk(reasonText);
                    thinkingCapture.Append(reasonText);
                    totalBytes += System.Text.Encoding.UTF8.GetByteCount(reasonText);
                    continue;
                }

                if (chunk.Content is not { Length: > 0 } text)
                    continue;

                totalBytes += System.Text.Encoding.UTF8.GetByteCount(text);

                // Parse chunk for <think> tags and classify segments
                foreach (var seg in parser.ProcessChunk(text))
                {
                    switch (seg.Kind)
                    {
                        case StreamSegmentKind.EnterThinking:
                            output.BeginThinking();
                            thinkingActive = true;
                            break;

                        case StreamSegmentKind.Thinking:
                            if (!thinkingActive)
                            {
                                output.BeginThinking();
                                thinkingActive = true;
                            }
                            output.WriteThinkingChunk(seg.Text);
                            thinkingCapture.Append(seg.Text);
                            break;

                        case StreamSegmentKind.ExitThinking:
                            output.EndThinking();
                            thinkingActive = false;
                            break;

                        case StreamSegmentKind.Content:
                            if (thinkingActive)
                            {
                                output.EndThinking();
                                thinkingActive = false;
                            }
                            if (!contentStarted)
                            {
                                output.BeginStreaming();
                                contentStarted = true;
                            }
                            fullResponse.Append(seg.Text);
                            output.WriteStreamingChunk(seg.Text);
                            break;
                    }
                }
            }

            // Flush any buffered tag remnants
            foreach (var seg in parser.Flush())
            {
                if (seg.Kind == StreamSegmentKind.Thinking)
                {
                    output.WriteThinkingChunk(seg.Text);
                    thinkingCapture.Append(seg.Text);
                }
                else if (seg.Kind == StreamSegmentKind.Content)
                {
                    if (!contentStarted)
                    {
                        output.BeginStreaming();
                        contentStarted = true;
                    }
                    fullResponse.Append(seg.Text);
                    output.WriteStreamingChunk(seg.Text);
                }
            }

            if (thinkingActive) output.EndThinking();
            if (contentStarted) output.EndStreaming();

            sw.Stop();

            var response = fullResponse.Length > 0
                ? fullResponse.ToString()
                : "(no response)";

            _session.History.AddAssistantMessage(response);

            var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                .EstimateTokens(response);

            var thinkingText = thinkingCapture.Length > 0 ? thinkingCapture.ToString() : null;

            output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, tokenEstimate, totalBytes));

            await _session.RecordAssistantTurnAsync(
                response, thinkingText,
                durationMs: sw.ElapsedMilliseconds,
                tokensOut: tokenEstimate).ConfigureAwait(false);

            turnEntry.Attributes["tokens_out"] = tokenEstimate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            turnEntry.Complete();
            _session.LastTimeline = traceCtx.Timeline;

            return response;
        }
        catch (OperationCanceledException)
        {
            output.EndStreaming();
            sw.Stop();
            turnEntry.Complete("cancelled");
            _session.LastTimeline = traceCtx.Timeline;
            output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, 0, totalBytes));
            throw; // Let caller handle cancellation
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            output.EndStreaming();
            sw.Stop();
            turnEntry.Complete("error", ex.Message);
            _session.LastTimeline = traceCtx.Timeline;
            output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, 0, totalBytes));
            var errorMsg = $"Error: {ex.Message}";
            AgentOutput.Current.RenderError(errorMsg);

            _session.History.AddAssistantMessage(
                $"[Error occurred: {ex.Message}. I'll try a different approach.]");

            return errorMsg;
        }
    }
}
