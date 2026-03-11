using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly HashSet<string> _textToolConfirmedOnce = new(StringComparer.Ordinal);

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
            if (toolResult is not null)
            {
                _session.History.AddAssistantMessage(response);
                _session.History.AddUserMessage($"[Tool result for {toolResult.Value.FunctionName}]:\n{toolResult.Value.Result}");

                AgentOutput.Current.RenderToolCall(toolResult.Value.FunctionName, null, toolResult.Value.Result);
                turnEntry.Attributes["text_tool_call"] = toolResult.Value.FunctionName;

                // Re-invoke the model with the tool result so it can produce a natural response
                sw.Restart();
                var followUp = await chat.GetChatMessageContentAsync(
                    _session.History, settings, _session.Kernel, ct).ConfigureAwait(false);
                sw.Stop();

                response = followUp.Content ?? toolResult.Value.Result;
            }

            _session.History.AddAssistantMessage(response);

            var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                .EstimateTokens(response);

            await _session.RecordAssistantTurnAsync(
                response, durationMs: sw.ElapsedMilliseconds,
                tokensOut: tokenEstimate).ConfigureAwait(false);

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
                _session.History.AddAssistantMessage(response);

                var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                    .EstimateTokens(response);

                await _session.RecordAssistantTurnAsync(
                    response, durationMs: sw.ElapsedMilliseconds,
                    tokensOut: tokenEstimate).ConfigureAwait(false);

                turnEntry.Attributes["tool_calling_fallback"] = "true";
                turnEntry.Complete();
                _session.LastTimeline = traceCtx.Timeline;

                return response;
            }
            catch (Exception retryEx) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
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
            var thinkingCapture = new StringBuilder();
            var parser = new StreamingContentParser();
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

            if (HasUnpairedFunctionCalls(_session.History, historySnapshot))
            {
                throw new InvalidOperationException(
                    "Each `tool_use` block must have a corresponding `tool_result` block");
            }

            // If the LLM produced no visible content, render a fallback
            if (!contentStarted)
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
            if (toolResult is not null)
            {
                _session.History.AddAssistantMessage(response);
                _session.History.AddUserMessage($"[Tool result for {toolResult.Value.FunctionName}]:\n{toolResult.Value.Result}");

                output.RenderToolCall(toolResult.Value.FunctionName, null, toolResult.Value.Result);

                DebugLogger.Log(DebugCategory.Agents,
                    "Text-based tool call detected: {0}, re-invoking model with result",
                    toolResult.Value.FunctionName);

                // Re-invoke non-streaming so the model can produce a natural response
                sw.Restart();
                var followUp = await chat.GetChatMessageContentAsync(
                    _session.History, settings, _session.Kernel, ct).ConfigureAwait(false);
                sw.Stop();

                response = followUp.Content ?? toolResult.Value.Result;

                // Render the actual response
                output.BeginStreaming();
                output.WriteStreamingChunk(response);
                output.EndStreaming();
            }

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
                if (fallbackToolResult is not null)
                {
                    output.RenderToolCall(fallbackToolResult.Value.FunctionName, null, fallbackToolResult.Value.Result);
                    _session.History.AddAssistantMessage(response);
                    _session.History.AddUserMessage(
                        $"[Tool result for {fallbackToolResult.Value.FunctionName}]:\n{fallbackToolResult.Value.Result}");

                    sw.Restart();
                    var followUp = await chat.GetChatMessageContentAsync(
                        _session.History, settings, _session.Kernel, ct).ConfigureAwait(false);
                    sw.Stop();
                    response = followUp.Content ?? fallbackToolResult.Value.Result;
                }

                _session.History.AddAssistantMessage(response);

                var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                    .EstimateTokens(response);

                output.BeginStreaming();
                output.WriteStreamingChunk(response);
                output.EndStreaming();
                output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, tokenEstimate, 0));

                await _session.RecordAssistantTurnAsync(
                    response, durationMs: sw.ElapsedMilliseconds,
                    tokensOut: tokenEstimate).ConfigureAwait(false);

                turnEntry.Attributes["streaming_fallback"] = "true";
                turnEntry.Complete();
                _session.LastTimeline = traceCtx.Timeline;

                return response;
            }
            catch (Exception fallbackEx) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
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
                if (retryToolResult is not null)
                {
                    output.RenderToolCall(retryToolResult.Value.FunctionName, null, retryToolResult.Value.Result);
                    _session.History.AddAssistantMessage(response);
                    _session.History.AddUserMessage(
                        $"[Tool result for {retryToolResult.Value.FunctionName}]:\n{retryToolResult.Value.Result}");

                    sw.Restart();
                    var followUp = await chat.GetChatMessageContentAsync(
                        _session.History, retrySettings, _session.Kernel, ct).ConfigureAwait(false);
                    sw.Stop();
                    response = followUp.Content ?? retryToolResult.Value.Result;
                }

                _session.History.AddAssistantMessage(response);

                var tokenEstimate = JD.SemanticKernel.Extensions.Compaction.TokenEstimator
                    .EstimateTokens(response);

                output.BeginStreaming();
                output.WriteStreamingChunk(response);
                output.EndStreaming();
                output.EndTurn(new TurnMetrics(sw.ElapsedMilliseconds, tokenEstimate, 0));

                await _session.RecordAssistantTurnAsync(
                    response, durationMs: sw.ElapsedMilliseconds,
                    tokensOut: tokenEstimate).ConfigureAwait(false);

                turnEntry.Attributes["tool_calling_fallback"] = "true";
                turnEntry.Complete();
                _session.LastTimeline = traceCtx.Timeline;

                return response;
            }
            catch (Exception retryEx) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
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
                msg.Contains("StatusCode: 400", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Saves a captured workflow if recording is active and tool calls were captured.
    /// </summary>
    private async Task SaveCapturedWorkflowIfActiveAsync(CancellationToken ct)
    {
        if (!string.Equals(_session.ActiveWorkflowName, "recording", StringComparison.Ordinal) ||
            _session.CapturedWorkflowSteps.Count == 0)
            return;

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
            }
        }

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
    private readonly record struct TextToolCallResult(string FunctionName, string Result);

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
            !ShouldAllowTaggedToolUseFallback(response))
            return null;

        var jsonText = ExtractFirstToolCallJson(response);
        if (jsonText is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            if (!TrySelectToolCallElement(doc.RootElement, out var toolCall))
                return null;

            if (!toolCall.TryGetProperty("name", out var nameEl) ||
                nameEl.ValueKind != JsonValueKind.String)
                return null;

            var fullName = nameEl.GetString();
            if (string.IsNullOrEmpty(fullName))
                return null;

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

            if (func is null)
                return null;

            // Build arguments from the "arguments" property
            var args = new KernelArguments();
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

            DebugLogger.Log(DebugCategory.Agents,
                "Intercepted text-based tool call: {0} with {1} args", fullName, args.Count);

            var argsSummary = string.Join(", ", args.Select(kv =>
            {
                var val = kv.Value?.ToString() ?? "null";
                if (val.Length > 200)
                    val = string.Concat(val.AsSpan(0, 197), "...");
                return $"{kv.Key}={val}";
            }));

            if (!CheckTextToolCallSafety(fullName!, argsSummary))
            {
                DebugLogger.Log(DebugCategory.Agents,
                    "Text-based tool call blocked by safety gate: {0}", fullName);
                return null;
            }

            var result = await func.InvokeAsync(_session.Kernel, args, ct).ConfigureAwait(false);
            var resultStr = result?.ToString() ?? "(no output)";

            AgentOutput.Current.RenderToolCall(fullName!, argsSummary, resultStr);

            return new TextToolCallResult(fullName, resultStr);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.Log(DebugCategory.Agents,
                "Text-based tool call execution failed: {0}", ex.Message);
            return null;
        }
    }

    private bool ShouldEnforceStructuredToolChannel() =>
        _session.CurrentModel?.Capabilities.HasFlag(ModelCapabilities.ToolCalling) ?? false;

    private bool ShouldAllowTaggedToolUseFallback(string response)
    {
        var provider = _session.CurrentModel?.ProviderName ?? string.Empty;
        if (!provider.Contains("anthropic", StringComparison.OrdinalIgnoreCase) &&
            !provider.Contains("claude", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ContainsTaggedToolUsePayload(response);
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

    private static readonly Regex EntireFencedJsonRegex = new(
        @"\A```(?:json)?\s*\r?\n(?<json>(?:\{[\s\S]*?\}|\[[\s\S]*?\]))\s*\r?\n```\z",
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
    private bool CheckTextToolCallSafety(string functionName, string argsSummary)
    {
        var canonicalName = OpenClawToolAliasResolver.Resolve(functionName);
        return EvaluateTextToolCallSafety(
            canonicalName, argsSummary,
            _session.PermissionMode, _session.SkipPermissions, _session.AutoRunEnabled,
            _session.ToolSafetyTiers, _textToolConfirmedOnce, AgentOutput.Current);
    }

    /// <summary>
    /// Pure-logic safety evaluation for text-based tool calls.
    /// Returns true if the tool call should proceed, false if blocked.
    /// </summary>
    internal static bool EvaluateTextToolCallSafety(
        string canonicalName, string argsSummary,
        PermissionMode permissionMode, bool skipPermissions, bool autoRunEnabled,
        IReadOnlyDictionary<string, Tools.SafetyTier>? tierMap,
        HashSet<string> confirmedOnce, IAgentOutput output)
    {
        var tier = tierMap?.GetValueOrDefault(canonicalName, Tools.SafetyTier.AlwaysConfirm)
                   ?? Tools.SafetyTier.AlwaysConfirm;

        switch (permissionMode)
        {
            case PermissionMode.Plan:
                if (tier != Tools.SafetyTier.AutoApprove)
                {
                    output.RenderWarning($"  \u2717 {canonicalName} blocked (plan mode \u2014 read-only)");
                    return false;
                }
                break;
            case PermissionMode.BypassAll:
                output.RenderInfo($"  \u25b8 [text-tool] {canonicalName}({argsSummary})");
                return true;
            case PermissionMode.AcceptEdits:
                if (tier == Tools.SafetyTier.AlwaysConfirm)
                {
                    if (!output.ConfirmToolCall(canonicalName, argsSummary))
                        return false;
                }
                else
                    output.RenderInfo($"  \u25b8 [text-tool] {canonicalName}({argsSummary})");
                return true;
            default: // Normal
                break;
        }

        if (skipPermissions || autoRunEnabled)
        {
            output.RenderInfo($"  \u25b8 [text-tool] {canonicalName}({argsSummary})");
            return true;
        }

        var needsConfirm = tier switch
        {
            Tools.SafetyTier.AutoApprove => false,
            Tools.SafetyTier.ConfirmOnce => !confirmedOnce.Contains(canonicalName),
            Tools.SafetyTier.AlwaysConfirm => true,
            _ => true,
        };

        if (needsConfirm)
        {
            if (!output.ConfirmToolCall(canonicalName, argsSummary))
                return false;
            if (tier == Tools.SafetyTier.ConfirmOnce)
                confirmedOnce.Add(canonicalName);
        }
        else
            output.RenderInfo($"  \u25b8 [text-tool] {canonicalName}({argsSummary})");

        return true;
    }
}
