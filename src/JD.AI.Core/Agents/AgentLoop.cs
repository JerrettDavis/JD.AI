using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JD.AI.Core.Events;
using JD.AI.Core.Memory;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using JD.AI.Core.Tools;
using JD.AI.Core.Tracing;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

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

        _session.ResetTurnState();
        PruneUnpairedFunctionCalls(_session.History);
        await _session.RecordUserTurnAsync(userMessage).ConfigureAwait(false);
        _session.History.AddUserMessage(userMessage);
        var historySnapshot = _session.History.Count;

        var chat = _session.Kernel.GetRequiredService<IChatCompletionService>();

        var settings = BuildExecutionSettings();
        PromptCachePolicy.Apply(
            settings,
            _session.CurrentModel,
            _session.History,
            _session.PromptCachingEnabled,
            _session.PromptCacheTtl);

        var sw = Stopwatch.StartNew();

        using var turnActivity = AgentInstrumentation.AgentSource.StartActivity(
            "agent.turn",
            ActivityKind.Internal);
        turnActivity?.SetTag(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName ?? "unknown");
        turnActivity?.SetTag(AgentInstrumentation.AttrRequestModel, _session.CurrentModel?.Id ?? "unknown");
        turnActivity?.SetTag(AgentInstrumentation.AttrOperationName, "chat");
        if (_session.CurrentModel?.MaxOutputTokens is > 0)
            turnActivity?.SetTag(AgentInstrumentation.AttrRequestMaxTokens, _session.CurrentModel.MaxOutputTokens);

        try
        {
            var result = await chat.GetChatMessageContentAsync(
                _session.History,
                settings,
                _session.Kernel,
                ct).ConfigureAwait(false);

            sw.Stop();

            var response = result.Content ?? "(no response)";
            if (HasUnpairedFunctionCalls(_session.History, historySnapshot))
            {
                throw new InvalidOperationException(
                    "Each `tool_use` block must have a corresponding `tool_result` block");
            }

            // Detect and execute text-based tool calls from models that emit JSON
            // instead of using the structured tool calling protocol.
            var toolResult = await TryExecuteTextToolCallAsync(response, ct).ConfigureAwait(false);
            var responseAlreadyAddedToHistory = false;
            if (toolResult is not null)
            {
                _session.History.AddAssistantMessage(toolResult.Value.AssistantMessage);
                responseAlreadyAddedToHistory = !toolResult.Value.RequiresFollowUp;

                if (toolResult.Value.RequiresFollowUp)
                {
                    _session.History.AddUserMessage($"[Tool result for {toolResult.Value.FunctionName}]:\n{toolResult.Value.ToolResult}");

                    turnEntry.Attributes["text_tool_call"] = toolResult.Value.FunctionName;

                    // Re-invoke the model with the tool result so it can produce a natural response
                    sw.Restart();
                    var followUp = await chat.GetChatMessageContentAsync(
                        _session.History, settings, _session.Kernel, ct).ConfigureAwait(false);
                    sw.Stop();

                    response = followUp.Content ?? toolResult.Value.ToolResult ?? toolResult.Value.AssistantMessage;
                }
                else
                {
                    response = toolResult.Value.AssistantMessage;
                }
            }

            if (!responseAlreadyAddedToHistory)
            {
                _session.History.AddAssistantMessage(response);
            }

            var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                .EstimateTokens(response);

            // Capture turn index before it is incremented by RecordAssistantTurnAsync
            var turnIdxForLog = _session.TurnIndex;

            await _session.RecordAssistantTurnAsync(
                response, durationMs: sw.ElapsedMilliseconds,
                tokensOut: tokenEstimate).ConfigureAwait(false);

            // Fire-and-forget daily memory log entry (non-blocking)
            AppendTurnToMemoryLog(turnIdxForLog, userMessage, ct);

            turnEntry.Attributes["tokens_out"] = tokenEstimate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            turnEntry.Complete();
            _session.LastTimeline = traceCtx.Timeline;

            turnActivity?.SetTag(AgentInstrumentation.AttrUsageOutputTokens, tokenEstimate);
            turnActivity?.SetStatus(ActivityStatusCode.Ok);
            AgentInstrumentation.TurnCount.Add(1,
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName),
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrRequestModel, _session.CurrentModel?.Id));
            AgentInstrumentation.TurnDuration.Record(sw.ElapsedMilliseconds,
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName));
            AgentInstrumentation.TokensUsed.Add(tokenEstimate,
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName));

            await SaveCapturedWorkflowIfActiveAsync(ct).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            (IsToolCallingError(ex) ||
             IsToolsRejectedError(ex, AgentExecutionSettingsFactory.HasToolsEnabled(settings))))
        {
            sw.Stop();

            // Remove intermediate messages SK added during failed auto-function-calling
            while (_session.History.Count > historySnapshot)
                _session.History.RemoveAt(_session.History.Count - 1);
            PruneUnpairedFunctionCalls(_session.History);

            DebugLogger.Log(DebugCategory.Agents,
                "Tool calling format error, retrying without tools: {0}", ex.Message);

            sw.Restart();
            try
            {
                var retrySettings = AgentExecutionSettingsFactory.Create(
                    _session.CurrentModel,
                    enableTools: false);
                ApplyReasoningEffort(
                    retrySettings,
                    _session.CurrentModel,
                    _session.ReasoningEffortOverride);

                var result = await chat.GetChatMessageContentAsync(
                    _session.History, retrySettings, _session.Kernel, ct).ConfigureAwait(false);
                sw.Stop();

                var response = result.Content ?? "(no response)";
                var retryToolResult = await TryExecuteTextToolCallAsync(response, ct).ConfigureAwait(false);
                var responseAlreadyAddedToHistory = false;
                if (retryToolResult is not null)
                {
                    _session.History.AddAssistantMessage(retryToolResult.Value.AssistantMessage);
                    responseAlreadyAddedToHistory = !retryToolResult.Value.RequiresFollowUp;

                    if (retryToolResult.Value.RequiresFollowUp)
                    {
                        _session.History.AddUserMessage(
                            $"[Tool result for {retryToolResult.Value.FunctionName}]:\n{retryToolResult.Value.ToolResult}");

                        sw.Restart();
                        var followUp = await chat.GetChatMessageContentAsync(
                            _session.History, retrySettings, _session.Kernel, ct).ConfigureAwait(false);
                        sw.Stop();
                        response = followUp.Content ?? retryToolResult.Value.ToolResult ?? retryToolResult.Value.AssistantMessage;
                    }
                    else
                    {
                        response = retryToolResult.Value.AssistantMessage;
                    }
                }

                if (!responseAlreadyAddedToHistory)
                {
                    _session.History.AddAssistantMessage(response);
                }

                var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                    .EstimateTokens(response);

                await _session.RecordAssistantTurnAsync(
                    response, durationMs: sw.ElapsedMilliseconds,
                    tokensOut: tokenEstimate).ConfigureAwait(false);
                await SaveCapturedWorkflowIfActiveAsync(ct).ConfigureAwait(false);

                turnEntry.Attributes["tool_calling_fallback"] = "true";
                turnEntry.Complete();
                _session.LastTimeline = traceCtx.Timeline;

                return response;
            }
            catch (Exception retryEx) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
                if (_session.CapturedWorkflowSteps.Count == 0)
                {
                    AbandonCapturedWorkflowIfActive();
                }
                turnEntry.Complete("error", retryEx.Message);
                _session.LastTimeline = traceCtx.Timeline;
                var errorMsg = $"Error: {retryEx.Message}";
                AgentOutput.Current.RenderError(errorMsg);
                _session.History.AddAssistantMessage(
                    $"[Error occurred: {retryEx.Message}. I'll try a different approach.]");
                return errorMsg;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            AbandonCapturedWorkflowIfActive();
            // Attempt fallback model if available and error is retriable
            if (IsRetriableError(ex) && _session.FallbackModels.Count > 0)
            {
                var fallbackResult = await TryFallbackAsync(userMessage, streaming: false, ct).ConfigureAwait(false);
                if (fallbackResult is not null)
                {
                    turnEntry.Attributes["fallback"] = "true";
                    turnEntry.Complete();
                    _session.LastTimeline = traceCtx.Timeline;
                    return fallbackResult;
                }
            }

            turnEntry.Complete("error", ex.Message);
            _session.LastTimeline = traceCtx.Timeline;

            turnActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            AgentInstrumentation.ProviderErrors.Add(1,
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName),
                new KeyValuePair<string, object?>("error.type", ex.GetType().Name));

            var errorMsg = $"Error: {ex.Message}";
            AgentOutput.Current.RenderError(errorMsg);

            _session.History.AddAssistantMessage(
                $"[Error occurred: {ex.Message}. I'll try a different approach.]");

            return errorMsg;
        }
    }

    /// <summary>
    /// Send a user message with streaming output— tokens appear as they arrive.
    /// Thinking/reasoning content (via &lt;think&gt; tags or metadata) is rendered
    /// as dim gray text, separate from the response content.
    /// </summary>
    public async Task<string> RunTurnStreamingAsync(
        string userMessage, CancellationToken ct = default)
    {
        var traceCtx = TraceContext.StartTurn(_session.SessionInfo?.Id, _session.TurnIndex);
        var turnEntry = traceCtx.Timeline.BeginOperation("agent.turn");
        DebugLogger.Log(DebugCategory.Agents, "turn={0} traceId={1} streaming=true", traceCtx.TurnIndex, traceCtx.TraceId);

        _session.ResetTurnState();
        PruneUnpairedFunctionCalls(_session.History);
        await _session.RecordUserTurnAsync(userMessage).ConfigureAwait(false);
        _session.History.AddUserMessage(userMessage);
        var historySnapshot = _session.History.Count;

        var chat = _session.Kernel.GetRequiredService<IChatCompletionService>();

        var settings = BuildExecutionSettings();
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

        var contentStarted = false;

        using var turnActivity = AgentInstrumentation.AgentSource.StartActivity(
            "agent.turn",
            ActivityKind.Internal);
        turnActivity?.SetTag(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName ?? "unknown");
        turnActivity?.SetTag(AgentInstrumentation.AttrRequestModel, _session.CurrentModel?.Id ?? "unknown");
        turnActivity?.SetTag(AgentInstrumentation.AttrOperationName, "chat");
        if (_session.CurrentModel?.MaxOutputTokens is > 0)
            turnActivity?.SetTag(AgentInstrumentation.AttrRequestMaxTokens, _session.CurrentModel.MaxOutputTokens);

        try
        {
            var fullResponse = new StringBuilder();
            var pendingContent = new StringBuilder();
            var thinkingCapture = new StringBuilder();
            var parser = new StreamingContentParser();
            var thinkingActive = false;
            var suppressTextToolStreaming = false;

            void AppendContentSegment(string text)
            {
                if (thinkingActive)
                {
                    output.EndThinking();
                    thinkingActive = false;
                }

                fullResponse.Append(text);
                pendingContent.Append(text);

                if (ShouldSuppressStreamingText(fullResponse.ToString()))
                {
                    suppressTextToolStreaming = true;
                    pendingContent.Clear();
                    return;
                }

                if (!suppressTextToolStreaming)
                {
                    FlushPendingStreamingContent(output, pendingContent, ref contentStarted, flushAll: false);
                }
            }

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
                            AppendContentSegment(seg.Text);
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
                    AppendContentSegment(seg.Text);
                }
            }

            if (!suppressTextToolStreaming)
            {
                FlushPendingStreamingContent(output, pendingContent, ref contentStarted, flushAll: true);
            }

            if (thinkingActive) output.EndThinking();

            if (HasUnpairedFunctionCalls(_session.History, historySnapshot))
            {
                throw new InvalidOperationException(
                    "Each `tool_use` block must have a corresponding `tool_result` block");
            }

            // If the LLM produced no visible content, render a fallback
            if (!contentStarted && fullResponse.Length == 0)
            {
                output.BeginStreaming();
                output.WriteStreamingChunk("(no response)");
                contentStarted = true;
            }

            if (contentStarted) output.EndStreaming();

            sw.Stop();

            var response = fullResponse.Length > 0
                ? fullResponse.ToString()
                : "(no response)";

            // Detect and execute text-based tool calls from models that emit JSON
            // instead of using the structured tool calling protocol.
            var toolResult = await TryExecuteTextToolCallAsync(response, ct).ConfigureAwait(false);
            var responseAlreadyAddedToHistory = false;
            if (toolResult is not null)
            {
                _session.History.AddAssistantMessage(toolResult.Value.AssistantMessage);
                responseAlreadyAddedToHistory = !toolResult.Value.RequiresFollowUp;

                if (toolResult.Value.RequiresFollowUp)
                {
                    _session.History.AddUserMessage($"[Tool result for {toolResult.Value.FunctionName}]:\n{toolResult.Value.ToolResult}");

                    DebugLogger.Log(DebugCategory.Agents,
                        "Text-based tool call detected: {0}, re-invoking model with result",
                        toolResult.Value.FunctionName);

                    // Re-invoke non-streaming so the model can produce a natural response
                    sw.Restart();
                    var followUp = await chat.GetChatMessageContentAsync(
                        _session.History, settings, _session.Kernel, ct).ConfigureAwait(false);
                    sw.Stop();

                    response = followUp.Content ?? toolResult.Value.ToolResult ?? toolResult.Value.AssistantMessage;

                    // Render the actual response
                    output.BeginStreaming();
                    output.WriteStreamingChunk(response);
                    output.EndStreaming();
                }
                else
                {
                    response = toolResult.Value.AssistantMessage;
                    if (suppressTextToolStreaming)
                    {
                        output.BeginStreaming();
                        output.WriteStreamingChunk(response);
                        output.EndStreaming();
                        contentStarted = true;
                    }
                }
            }
            else if (suppressTextToolStreaming)
            {
                if (ContainsTaggedToolPayload(response) ||
                    IsStandalonePotentialFencedJsonWrapper(response) ||
                    IsStandaloneFencedShellCommandPayload(response))
                {
                    response = "[Text tool call]: Unrecognized or malformed tool payload.";
                }

                output.BeginStreaming();
                output.WriteStreamingChunk(response);
                output.EndStreaming();
                contentStarted = true;
            }

            if (!responseAlreadyAddedToHistory)
            {
                _session.History.AddAssistantMessage(response);
            }

            var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                .EstimateTokens(response);

            var thinkingText = thinkingCapture.Length > 0 ? thinkingCapture.ToString() : null;

            output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, tokenEstimate, totalBytes));

            // Capture turn index before it is incremented by RecordAssistantTurnAsync
            var turnIdxForLog = _session.TurnIndex;

            await _session.RecordAssistantTurnAsync(
                response, thinkingText,
                durationMs: sw.ElapsedMilliseconds,
                tokensOut: tokenEstimate).ConfigureAwait(false);

            // Fire-and-forget daily memory log entry (non-blocking)
            AppendTurnToMemoryLog(turnIdxForLog, userMessage, ct);

            turnEntry.Attributes["tokens_out"] = tokenEstimate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            turnEntry.Complete();
            _session.LastTimeline = traceCtx.Timeline;

            turnActivity?.SetTag(AgentInstrumentation.AttrUsageOutputTokens, tokenEstimate);
            turnActivity?.SetStatus(ActivityStatusCode.Ok);
            AgentInstrumentation.TurnCount.Add(1,
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName),
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrRequestModel, _session.CurrentModel?.Id));
            AgentInstrumentation.TurnDuration.Record(sw.ElapsedMilliseconds,
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName));
            AgentInstrumentation.TokensUsed.Add(tokenEstimate,
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName));

            await SaveCapturedWorkflowIfActiveAsync(ct).ConfigureAwait(false);

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
        catch (Exception ex) when (!ct.IsCancellationRequested && IsStreamingPrematureEnd(ex))
        {
            // Foundry Local (and similar providers) silently terminate SSE
            // connections when the request payload is large (many tools).
            // Fall back to a non-streaming request using the same history.
            output.EndStreaming();
            sw.Stop();
            DebugLogger.Log(DebugCategory.Agents,
                "Streaming terminated prematurely, retrying without streaming");

            sw.Restart();
            try
            {
                var result = await chat.GetChatMessageContentAsync(
                    _session.History, settings, _session.Kernel, ct).ConfigureAwait(false);
                sw.Stop();

                var response = result.Content ?? "(no response)";

                // Check for text-based tool calls before rendering or committing to history
                var fallbackToolResult = await TryExecuteTextToolCallAsync(response, ct).ConfigureAwait(false);
                var responseAlreadyAddedToHistory = false;
                if (fallbackToolResult is not null)
                {
                    _session.History.AddAssistantMessage(fallbackToolResult.Value.AssistantMessage);
                    responseAlreadyAddedToHistory = !fallbackToolResult.Value.RequiresFollowUp;

                    if (fallbackToolResult.Value.RequiresFollowUp)
                    {
                        _session.History.AddUserMessage(
                            $"[Tool result for {fallbackToolResult.Value.FunctionName}]:\n{fallbackToolResult.Value.ToolResult}");

                        sw.Restart();
                        var followUp = await chat.GetChatMessageContentAsync(
                            _session.History, settings, _session.Kernel, ct).ConfigureAwait(false);
                        sw.Stop();
                        response = followUp.Content ?? fallbackToolResult.Value.ToolResult ?? fallbackToolResult.Value.AssistantMessage;
                    }
                    else
                    {
                        response = fallbackToolResult.Value.AssistantMessage;
                    }
                }

                if (!responseAlreadyAddedToHistory)
                {
                    _session.History.AddAssistantMessage(response);
                }

                var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                    .EstimateTokens(response);

                output.BeginStreaming();
                output.WriteStreamingChunk(response);
                output.EndStreaming();
                output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, tokenEstimate, 0));

                await _session.RecordAssistantTurnAsync(
                    response, durationMs: sw.ElapsedMilliseconds,
                    tokensOut: tokenEstimate).ConfigureAwait(false);
                await SaveCapturedWorkflowIfActiveAsync(ct).ConfigureAwait(false);

                turnEntry.Attributes["streaming_fallback"] = "true";
                turnEntry.Complete();
                _session.LastTimeline = traceCtx.Timeline;

                return response;
            }
            catch (Exception fallbackEx) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
                AbandonCapturedWorkflowIfActive();
                turnEntry.Complete("error", fallbackEx.Message);
                _session.LastTimeline = traceCtx.Timeline;
                output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, 0, totalBytes));
                var errorMsg = $"Error: {fallbackEx.Message}";
                AgentOutput.Current.RenderError(errorMsg);

                _session.History.AddAssistantMessage(
                    $"[Error occurred: {fallbackEx.Message}. I'll try a different approach.]");

                return errorMsg;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            (IsToolCallingError(ex) ||
             IsToolsRejectedError(ex, AgentExecutionSettingsFactory.HasToolsEnabled(settings))))
        {
            if (contentStarted) output.EndStreaming();
            sw.Stop();

            // Remove intermediate messages SK added during failed auto-function-calling
            while (_session.History.Count > historySnapshot)
                _session.History.RemoveAt(_session.History.Count - 1);
            PruneUnpairedFunctionCalls(_session.History);

            DebugLogger.Log(DebugCategory.Agents,
                "Tool calling format error, retrying without tools: {0}", ex.Message);

            sw.Restart();
            try
            {
                var retrySettings = AgentExecutionSettingsFactory.Create(
                    _session.CurrentModel,
                    enableTools: false);
                ApplyReasoningEffort(
                    retrySettings,
                    _session.CurrentModel,
                    _session.ReasoningEffortOverride);

                var result = await chat.GetChatMessageContentAsync(
                    _session.History, retrySettings, _session.Kernel, ct).ConfigureAwait(false);
                sw.Stop();

                var response = result.Content ?? "(no response)";

                // Even without structured tool calling, the model may emit JSON tool calls as text
                var retryToolResult = await TryExecuteTextToolCallAsync(response, ct).ConfigureAwait(false);
                var responseAlreadyAddedToHistory = false;
                if (retryToolResult is not null)
                {
                    _session.History.AddAssistantMessage(retryToolResult.Value.AssistantMessage);
                    responseAlreadyAddedToHistory = !retryToolResult.Value.RequiresFollowUp;

                    if (retryToolResult.Value.RequiresFollowUp)
                    {
                        _session.History.AddUserMessage(
                            $"[Tool result for {retryToolResult.Value.FunctionName}]:\n{retryToolResult.Value.ToolResult}");

                        sw.Restart();
                        var followUp = await chat.GetChatMessageContentAsync(
                            _session.History, retrySettings, _session.Kernel, ct).ConfigureAwait(false);
                        sw.Stop();
                        response = followUp.Content ?? retryToolResult.Value.ToolResult ?? retryToolResult.Value.AssistantMessage;
                    }
                    else
                    {
                        response = retryToolResult.Value.AssistantMessage;
                    }
                }

                if (!responseAlreadyAddedToHistory)
                {
                    _session.History.AddAssistantMessage(response);
                }

                var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                    .EstimateTokens(response);

                output.BeginStreaming();
                output.WriteStreamingChunk(response);
                output.EndStreaming();
                output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, tokenEstimate, 0));

                await _session.RecordAssistantTurnAsync(
                    response, durationMs: sw.ElapsedMilliseconds,
                    tokensOut: tokenEstimate).ConfigureAwait(false);
                await SaveCapturedWorkflowIfActiveAsync(ct).ConfigureAwait(false);

                turnEntry.Attributes["tool_calling_fallback"] = "true";
                turnEntry.Complete();
                _session.LastTimeline = traceCtx.Timeline;

                return response;
            }
            catch (Exception retryEx) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
                if (_session.CapturedWorkflowSteps.Count == 0)
                {
                    AbandonCapturedWorkflowIfActive();
                }
                turnEntry.Complete("error", retryEx.Message);
                _session.LastTimeline = traceCtx.Timeline;
                // totalBytes here is from the failed original stream, not the retry (which was
                // non-streaming). Report 0 to avoid mixing metrics from two different operations.
                output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, 0, 0));
                var errorMsg = $"Error: {retryEx.Message}";
                AgentOutput.Current.RenderError(errorMsg);
                _session.History.AddAssistantMessage(
                    $"[Error occurred: {retryEx.Message}. I'll try a different approach.]");
                return errorMsg;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            output.EndStreaming();
            sw.Stop();
            AbandonCapturedWorkflowIfActive();

            // Attempt fallback model if available and error is retriable
            if (IsRetriableError(ex) && _session.FallbackModels.Count > 0)
            {
                output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, 0, totalBytes));
                var fallbackResult = await TryFallbackAsync(userMessage, streaming: true, ct).ConfigureAwait(false);
                if (fallbackResult is not null)
                {
                    turnEntry.Attributes["fallback"] = "true";
                    turnEntry.Complete();
                    _session.LastTimeline = traceCtx.Timeline;
                    return fallbackResult;
                }
            }

            turnEntry.Complete("error", ex.Message);
            _session.LastTimeline = traceCtx.Timeline;
            output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, 0, totalBytes));

            turnActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            AgentInstrumentation.ProviderErrors.Add(1,
                new KeyValuePair<string, object?>(AgentInstrumentation.AttrSystem, _session.CurrentModel?.ProviderName),
                new KeyValuePair<string, object?>("error.type", ex.GetType().Name));

            var errorMsg = $"Error: {ex.Message}";
            AgentOutput.Current.RenderError(errorMsg);

            _session.History.AddAssistantMessage(
                $"[Error occurred: {ex.Message}. I'll try a different approach.]");

            return errorMsg;
        }
    }

    internal static bool IsToolCallingError(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            var msg = current.Message;
            // Anthropic-style tool protocol errors
            if (msg.Contains("tool_use_id", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("tool_result", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasUnpairedFunctionCalls(ChatHistory history, int startIndex)
    {
        if (startIndex < 0 || startIndex >= history.Count)
            return false;

        var pendingIds = new HashSet<string>(StringComparer.Ordinal);
        var seenCallWithoutId = false;

        for (var i = startIndex; i < history.Count; i++)
        {
            var items = history[i].Items;
            if (items is null || items.Count == 0)
                continue;

            foreach (var item in items)
            {
                if (item is FunctionCallContent call)
                {
                    if (!string.IsNullOrWhiteSpace(call.Id))
                    {
                        pendingIds.Add(call.Id);
                    }
                    else
                    {
                        // Without an ID, the corresponding result cannot be linked.
                        seenCallWithoutId = true;
                    }
                }
                else if (item is FunctionResultContent result &&
                         !string.IsNullOrWhiteSpace(result.CallId))
                {
                    pendingIds.Remove(result.CallId);
                }
            }
        }

        return seenCallWithoutId || pendingIds.Count > 0;
    }

    private static void PruneUnpairedFunctionCalls(ChatHistory history)
    {
        if (history.Count == 0)
            return;

        var pendingIds = new HashSet<string>(StringComparer.Ordinal);
        var resolvedIds = new HashSet<string>(StringComparer.Ordinal);
        var callsById = new Dictionary<string, List<(int MessageIndex, int ItemIndex)>>(StringComparer.Ordinal);
        var callsWithoutId = new List<(int MessageIndex, int ItemIndex)>();
        var orphanedResults = new List<(int MessageIndex, int ItemIndex)>();

        for (var i = 0; i < history.Count; i++)
        {
            var items = history[i].Items;
            if (items is null || items.Count == 0)
                continue;

            for (var j = 0; j < items.Count; j++)
            {
                var item = items[j];
                if (item is FunctionCallContent call)
                {
                    if (string.IsNullOrWhiteSpace(call.Id))
                    {
                        callsWithoutId.Add((i, j));
                        continue;
                    }

                    pendingIds.Add(call.Id);
                    if (!callsById.TryGetValue(call.Id, out var calls))
                    {
                        calls = [];
                        callsById[call.Id] = calls;
                    }

                    calls.Add((i, j));
                }
                else if (item is FunctionResultContent result &&
                         !string.IsNullOrWhiteSpace(result.CallId))
                {
                    resolvedIds.Add(result.CallId);
                    orphanedResults.Add((i, j));
                }
            }
        }

        var indexesByMessage = new Dictionary<int, HashSet<int>>();

        foreach (var (messageIndex, itemIndex) in callsWithoutId)
            AddRemovalIndex(indexesByMessage, messageIndex, itemIndex);

        foreach (var id in pendingIds)
        {
            if (resolvedIds.Contains(id))
                continue;

            if (callsById.TryGetValue(id, out var entries))
            {
                foreach (var (messageIndex, itemIndex) in entries)
                    AddRemovalIndex(indexesByMessage, messageIndex, itemIndex);
            }
        }

        foreach (var (messageIndex, itemIndex) in orphanedResults)
        {
            var items = history[messageIndex].Items;
            if (items is not null &&
                itemIndex >= 0 &&
                itemIndex < items.Count &&
                items[itemIndex] is FunctionResultContent result &&
                (string.IsNullOrWhiteSpace(result.CallId) ||
                 !pendingIds.Contains(result.CallId)))
            {
                AddRemovalIndex(indexesByMessage, messageIndex, itemIndex);
            }
        }

        if (indexesByMessage.Count == 0)
            return;

        foreach (var kvp in indexesByMessage.OrderByDescending(k => k.Key))
        {
            var messageIndex = kvp.Key;
            var message = history[messageIndex];
            var items = message.Items;
            if (items is null || items.Count == 0)
                continue;

            foreach (var itemIndex in kvp.Value.OrderByDescending(i => i))
            {
                if (itemIndex >= 0 && itemIndex < items.Count)
                    items.RemoveAt(itemIndex);
            }

            if (items.Count == 0 && string.IsNullOrWhiteSpace(message.Content))
                history.RemoveAt(messageIndex);
        }
    }

    private static void AddRemovalIndex(
        IDictionary<int, HashSet<int>> indexesByMessage,
        int messageIndex,
        int itemIndex)
    {
        if (!indexesByMessage.TryGetValue(messageIndex, out var itemIndexes))
        {
            itemIndexes = [];
            indexesByMessage[messageIndex] = itemIndexes;
        }

        itemIndexes.Add(itemIndex);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="ex"/> looks like a provider
    /// rejection caused by sending the <c>tools</c> array to a model that does not
    /// actually support it at runtime (e.g. OpenRouter free-tier models that claim
    /// tool support in metadata but return 400 when tools are sent).
    /// Only considered when <paramref name="toolsWereEnabled"/> is <see langword="true"/>
    /// so that genuine bad-request errors are not silently swallowed.
    /// </summary>
    internal static bool IsToolsRejectedError(Exception ex, bool toolsWereEnabled)
    {
        if (!toolsWereEnabled)
            return false;

        for (var current = ex; current != null; current = current.InnerException)
        {
            // Prefer the typed StatusCode when available (SK's OpenAI connector sets it).
            if (current is HttpRequestException { StatusCode: System.Net.HttpStatusCode.BadRequest })
                return true;

            // String fallback for providers/wrappers that don't propagate StatusCode.
            // Anchored patterns only — avoid bare "400" which would match unrelated text
            // like "Rate limit: retry after 400 seconds" or "error code 40001".
            var msg = current.Message;
            if (msg.Contains("400 (Bad Request)", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("Status: 400", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("StatusCode: 400", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("invalid_request_error", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Builds provider-appropriate execution settings for the current model.
    /// Uses provider-native tool-call settings (OpenAI-style function choice
    /// for most providers, Mistral tool-call behavior for Mistral connector).
    /// </summary>
    private PromptExecutionSettings BuildExecutionSettings()
    {
        var supportsTools = _session.CurrentModel?.Capabilities
            .HasFlag(ModelCapabilities.ToolCalling) ?? false;

        // Loadout-aware tool scoping: instead of disabling ALL tools when the
        // context window is tight, apply progressively smaller loadouts that keep
        // the most essential tools (always including toolDiscovery so agents can
        // still find and activate tools on demand).
        if (supportsTools)
        {
            var contextWindow = _session.CurrentModel?.ContextWindowTokens ?? 128_000;
            var toolCount = _session.Kernel.Plugins.SelectMany(p => p).Count();
            var estimatedToolTokens = toolCount * 200;

            if (estimatedToolTokens > contextWindow / 2 &&
                _session.LoadoutRegistry is not null)
            {
                var loadoutName = SelectLoadoutForContext(contextWindow);
                ApplyLoadoutScoping(loadoutName);

                // Re-check after scoping
                toolCount = _session.Kernel.Plugins.SelectMany(p => p).Count();
                estimatedToolTokens = toolCount * 200;

                DebugLogger.Log(DebugCategory.Agents,
                    "Applied '{0}' loadout: {1} tools (~{2} tokens) for {3}-token context window",
                    loadoutName, toolCount, estimatedToolTokens, contextWindow);

                // If still too large even after loadout scoping, disable as last resort
                if (estimatedToolTokens > contextWindow / 2)
                {
                    DebugLogger.Log(DebugCategory.Agents,
                        "Disabling tools: even '{0}' loadout ({1} tools, ~{2} tokens) exceeds half of {3}-token context window",
                        loadoutName, toolCount, estimatedToolTokens, contextWindow);
                    supportsTools = false;
                }
            }
            else if (estimatedToolTokens > contextWindow / 2)
            {
                // No loadout registry -- fall back to disabling all tools
                DebugLogger.Log(DebugCategory.Agents,
                    "Disabling tools: {0} tools (~{1} tokens) exceed half of {2}-token context window",
                    toolCount, estimatedToolTokens, contextWindow);
                supportsTools = false;
            }
        }

        var settings = AgentExecutionSettingsFactory.Create(
            _session.CurrentModel,
            supportsTools);
        ApplyReasoningEffort(settings, _session.CurrentModel, _session.ReasoningEffortOverride);
        return settings;
    }

    public static void ApplyReasoningEffort(
        PromptExecutionSettings settings,
        ProviderModelInfo? model,
        ReasoningEffort? effort)
    {
        if (effort is null)
            return;

        var provider = (model?.ProviderName ?? string.Empty).ToLowerInvariant();
        var extensionData = settings.ExtensionData is null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(settings.ExtensionData, StringComparer.OrdinalIgnoreCase);

        if (provider.Contains("anthropic", StringComparison.Ordinal) ||
            provider.Contains("claude", StringComparison.Ordinal))
        {
            extensionData["thinking"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "adaptive",
            };
            extensionData["output_config"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["effort"] = MapAnthropicEffort(effort.Value),
            };
            settings.ExtensionData = extensionData;
            return;
        }

        if (provider.Contains("google", StringComparison.Ordinal) ||
            provider.Contains("gemini", StringComparison.Ordinal))
        {
            extensionData["thinking_config"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["thinking_level"] = MapGeminiEffort(effort.Value),
            };
            settings.ExtensionData = extensionData;
            return;
        }

        extensionData["reasoning_effort"] = MapOpenAiEffort(effort.Value);
        settings.ExtensionData = extensionData;
    }

    private static string MapOpenAiEffort(ReasoningEffort effort) => effort switch
    {
        ReasoningEffort.None => "low",
        ReasoningEffort.Low => "low",
        ReasoningEffort.Medium => "medium",
        ReasoningEffort.High => "high",
        ReasoningEffort.Max => "xhigh",
        _ => "medium",
    };

    private static string MapAnthropicEffort(ReasoningEffort effort) => effort switch
    {
        ReasoningEffort.None => "low",
        ReasoningEffort.Low => "low",
        ReasoningEffort.Medium => "medium",
        ReasoningEffort.High => "high",
        ReasoningEffort.Max => "max",
        _ => "high",
    };

    private static string MapGeminiEffort(ReasoningEffort effort) => effort switch
    {
        ReasoningEffort.None => "minimal",
        ReasoningEffort.Low => "low",
        ReasoningEffort.Medium => "medium",
        ReasoningEffort.High => "high",
        ReasoningEffort.Max => "high",
        _ => "medium",
    };

    /// <summary>
    /// Detects when a streaming connection was terminated prematurely by the server.
    /// This occurs with Foundry Local and similar providers that silently close SSE
    /// connections when the request payload is large (e.g. many tools).
    /// </summary>
    internal static bool IsStreamingPrematureEnd(Exception ex)
    {
        // Walk exception chain looking for HttpIOException or ResponseEnded indicators
        for (var current = ex; current != null; current = current.InnerException)
        {
            var msg = current.Message;
            if (msg.Contains("ResponseEnded", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase))
                return true;

            // .NET's HttpIOException (System.Net.Http) with HttpRequestError.ResponseEnded
            if (string.Equals(current.GetType().Name, "HttpIOException", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether an exception is retriable (429/500/503/timeout)
    /// and therefore eligible for model fallback.
    /// </summary>
    private static bool IsRetriableError(Exception ex)
    {
        if (IsPermanentQuotaError(ex))
            return false;

        if (ex is TaskCanceledException or TimeoutException)
            return true;

        if (ex is HttpRequestException httpEx)
        {
            return httpEx.StatusCode is
                System.Net.HttpStatusCode.InternalServerError or   // 500
                System.Net.HttpStatusCode.TooManyRequests or       // 429
                System.Net.HttpStatusCode.ServiceUnavailable or    // 503
                System.Net.HttpStatusCode.GatewayTimeout;          // 504
        }

        // Check inner exceptions (SK wraps HTTP errors)
        if (ex.InnerException is HttpRequestException inner)
        {
            return inner.StatusCode is
                System.Net.HttpStatusCode.InternalServerError or
                System.Net.HttpStatusCode.TooManyRequests or
                System.Net.HttpStatusCode.ServiceUnavailable or
                System.Net.HttpStatusCode.GatewayTimeout;
        }

        // Check message for common patterns
        var msg = ex.Message;
        return msg.Contains("429", StringComparison.Ordinal) ||
               msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("overloaded", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("model: Field required", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPermanentQuotaError(Exception ex)
    {
        static bool HasQuotaMarker(string? message) =>
            !string.IsNullOrWhiteSpace(message) &&
            (message.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("billing", StringComparison.OrdinalIgnoreCase));

        for (var current = ex; current != null; current = current.InnerException)
        {
            if (HasQuotaMarker(current.Message))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Saves a captured workflow if recording is active and tool calls were captured.
    /// </summary>
    private async Task SaveCapturedWorkflowIfActiveAsync(CancellationToken ct)
    {
        if (!string.Equals(_session.ActiveWorkflowName, "recording", StringComparison.Ordinal))
            return;

        if (_session.CapturedWorkflowSteps.Count == 0)
        {
            _session.ActiveWorkflowName = null;
            return;
        }

        var workflowName = $"captured-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        if (_session.SaveCapturedWorkflowAsync is { } saveCallback)
        {
            try
            {
                var savedName = await saveCallback(workflowName, _session.CapturedWorkflowSteps, ct)
                    .ConfigureAwait(false);

                AgentOutput.Current.RenderInfo(
                    $"📋 Workflow '{savedName}' saved with {_session.CapturedWorkflowSteps.Count} steps.\n" +
                    $"   Use '/workflow show {savedName}' to view, '/workflow refine {savedName} <feedback>' to improve.");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(DebugCategory.Agents,
                    "Failed to save captured workflow: {0}", ex.Message);
                AgentOutput.Current.RenderWarning($"⚠ Failed to save captured workflow: {ex.Message}");
                return;
            }
        }

        _session.CapturedWorkflowSteps.Clear();
        _session.ActiveWorkflowName = null;
    }

    private void AbandonCapturedWorkflowIfActive()
    {
        if (!string.Equals(_session.ActiveWorkflowName, "recording", StringComparison.Ordinal))
            return;

        _session.CapturedWorkflowSteps.Clear();
        _session.ActiveWorkflowName = null;
    }

    /// <summary>
    /// Attempts to switch to a fallback model and retry the turn.
    /// Returns null if all fallbacks fail.
    /// </summary>
    private async Task<string?> TryFallbackAsync(
        string userMessage, bool streaming, CancellationToken ct)
    {
        var output = AgentOutput.Current;

        foreach (var fallbackModel in _session.FallbackModels)
        {
            output.RenderWarning(
                $"Primary model unavailable, falling back to {fallbackModel}...");

            DebugLogger.Log(DebugCategory.Providers,
                "Attempting fallback to model: {0}", fallbackModel);

            try
            {
                // Try to switch model via the session's registry
                var switched = await _session.TrySwitchModelAsync(fallbackModel, ct)
                    .ConfigureAwait(false);

                if (!switched)
                {
                    output.RenderWarning($"  Fallback model '{fallbackModel}' not available.");
                    continue;
                }

                // Remove the user message we already added (it'll be re-added by the recursive call)
                if (_session.History.Count > 0 &&
                    _session.History[^1].Role == AuthorRole.User)
                {
                    _session.History.RemoveAt(_session.History.Count - 1);
                }

                // Retry with the new model
                return streaming
                    ? await RunTurnStreamingAsync(userMessage, ct).ConfigureAwait(false)
                    : await RunTurnAsync(userMessage, ct).ConfigureAwait(false);
            }
            catch (Exception fallbackEx)
            {
                output.RenderWarning(
                    $"  Fallback to {fallbackModel} also failed: {fallbackEx.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Selects the appropriate tool loadout based on the available context window size.
    /// </summary>
    internal static string SelectLoadoutForContext(int contextWindowTokens)
    {
        return contextWindowTokens switch
        {
            >= 128_000 => WellKnownLoadouts.Full,
            >= 64_000 => WellKnownLoadouts.Developer,
            _ => WellKnownLoadouts.Minimal,
        };
    }

    /// <summary>
    /// Removes plugins from the kernel that are not in the active set for the given loadout.
    /// Always preserves the <c>toolDiscovery</c> plugin so agents can still find and
    /// activate additional tools on demand.
    /// </summary>
    private void ApplyLoadoutScoping(string loadoutName)
    {
        var registry = _session.LoadoutRegistry!;
        var activePlugins = registry.ResolveActivePlugins(
            loadoutName, _session.Kernel.Plugins);

        var toRemove = _session.Kernel.Plugins
            .Where(p => !activePlugins.Contains(p.Name) &&
                        !p.Name.Equals("toolDiscovery", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        foreach (var name in toRemove)
        {
            _session.Kernel.Plugins.Remove(
                _session.Kernel.Plugins.First(p =>
                    p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// Result of manually executing a tool call that was emitted as plain text JSON.
    /// </summary>
    private readonly record struct TextToolCallResult(
        string FunctionName,
        string AssistantMessage,
        string? ToolResult,
        bool RequiresFollowUp);

    internal readonly record struct TextToolSafetyResult(
        bool Allowed,
        string Status,
        string Message);

    /// <summary>
    /// Detects when a model emits a tool/function call as plain text JSON instead of
    /// using the structured tool calling protocol. Common with smaller models (1-8B).
    /// If detected, resolves and invokes the kernel function, returning the result.
    /// Handles single bare JSON, code-fenced JSON, and responses with prose around the JSON.
    /// </summary>
    private async Task<TextToolCallResult?> TryExecuteTextToolCallAsync(
        string response, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        if (AgentOutput.Current.IsJsonOutputMode)
            return null;

        // Tool-capable models should use SK's structured tool-calling channel.
        // However, some providers/models occasionally emit orphan tool JSON as
        // plain text. Allow a strict fallback only for standalone payloads.
        if (ShouldEnforceStructuredToolChannel() &&
            !IsStandaloneToolCallPayload(response) &&
            !ContainsTaggedToolPayload(response) &&
            !ContainsPotentialFencedJsonWrapper(response) &&
            !IsStandaloneFencedShellCommandPayload(response))
            return null;

        var hasTaggedWrapper = ContainsPotentialTaggedToolPayload(response);
        var hasFencedJsonWrapper = ContainsPotentialFencedJsonWrapper(response);
        var hasStandaloneFencedJsonWrapper = IsStandalonePotentialFencedJsonWrapper(response);
        var hasStandaloneFencedShellWrapper = IsStandaloneFencedShellCommandPayload(response);
        string? handledToolName = null;
        string? handledCanonicalName = null;
        string? handledArgsSummary = null;
        string? handledRedactedArgsSummary = null;
        var recognizedPayload = false;

        try
        {
            var args = new KernelArguments();
            var fullName = string.Empty;

            var jsonText = ExtractFirstToolCallJson(response);
            if (jsonText is not null)
            {
                recognizedPayload = true;
                using var doc = JsonDocument.Parse(jsonText);
                if (!TrySelectToolCallElement(doc.RootElement, out var toolCall))
                {
                    return new TextToolCallResult(
                        "tool",
                        BuildTextToolAssistantMessage("tool", string.Empty, "Malformed tool payload."),
                        ToolResult: null,
                        RequiresFollowUp: false);
                }

                if (!toolCall.TryGetProperty("name", out var nameEl) ||
                    nameEl.ValueKind != JsonValueKind.String)
                {
                    return new TextToolCallResult(
                        "tool",
                        BuildTextToolAssistantMessage("tool", string.Empty, "Malformed tool payload."),
                        ToolResult: null,
                        RequiresFollowUp: false);
                }

                fullName = nameEl.GetString() ?? string.Empty;
                if (string.IsNullOrEmpty(fullName))
                {
                    return new TextToolCallResult(
                        "tool",
                        BuildTextToolAssistantMessage("tool", string.Empty, "Malformed tool payload."),
                        ToolResult: null,
                        RequiresFollowUp: false);
                }

                // Build arguments from the "arguments" property
                if (TryGetToolCallArguments(toolCall, out var argsEl) &&
                    argsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in argsEl.EnumerateObject())
                    {
                        args[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString()
                            : prop.Value.GetRawText();
                    }
                }
            }
            else
            {
                var command = ExtractFirstFencedShellCommand(response);
                if (string.IsNullOrWhiteSpace(command))
                {
                    if (hasTaggedWrapper || hasStandaloneFencedJsonWrapper || hasStandaloneFencedShellWrapper)
                    {
                        return new TextToolCallResult(
                            hasStandaloneFencedShellWrapper ? "bash" : "tool",
                            BuildTextToolAssistantMessage(
                                hasStandaloneFencedShellWrapper ? "bash" : "tool",
                                string.Empty,
                                "Malformed tool payload."),
                            ToolResult: null,
                            RequiresFollowUp: false);
                    }

                    return null;
                }

                if (!hasStandaloneFencedShellWrapper)
                    return null;

                // Claude/Copilot-style fenced shell command fallback:
                // route through canonical run_command alias + safety checks.
                recognizedPayload = true;
                fullName = "bash";
                args["command"] = command;
            }

            // Parse "pluginName-functionName", "pluginName.functionName",
            // "pluginName/functionName", or just "functionName".
            string? pluginName = null;
            var normalizedName = fullName.Trim();
            string functionName = normalizedName;
            var separatorIndex = normalizedName.IndexOfAny(['-', '.', '/']);
            if (separatorIndex > 0)
            {
                pluginName = normalizedName[..separatorIndex];
                functionName = normalizedName[(separatorIndex + 1)..];
            }
            functionName = OpenClawToolAliasResolver.Resolve(functionName);
            handledToolName = fullName;
            handledCanonicalName = functionName;

            // Resolve the kernel function
            KernelFunction? func = null;
            if (pluginName is not null)
            {
                _session.Kernel.Plugins
                    .FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                    ?.TryGetFunction(functionName, out func);
            }

            // Fallback: search all plugins
            func ??= _session.Kernel.Plugins
                .SelectMany(p => p)
                .FirstOrDefault(f => f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));

            DebugLogger.Log(DebugCategory.Agents,
                "Intercepted text-based tool call: {0} with {1} args", fullName, args.Count);

            var argsSummary = ToolConfirmationFilter.BuildDisplayArgs(functionName, args);
            var persistedArgsSummary = ToolConfirmationFilter.BuildPersistedArgs(functionName, args);
            var redactedArgsSummary = ToolConfirmationFilter.BuildRedactedArgs(functionName, args);
            var argsFingerprint = ToolConfirmationFilter.BuildArgsFingerprint(functionName, args);
            handledArgsSummary = persistedArgsSummary;
            handledRedactedArgsSummary = redactedArgsSummary;

            if (func is null)
            {
                _session.RecordToolCall(functionName, redactedArgsSummary, "Tool unavailable.", "error", 0);
                return new TextToolCallResult(
                    fullName,
                    BuildTextToolAssistantMessage(fullName, persistedArgsSummary, "Tool unavailable."),
                    ToolResult: null,
                    RequiresFollowUp: false);
            }

            var safety = CheckTextToolCallSafety(functionName, argsSummary);
            if (!safety.Allowed)
            {
                DebugLogger.Log(DebugCategory.Agents,
                    "Text-based tool call blocked by safety gate: {0}", fullName);
                _session.RecordToolCall(functionName, redactedArgsSummary, safety.Message, safety.Status, 0);
                return new TextToolCallResult(
                    fullName,
                    BuildTextToolAssistantMessage(fullName, persistedArgsSummary, safety.Message),
                    ToolResult: null,
                    RequiresFollowUp: false);
            }

            if (!_session.TryRegisterToolCallForCurrentTurn(functionName, argsFingerprint))
            {
                DebugLogger.Log(DebugCategory.Agents,
                    "Skipping duplicate text-based tool call in current turn: {0}", fullName);
                return new TextToolCallResult(
                    fullName,
                    BuildTextToolAssistantMessage(fullName, persistedArgsSummary, "Skipped duplicate tool call in current turn."),
                    ToolResult: null,
                    RequiresFollowUp: false);
            }

            var toolStopwatch = Stopwatch.StartNew();
            var result = await func.InvokeAsync(_session.Kernel, args, ct).ConfigureAwait(false);
            toolStopwatch.Stop();
            var resultStr = result?.ToString() ?? "(no output)";

            AgentOutput.Current.RenderToolCall(fullName!, argsSummary, resultStr);
            _session.RecordToolCall(functionName, redactedArgsSummary, resultStr, "ok", toolStopwatch.ElapsedMilliseconds);
            if (string.Equals(_session.ActiveWorkflowName, "recording", StringComparison.Ordinal))
            {
                _session.CapturedWorkflowSteps.Add((functionName, persistedArgsSummary));
            }

            return new TextToolCallResult(
                fullName,
                BuildTextToolAssistantMessage(fullName, persistedArgsSummary),
                resultStr,
                RequiresFollowUp: true);
        }
        catch (JsonException)
        {
            if (!recognizedPayload)
                return null;

            var toolName = handledToolName ?? "tool";
            var argsSummary = handledArgsSummary ?? string.Empty;
            return new TextToolCallResult(
                toolName,
                BuildTextToolAssistantMessage(toolName, argsSummary, "Malformed tool payload."),
                ToolResult: null,
                RequiresFollowUp: false);
        }
        catch (Exception ex)
        {
            DebugLogger.Log(DebugCategory.Agents,
                "Text-based tool call execution failed: {0}", ex.Message);
            if (handledCanonicalName is null)
                return null;

            _session.RecordToolCall(handledCanonicalName, handledRedactedArgsSummary, ex.Message, "error", 0);
            return new TextToolCallResult(
                handledToolName ?? handledCanonicalName,
                BuildTextToolAssistantMessage(
                    handledToolName ?? handledCanonicalName,
                    handledArgsSummary ?? string.Empty,
                    "Tool execution failed."),
                ToolResult: null,
                RequiresFollowUp: false);
        }
    }

    private bool ShouldEnforceStructuredToolChannel() =>
        _session.CurrentModel?.Capabilities.HasFlag(ModelCapabilities.ToolCalling) ?? false;

    private const int StreamingToolLookbehindChars = 32;

    private static void FlushPendingStreamingContent(
        IAgentOutput output,
        StringBuilder pendingContent,
        ref bool contentStarted,
        bool flushAll)
    {
        var charsToFlush = flushAll
            ? pendingContent.Length
            : Math.Max(0, pendingContent.Length - StreamingToolLookbehindChars);

        if (charsToFlush == 0)
            return;

        if (!contentStarted)
        {
            output.BeginStreaming();
            contentStarted = true;
        }

        var text = pendingContent.ToString(0, charsToFlush);
        output.WriteStreamingChunk(text);
        pendingContent.Remove(0, charsToFlush);
    }

    private static bool ShouldSuppressStreamingText(string responseSoFar)
    {
        if (string.IsNullOrWhiteSpace(responseSoFar))
            return false;

        if (ContainsPotentialTaggedToolPayload(responseSoFar) ||
            ContainsPotentialFencedJsonWrapper(responseSoFar) ||
            ContainsPotentialFencedJsonStreamingPayload(responseSoFar) ||
            ContainsPotentialFencedShellPayload(responseSoFar))
            return true;

        var trimmed = responseSoFar.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    // Compiled regex for fenced JSON blocks (e.g. ```json\n{...}\n```)
    private static readonly Regex FencedJsonRegex = new(
        @"```(?:json)?\s*\r?\n(?<json>(?:\{[\s\S]*?\}|\[[\s\S]*?\]))\s*\r?\n```",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Small models sometimes emit XML-like wrappers:
    // <tool_call>{...}</tool_call>
    private static readonly Regex TaggedToolCallRegex = new(
        @"<tool_call>\s*(?<json>(?:\{[\s\S]*?\}|\[[\s\S]*?\]))\s*</tool_call>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Anthropic/Claude models may emit <tool_use> wrappers with JSON payloads.
    private static readonly Regex TaggedToolUseRegex = new(
        @"<tool_use(?:\s+[^>]*)?>\s*(?<json>(?:\{[\s\S]*?\}|\[[\s\S]*?\]))\s*</tool_use>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Claude/Copilot-style shell tool blocks:
    // ```bash
    // ls
    // ```
    private static readonly Regex FencedShellCommandRegex = new(
        @"```(?:bash|sh|shell|zsh|pwsh|powershell|ps1|cmd|bat)\s*\r?\n(?<command>[\s\S]*?)\r?\n```",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EntireFencedJsonRegex = new(
        @"\A```(?:json)?\s*\r?\n(?<json>(?:\{[\s\S]*?\}|\[[\s\S]*?\]))\s*\r?\n```\z",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EntireFencedShellCommandRegex = new(
        @"\A```(?:bash|sh|shell|zsh|pwsh|powershell|ps1|cmd|bat)\s*\r?\n(?<command>[\s\S]*?)\r?\n```\z",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EntireTaggedToolCallRegex = new(
        @"\A<tool_call>\s*(?<json>(?:\{[\s\S]*?\}|\[[\s\S]*?\]))\s*</tool_call>\z",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EntireTaggedToolUseRegex = new(
        @"\A<tool_use(?:\s+[^>]*)?>\s*(?<json>(?:\{[\s\S]*?\}|\[[\s\S]*?\]))\s*</tool_use>\z",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts the first JSON object that looks like a tool call (has "name" + args payload)
    /// from a response that may contain prose, code fences, or multiple JSON blocks.
    /// </summary>
    internal static string? ExtractFirstToolCallJson(string response)
    {
        var text = response.Trim();

        // Strategy 1: Whole response is bare JSON object or array
        if ((text.StartsWith('{') && text.EndsWith('}')) ||
            (text.StartsWith('[') && text.EndsWith(']')))
        {
            if (LooksLikeToolCall(text))
                return text;
        }

        // Strategy 2: Anthropic-style tagged blocks (<tool_use> ... </tool_use>)
        foreach (Match m in TaggedToolUseRegex.Matches(text))
        {
            var candidate = m.Groups["json"].Value.Trim();
            if (LooksLikeToolCall(candidate))
                return candidate;
        }

        // Strategy 3: Tagged tool-call blocks (<tool_call> ... </tool_call>)
        foreach (Match m in TaggedToolCallRegex.Matches(text))
        {
            var candidate = m.Groups["json"].Value.Trim();
            if (LooksLikeToolCall(candidate))
                return candidate;
        }

        // Strategy 4: Fenced code blocks (```json ... ```)
        foreach (Match m in FencedJsonRegex.Matches(text))
        {
            var candidate = m.Groups["json"].Value.Trim();
            if (LooksLikeToolCall(candidate))
                return candidate;
        }

        // Strategy 5: Scan for first { ... } block with balanced braces
        var pos = 0;
        while (pos < text.Length)
        {
            var start = text.IndexOf('{', pos);
            if (start < 0) break;

            var depth = 0;
            var inString = false;
            var escape = false;

            for (var i = start; i < text.Length; i++)
            {
                var ch = text[i];
                if (escape) { escape = false; continue; }
                if (ch == '\\' && inString) { escape = true; continue; }
                if (ch == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (ch == '{') depth++;
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        var candidate = text[start..(i + 1)];
                        if (LooksLikeToolCall(candidate))
                            return candidate;
                        break;
                    }
                }
            }

            pos = start + 1;
        }

        return null;
    }

    /// <summary>
    /// Quick heuristic: returns true if the JSON text contains both "name" and a
    /// recognized argument payload key at the top level ("arguments", "parameters", "input"),
    /// suggesting it's a tool call rather than arbitrary data.
    /// </summary>
    private static bool LooksLikeToolCall(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
                return LooksLikeToolCallObject(root);

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object &&
                        LooksLikeToolCallObject(item))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikeToolCallObject(JsonElement root) =>
        root.TryGetProperty("name", out var n) &&
        n.ValueKind == JsonValueKind.String &&
        TryGetToolCallArguments(root, out _);

    private static bool TrySelectToolCallElement(
        JsonElement root, out JsonElement toolCallElement)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            LooksLikeToolCallObject(root))
        {
            toolCallElement = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    LooksLikeToolCallObject(item))
                {
                    toolCallElement = item;
                    return true;
                }
            }
        }

        toolCallElement = default;
        return false;
    }

    internal static bool IsStandaloneToolCallPayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var text = response.Trim();
        if ((text.StartsWith('{') && text.EndsWith('}')) ||
            (text.StartsWith('[') && text.EndsWith(']')))
        {
            return LooksLikeToolCall(text);
        }

        var taggedMatch = EntireTaggedToolCallRegex.Match(text);
        if (taggedMatch.Success)
            return LooksLikeToolCall(taggedMatch.Groups["json"].Value.Trim());

        var taggedToolUseMatch = EntireTaggedToolUseRegex.Match(text);
        if (taggedToolUseMatch.Success)
            return LooksLikeToolCall(taggedToolUseMatch.Groups["json"].Value.Trim());

        var fencedMatch = EntireFencedJsonRegex.Match(text);
        if (fencedMatch.Success)
            return LooksLikeToolCall(fencedMatch.Groups["json"].Value.Trim());

        return false;
    }

    private static bool IsStandaloneFencedShellCommandPayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var match = EntireFencedShellCommandRegex.Match(response.Trim());
        if (!match.Success)
            return false;

        var command = match.Groups["command"].Value.Trim();
        return !string.IsNullOrWhiteSpace(command) &&
               !command.Contains("```", StringComparison.Ordinal);
    }

    private static bool IsStandalonePotentialFencedJsonWrapper(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var text = response.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal) ||
            !text.EndsWith("```", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsPotentialFencedJsonWrapper(text);
    }

    private static bool ContainsTaggedToolUsePayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        foreach (Match m in TaggedToolUseRegex.Matches(response))
        {
            var candidate = m.Groups["json"].Value.Trim();
            if (LooksLikeToolCall(candidate))
                return true;
        }

        return false;
    }

    private static bool ContainsTaggedToolCallPayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        foreach (Match m in TaggedToolCallRegex.Matches(response))
        {
            var candidate = m.Groups["json"].Value.Trim();
            if (LooksLikeToolCall(candidate))
                return true;
        }

        return false;
    }

    private static bool ContainsTaggedToolPayload(string response) =>
        ContainsTaggedToolUsePayload(response) || ContainsTaggedToolCallPayload(response);

    private static bool ContainsPotentialTaggedToolPayload(string response) =>
        response.Contains("<tool", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPotentialFencedJsonWrapper(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        foreach (var candidate in EnumerateFencedJsonCandidates(response))
        {
            if (LooksLikePotentialToolWrapper(candidate))
                return true;
        }

        return false;
    }

    private static bool ContainsPotentialFencedJsonStreamingPayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        if (response.Contains("```json", StringComparison.OrdinalIgnoreCase))
            return true;

        var lastFenceStart = response.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFenceStart < 0)
            return false;

        var afterFence = response[(lastFenceStart + 3)..];
        if (string.IsNullOrWhiteSpace(afterFence))
            return true;

        if (afterFence.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            return true;

        var trimmed = afterFence.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static bool ContainsFencedShellCommandPayload(string response) =>
        ExtractFirstFencedShellCommand(response) is not null;

    private static IEnumerable<string> EnumerateFencedJsonCandidates(string response)
    {
        foreach (Match match in FencedJsonRegex.Matches(response))
        {
            var candidate = match.Groups["json"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
                yield return candidate;
        }

        var searchIndex = 0;
        while (searchIndex < response.Length)
        {
            var fenceStart = response.IndexOf("```", searchIndex, StringComparison.Ordinal);
            if (fenceStart < 0)
                yield break;

            var candidateStart = fenceStart + 3;
            if (candidateStart >= response.Length)
                yield break;

            var remainder = response[candidateStart..];
            var trimmedRemainder = remainder.TrimStart();
            candidateStart += remainder.Length - trimmedRemainder.Length;

            if (trimmedRemainder.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                candidateStart += 4;
                if (candidateStart >= response.Length)
                    yield break;

                remainder = response[candidateStart..];
                trimmedRemainder = remainder.TrimStart();
                candidateStart += remainder.Length - trimmedRemainder.Length;
            }

            if (candidateStart >= response.Length ||
                (response[candidateStart] != '{' && response[candidateStart] != '['))
            {
                searchIndex = fenceStart + 3;
                continue;
            }

            var fenceEnd = response.IndexOf("```", candidateStart, StringComparison.Ordinal);
            if (fenceEnd < 0)
            {
                yield return response[candidateStart..].Trim();
                yield break;
            }

            yield return response[candidateStart..fenceEnd].Trim();
            searchIndex = fenceEnd + 3;
        }
    }

    private static bool LooksLikePotentialToolWrapper(string jsonLike)
    {
        if (string.IsNullOrWhiteSpace(jsonLike))
            return false;

        return jsonLike.Contains("\"name\"", StringComparison.OrdinalIgnoreCase) ||
               jsonLike.Contains("\"arguments\"", StringComparison.OrdinalIgnoreCase) ||
               jsonLike.Contains("\"parameters\"", StringComparison.OrdinalIgnoreCase) ||
               jsonLike.Contains("\"input\"", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsPotentialFencedShellPayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        return response.Contains("```bash", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("```sh", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("```shell", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("```zsh", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("```pwsh", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("```powershell", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("```ps1", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("```cmd", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("```bat", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ExtractFirstFencedShellCommand(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        foreach (Match match in FencedShellCommandRegex.Matches(response))
        {
            var command = match.Groups["command"].Value.Trim();
            if (string.IsNullOrWhiteSpace(command))
                continue;

            if (command.StartsWith("$ ", StringComparison.Ordinal))
                command = command[2..].TrimStart();
            else if (command.StartsWith("> ", StringComparison.Ordinal))
                command = command[2..].TrimStart();

            if (command.Length > 2_000)
                continue;

            if (command.Contains("```", StringComparison.Ordinal))
                continue;

            return command;
        }

        return null;
    }

    private static bool TryGetToolCallArguments(
        JsonElement root, out JsonElement argumentsElement)
    {
        if (root.TryGetProperty("arguments", out var arguments) &&
            arguments.ValueKind == JsonValueKind.Object)
        {
            argumentsElement = arguments;
            return true;
        }

        if (root.TryGetProperty("parameters", out var parameters) &&
            parameters.ValueKind == JsonValueKind.Object)
        {
            argumentsElement = parameters;
            return true;
        }

        if (root.TryGetProperty("input", out var input) &&
            input.ValueKind == JsonValueKind.Object)
        {
            argumentsElement = input;
            return true;
        }

        argumentsElement = default;
        return false;
    }

    // ── Text-based tool call safety ──────────────────────────────────────

    /// <summary>
    /// Mirrors ToolConfirmationFilter's safety tier / permission mode logic
    /// for text-based tool calls that bypass the SK auto-function-invocation pipeline.
    /// Delegates to <see cref="EvaluateTextToolCallSafety"/> for testability.
    /// </summary>
    private static string BuildTextToolAssistantMessage(string toolName, string argsSummary, string? outcome = null)
    {
        var invocation = string.IsNullOrWhiteSpace(argsSummary)
            ? toolName
            : $"{toolName}({argsSummary})";

        return string.IsNullOrWhiteSpace(outcome)
            ? $"[Text tool call]: {invocation}"
            : $"[Text tool call]: {invocation} — {outcome}";
    }

    private TextToolSafetyResult CheckTextToolCallSafety(string functionName, string argsSummary)
    {
        var canonicalName = OpenClawToolAliasResolver.Resolve(functionName);
        return EvaluateTextToolCallSafety(
            canonicalName, argsSummary,
            _session.PermissionMode, _session.SkipPermissions, _session.AutoRunEnabled,
            _session.ToolSafetyTiers, _session.ToolPermissionProfile, _session.ConfirmedOnceTools, AgentOutput.Current,
            _session.EventBus, _session.SessionInfo?.Id);
    }

    /// <summary>
    /// Pure-logic safety evaluation for text-based tool calls.
    /// Returns a structured decision so callers can persist accurate denial reasons.
    /// </summary>
    internal static TextToolSafetyResult EvaluateTextToolCallSafety(
        string canonicalName, string argsSummary,
        PermissionMode permissionMode, bool skipPermissions, bool autoRunEnabled,
        IReadOnlyDictionary<string, Tools.SafetyTier>? tierMap,
        ToolPermissionProfile? permissionProfile,
        HashSet<string> confirmedOnce, IAgentOutput output,
        IEventBus? eventBus = null,
        string? sessionId = null)
    {
        var effectivePermissionMode = (skipPermissions || autoRunEnabled)
            ? PermissionMode.BypassAll
            : permissionMode;
        var tier = tierMap?.GetValueOrDefault(canonicalName, Tools.SafetyTier.AlwaysConfirm)
                   ?? Tools.SafetyTier.AlwaysConfirm;
        var gate = ToolExecutionPermissionEvaluator.Evaluate(
            canonicalName,
            effectivePermissionMode,
            tier,
            permissionProfile);

        if (gate.Decision == ToolExecutionGateDecision.Blocked)
        {
            ToolExecutionPermissionEvaluator.PublishAuditDecision(
                canonicalName,
                ToolExecutionGateDecision.Blocked,
                eventBus,
                sessionId);
            output.RenderWarning($"  \u2717 {canonicalName} blocked ({gate.Reason})");
            return new TextToolSafetyResult(
                Allowed: false,
                Status: "denied",
                Message: $"Tool blocked: {gate.Reason}.");
        }

        if (gate.Decision == ToolExecutionGateDecision.AllowWithoutPrompt)
        {
            ToolExecutionPermissionEvaluator.PublishAuditDecision(
                canonicalName,
                ToolExecutionGateDecision.AllowWithoutPrompt,
                eventBus,
                sessionId);
            output.RenderInfo($"  \u25b8 [text-tool] {canonicalName}({argsSummary})");
            return new TextToolSafetyResult(
                Allowed: true,
                Status: "ok",
                Message: "Allowed without prompt.");
        }

        _ = skipPermissions;
        _ = autoRunEnabled;

        if (tier == Tools.SafetyTier.ConfirmOnce && confirmedOnce.Contains(canonicalName))
        {
            ToolExecutionPermissionEvaluator.PublishAuditDecision(
                canonicalName,
                ToolExecutionGateDecision.AllowWithoutPrompt,
                eventBus,
                sessionId);
            output.RenderInfo($"  \u25b8 [text-tool] {canonicalName}({argsSummary})");
            return new TextToolSafetyResult(
                Allowed: true,
                Status: "ok",
                Message: "Allowed by prior confirmation.");
        }

        ToolExecutionPermissionEvaluator.PublishAuditDecision(
            canonicalName,
            ToolExecutionGateDecision.RequirePrompt,
            eventBus,
            sessionId);
        var approved = output.ConfirmToolCall(canonicalName, argsSummary);
        if (approved && tier == Tools.SafetyTier.ConfirmOnce)
        {
            confirmedOnce.Add(canonicalName);
        }

        return approved
            ? new TextToolSafetyResult(
                Allowed: true,
                Status: "ok",
                Message: "Allowed by user confirmation.")
            : new TextToolSafetyResult(
                Allowed: false,
                Status: "denied",
                Message: "User denied tool execution.");
    }

    /// <summary>
    /// Fire-and-forget append of a turn summary to the daily memory log.
    /// </summary>
    private void AppendTurnToMemoryLog(int turnIndex, string userMessage, CancellationToken ct)
    {
        var sessionId = _session.SessionInfo?.Id ?? "default";
        var msgPreview = userMessage.Length > 50
            ? userMessage[..50] + "…"
            : userMessage;
        var summary = $"Turn {turnIndex}: {msgPreview}";

        _ = _session.MemoryService?.AppendToDailyLogAsync(sessionId, summary, ct);
    }
}
