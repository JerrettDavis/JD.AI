using System.Diagnostics;
using System.Text;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
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

        var settings = BuildExecutionSettings();
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
            var errorMsg = $"Error: {ex.Message}";
            AgentOutput.Current.RenderError(errorMsg);

            _session.History.AddAssistantMessage(
                $"[Error occurred: {ex.Message}. I'll try a different approach.]");

            return errorMsg;
        }
    }

    /// <summary>
    /// Builds provider-appropriate execution settings for the current model.
    /// Uses <see cref="OpenAIPromptExecutionSettings"/> for <c>MaxTokens</c> support
    /// with SK's unified <see cref="FunctionChoiceBehavior"/> (not the deprecated
    /// <c>ToolCallBehavior</c>) so tool calling works across all connector types.
    /// MEAI adapters read <c>FunctionChoiceBehavior</c> and <c>ModelId</c> from the
    /// base <see cref="PromptExecutionSettings"/> class.
    /// </summary>
    private OpenAIPromptExecutionSettings BuildExecutionSettings()
    {
        var supportsTools = _session.CurrentModel?.Capabilities
            .HasFlag(ModelCapabilities.ToolCalling) ?? false;

        // Disable tools when the model's context window is too small to fit them.
        // Each tool definition consumes ~200 tokens (name, description, schema).
        // If the estimated tool tokens would exceed half the context window,
        // the model has no room for conversation — disable tools to avoid OOM.
        if (supportsTools)
        {
            var contextWindow = _session.CurrentModel?.ContextWindowTokens ?? 128_000;
            var toolCount = _session.Kernel.Plugins.SelectMany(p => p).Count();
            var estimatedToolTokens = toolCount * 200;

            if (estimatedToolTokens > contextWindow / 2)
            {
                DebugLogger.Log(DebugCategory.Agents,
                    "Disabling tools: {0} tools (~{1} tokens) exceed half of {2}-token context window",
                    toolCount, estimatedToolTokens, contextWindow);
                supportsTools = false;
            }
        }

        var maxTokens = _session.CurrentModel?.MaxOutputTokens;
        if (maxTokens is null or <= 0)
        {
            maxTokens = 4096;
        }

        return new OpenAIPromptExecutionSettings
        {
            ModelId = _session.CurrentModel?.Id,
            MaxTokens = maxTokens,
            FunctionChoiceBehavior = supportsTools
                ? FunctionChoiceBehavior.Auto()
                : null,
        };
    }

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
            if (current.GetType().Name == "HttpIOException")
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
}
