using System.Diagnostics;
using System.Text;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
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
    /// Uses SK's unified <see cref="FunctionChoiceBehavior"/> instead of
    /// OpenAI-specific <c>ToolCallBehavior</c> so that tool calling works
    /// across all connector types (OpenAI, MEAI/Anthropic, native SK connectors).
    /// </summary>
    private PromptExecutionSettings BuildExecutionSettings()
    {
        var supportsTools = _session.CurrentModel?.Capabilities
            .HasFlag(ModelCapabilities.ToolCalling) ?? false;

        return new PromptExecutionSettings
        {
            ModelId = _session.CurrentModel?.Id,
            FunctionChoiceBehavior = supportsTools
                ? FunctionChoiceBehavior.Auto()
                : null,
        };
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
