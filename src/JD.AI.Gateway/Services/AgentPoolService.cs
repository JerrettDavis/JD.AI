using System.Collections.Concurrent;
using System.Diagnostics;
using JD.AI.Core.Agents;
using JD.AI.Core.Channels;
using JD.AI.Core.Events;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Config;
using JD.AI.Core.Tools;
using JD.AI.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Gateway.Services;

/// <summary>
/// Manages a pool of live agent instances. Each agent has its own
/// <see cref="Kernel"/>, <see cref="ChatHistory"/>, and lifecycle.
/// </summary>
public sealed class AgentPoolService : IHostedService
{
    private readonly IProviderRegistry _providers;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AgentPoolService> _logger;
    private readonly ConcurrentDictionary<string, AgentInstance> _agents = new();

    /// <summary>Maximum retry attempts for transient Ollama errors.</summary>
    internal const int MaxRetries = 3;

    /// <summary>Base delay between retries (doubles each attempt).</summary>
    internal static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(2);

    public AgentPoolService(
        IProviderRegistry providers, IEventBus eventBus,
        ILogger<AgentPoolService> logger)
    {
        _providers = providers;
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _agents.Clear();
        return Task.CompletedTask;
    }

    public IReadOnlyList<AgentInfo> ListAgents() =>
        _agents.Values.Select(a => new AgentInfo(a.Id, a.Provider, a.Model, a.TurnCount, a.CreatedAt)).ToList();

    public async Task<string> SpawnAgentAsync(
        string provider, string model, string? systemPrompt,
        CancellationToken ct, ModelParameters? parameters = null,
        IReadOnlyList<string>? fallbackProviders = null)
    {
        var allProviders = await _providers.DetectProvidersAsync(ct);
        var providerInfo = allProviders.FirstOrDefault(p =>
            p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Provider '{provider}' not found or not available.");

        var modelInfo = providerInfo.Models.FirstOrDefault(m =>
            m.Id.Equals(model, StringComparison.OrdinalIgnoreCase)
            || m.DisplayName.Equals(model, StringComparison.OrdinalIgnoreCase)
            // Support short model names (e.g., "llama3.2" matches "llama3.2:latest")
            || m.Id.StartsWith(model + ":", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Model '{model}' not found in provider '{provider}'.");

        var detector = _providers.GetDetector(provider)
            ?? throw new InvalidOperationException($"No detector for provider '{provider}'.");

        var kernel = detector.BuildKernel(modelInfo);

        // Register core tools (file, exec, web search, memory, tasks)
        // Uses the shared CoreToolRegistrar from JD.AI.Core — works without session infrastructure
        try
        {
            CoreToolRegistrar.Register(kernel);
            _logger.LogInformation("Registered core tools for agent ({Provider}/{Model})",
                provider, model);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register core tools for agent — agent will run without tools");
        }

        var history = new ChatHistory();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            history.AddSystemMessage(systemPrompt);

        var id = Guid.NewGuid().ToString("N")[..12];
        var instance = new AgentInstance(id, provider, model, kernel, history, parameters, fallbackProviders);
        _agents[id] = instance;

        await _eventBus.PublishAsync(
            new GatewayEvent("agent.spawned", id, DateTimeOffset.UtcNow, new { provider, model }), ct);

        return id;
    }

    public Task<string?> SendMessageAsync(string agentId, string message, CancellationToken ct)
        => SendMessageAsync(agentId, message, attachments: null, ct);

    public Task<string?> SendMessageAsync(string agentId, ChannelMessage message, CancellationToken ct)
        => SendMessageAsync(agentId, message.Content, message.Attachments, ct);

    private async Task<string?> SendMessageAsync(
        string agentId,
        string message,
        IReadOnlyList<ChannelAttachment>? attachments,
        CancellationToken ct)
    {
        if (!_agents.TryGetValue(agentId, out var agent)) return null;

        var turnTraceId = Guid.NewGuid().ToString("N")[..8];
        var toolIntent = LooksLikeToolIntent(message);

        await AddUserTurnToHistoryAsync(agent.History, message, attachments, ct).ConfigureAwait(false);
        var chat = agent.Kernel.GetRequiredService<IChatCompletionService>();
        var settings = BuildExecutionSettings(agent.Parameters, agent.Provider, agent.Model);
        PromptCachePolicy.Apply(
            settings,
            agent.Provider,
            agent.Model,
            agent.History,
            enabled: true,
            ttl: PromptCacheTtl.FiveMinutes);

        using var turnActivity = ActivitySources.Agent.StartActivity("jdai.agent.turn");
        turnActivity?.SetTag("jdai.session.agent_id", agentId);
        turnActivity?.SetTag("jdai.turn.index", agent.TurnCount);
        turnActivity?.SetTag("gen_ai.system", agent.Provider);
        turnActivity?.SetTag("gen_ai.request.model", agent.Model);
        turnActivity?.SetTag("jdai.turn.trace_id", turnTraceId);
        turnActivity?.SetTag("jdai.turn.tool_intent", toolIntent);

        var sw = Stopwatch.StartNew();
        string? content;
        try
        {
            var response = await SendWithRetryAsync(
                chat, agent, settings, ct).ConfigureAwait(false);

            content = response.Content ?? "";

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning(
                    "Empty model response on first attempt. trace={TraceId} agent={AgentId} provider={Provider} model={Model} toolIntent={ToolIntent} promptChars={PromptChars}",
                    turnTraceId, agentId, agent.Provider, agent.Model, toolIntent, message.Length);

                // Retry once with strict low-latency settings to reduce blank-turn risk.
                var retrySettings = BuildExecutionSettings(agent.Parameters, agent.Provider, agent.Model);
                retrySettings.MaxTokens = Math.Min(retrySettings.MaxTokens ?? 512, 512);
                retrySettings.Temperature = 0.2;
                PromptCachePolicy.Apply(
                    retrySettings,
                    agent.Provider,
                    agent.Model,
                    agent.History,
                    enabled: true,
                    ttl: PromptCacheTtl.FiveMinutes);

                var retryResponse = await SendWithRetryAsync(
                    chat, agent, retrySettings, ct).ConfigureAwait(false);
                content = retryResponse.Content ?? "";
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                turnActivity?.SetTag("jdai.turn.empty_after_retry", true);

                // Try configured fallback providers before surfacing a retry message.
                var emptyFallback = await TryFallbackProvidersAsync(agent, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(emptyFallback))
                {
                    content = emptyFallback;
                    turnActivity?.SetTag("jdai.turn.empty_fallback_used", true);
                }
                else
                {
                    // Final fallback to deterministic operator-facing text with trace id.
                    content = $"[JD.AI turn produced empty output after retry. trace={turnTraceId}. Please retry your request.]";
                }
            }

            agent.History.AddAssistantMessage(content);
            agent.TurnCount++;

            sw.Stop();

            turnActivity?.SetTag("jdai.agent.turn_count", agent.TurnCount);
            turnActivity?.SetStatus(ActivityStatusCode.Ok);

            // Record metrics
            var providerTag = new KeyValuePair<string, object?>("gen_ai.system", agent.Provider);
            Meters.TurnCount.Add(1, providerTag);
            Meters.TurnDuration.Record(sw.Elapsed.TotalMilliseconds, providerTag);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            // Cancellations are not actionable failures; leave span status as Unset.
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            turnActivity?.AddException(ex);
            turnActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Meters.ProviderErrors.Add(1,
                new KeyValuePair<string, object?>("gen_ai.system", agent.Provider));

            // Try fallback providers before giving up
            var fallbackResult = await TryFallbackProvidersAsync(agent, ct).ConfigureAwait(false);
            if (fallbackResult is not null)
            {
                content = fallbackResult;
                agent.History.AddAssistantMessage(content);
                agent.TurnCount++;

                turnActivity?.SetTag("jdai.agent.fallback_used", "true");

                await _eventBus.PublishAsync(
                    new GatewayEvent("agent.turn_complete", agentId, DateTimeOffset.UtcNow,
                        new { Turn = agent.TurnCount, Fallback = true, Trace = turnTraceId }), ct);

                return content;
            }

            throw;
        }

        await _eventBus.PublishAsync(
            new GatewayEvent("agent.turn_complete", agentId, DateTimeOffset.UtcNow,
                new { Turn = agent.TurnCount, Trace = turnTraceId }), ct);

        return content;
    }

    /// <summary>
    /// Tries each configured fallback provider/model in order. Returns the
    /// response content on first success, or <c>null</c> if all fallbacks fail.
    /// </summary>
    private async Task<string?> TryFallbackProvidersAsync(
        AgentInstance agent, CancellationToken ct)
    {
        if (agent.FallbackProviders.Count == 0)
            return null;

        foreach (var fallback in agent.FallbackProviders)
        {
            if (ct.IsCancellationRequested) break;

            var (fbProvider, fbModel) = ParseProviderModel(fallback);

            try
            {
                var allProviders = await _providers.DetectProvidersAsync(ct);
                var providerInfo = allProviders.FirstOrDefault(p =>
                    p.Name.Equals(fbProvider, StringComparison.OrdinalIgnoreCase));

                if (providerInfo is null || !providerInfo.IsAvailable)
                {
                    _logger.LogDebug("Fallback provider '{Provider}' not available, skipping", fbProvider);
                    continue;
                }

                var modelInfo = fbModel is not null
                    ? providerInfo.Models.FirstOrDefault(m =>
                        m.Id.Equals(fbModel, StringComparison.OrdinalIgnoreCase)
                        || m.DisplayName.Equals(fbModel, StringComparison.OrdinalIgnoreCase)
                        || m.Id.StartsWith(fbModel + ":", StringComparison.OrdinalIgnoreCase))
                    : providerInfo.Models.Count > 0 ? providerInfo.Models[0] : null;

                if (modelInfo is null)
                {
                    _logger.LogDebug("No suitable model found for fallback provider '{Provider}', skipping", fbProvider);
                    continue;
                }

                var detector = _providers.GetDetector(fbProvider);
                if (detector is null) continue;

                var fbKernel = detector.BuildKernel(modelInfo);
                var fbChat = fbKernel.GetRequiredService<IChatCompletionService>();

                _logger.LogInformation(
                    "Falling back to {Provider}/{Model} for agent {AgentId}",
                    fbProvider, modelInfo.Id, agent.Id);

                await _eventBus.PublishAsync(
                    new GatewayEvent("agent.fallback", agent.Id, DateTimeOffset.UtcNow,
                        new { Provider = fbProvider, Model = modelInfo.Id }), ct);

                var fallbackSettings = BuildExecutionSettings(
                    agent.Parameters,
                    fbProvider,
                    modelInfo.Id);
                PromptCachePolicy.Apply(
                    fallbackSettings,
                    fbProvider,
                    modelInfo.Id,
                    agent.History,
                    enabled: true,
                    ttl: PromptCacheTtl.FiveMinutes);

                var result = await fbChat.GetChatMessageContentAsync(
                    agent.History, fallbackSettings, fbKernel, cancellationToken: ct).ConfigureAwait(false);

                return result.Content ?? "";
            }
#pragma warning disable CA1031
            catch (Exception fbEx)
            {
                _logger.LogWarning(
                    "Fallback provider '{Provider}' also failed: {Error}",
                    fallback, fbEx.Message);
            }
#pragma warning restore CA1031
        }

        return null;
    }

    private static async Task AddUserTurnToHistoryAsync(
        ChatHistory history,
        string message,
        IReadOnlyList<ChannelAttachment>? attachments,
        CancellationToken ct)
    {
        var imageAttachments = attachments?
            .Where(a => a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();

        if (imageAttachments is null || imageAttachments.Count == 0)
        {
            history.AddUserMessage(message);
            return;
        }

        var items = new ChatMessageContentItemCollection
        {
            new TextContent(message)
        };

        foreach (var attachment in imageAttachments)
        {
            const long maxImageBytes = 8 * 1024 * 1024; // 8 MB per image item
            if (attachment.SizeBytes > maxImageBytes)
                continue;

            await using var stream = await attachment.OpenReadAsync(ct).ConfigureAwait(false);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            items.Add(new ImageContent(ms.ToArray(), attachment.ContentType));
        }

        if (items.Count == 1)
        {
            // All images were filtered out (size/stream issues), keep text-only behavior.
            history.AddUserMessage(message);
            return;
        }

        history.AddUserMessage(items);
    }

    private static bool LooksLikeToolIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var m = message.ToLowerInvariant();
        return m.Contains("run ")
            || m.Contains("execute")
            || m.Contains("echo ")
            || m.Contains("ls")
            || m.Contains("dir")
            || m.Contains("web_search")
            || m.Contains("web_fetch")
            || m.Contains("fetch")
            || m.Contains("http://")
            || m.Contains("https://")
            || m.Contains("url")
            || m.Contains("search")
            || m.Contains("read ")
            || m.Contains("file");
    }

    /// <summary>
    /// Parses "provider/model" or "provider" into its components.
    /// </summary>
    internal static (string Provider, string? Model) ParseProviderModel(string input)
    {
        var slashIdx = input.IndexOf('/', StringComparison.Ordinal);
        return slashIdx >= 0
            ? (input[..slashIdx], input[(slashIdx + 1)..])
            : (input, null);
    }

    /// <summary>
    /// Sends a chat completion request with exponential-backoff retry for
    /// transient provider errors (model runner crash, 500s, connection resets).
    /// </summary>
    internal async Task<ChatMessageContent> SendWithRetryAsync(
        IChatCompletionService chat, AgentInstance agent,
        OpenAIPromptExecutionSettings settings, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var providerActivity = ActivitySources.Providers.StartActivity("jdai.provider.chat_completion");
            providerActivity?.SetTag("gen_ai.system", agent.Provider);
            providerActivity?.SetTag("gen_ai.request.model", agent.Model);
            providerActivity?.SetTag("jdai.provider.attempt", attempt);

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await chat.GetChatMessageContentAsync(
                    agent.History, settings, agent.Kernel, cancellationToken: ct).ConfigureAwait(false);

                sw.Stop();
                providerActivity?.SetStatus(ActivityStatusCode.Ok);

                Meters.ProviderLatency.Record(
                    sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("gen_ai.system", agent.Provider));

                return result;
            }
            catch (Exception ex) when (attempt < MaxRetries && IsTransientProviderError(ex) && !ct.IsCancellationRequested)
            {
                sw.Stop();
                providerActivity?.AddException(ex);
                providerActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                var delay = BaseRetryDelay * Math.Pow(2, attempt);
                _logger.LogWarning(
                    "Transient provider error on attempt {Attempt}/{MaxRetries}: {Error}. Retrying in {Delay}s...",
                    attempt + 1, MaxRetries, ex.Message, delay.TotalSeconds);

                await _eventBus.PublishAsync(
                    new GatewayEvent("agent.retry", agent.Id, DateTimeOffset.UtcNow,
                        new { Attempt = attempt + 1, MaxRetries, Reason = ex.Message }), ct);

                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                sw.Stop();
                providerActivity?.AddException(ex);
                providerActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }

    /// <summary>
    /// Determines whether an exception represents a transient provider error
    /// that is likely to succeed on retry (model runner crash, resource limits,
    /// connection reset, timeout).
    /// </summary>
    internal static bool IsTransientProviderError(Exception ex)
    {
        // Walk the exception chain for inner causes
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var msg = current.Message;

            // Ollama model runner crash (500 with specific message)
            if (msg.Contains("model runner", StringComparison.OrdinalIgnoreCase))
                return true;

            // Resource-related crashes
            if (msg.Contains("resource limitations", StringComparison.OrdinalIgnoreCase))
                return true;

            // Generic 500 from Ollama
            if (msg.Contains("500", StringComparison.Ordinal) &&
                msg.Contains("error", StringComparison.OrdinalIgnoreCase))
                return true;

            // Connection reset / refused (Ollama process restarting)
            if (current is HttpRequestException hrex &&
                (hrex.StatusCode == System.Net.HttpStatusCode.InternalServerError ||
                 hrex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                 hrex.StatusCode == System.Net.HttpStatusCode.BadGateway))
                return true;

            // Socket-level errors (ECONNRESET, ECONNREFUSED)
            if (current is System.Net.Sockets.SocketException)
                return true;

            // I/O errors during streaming
            if (current is IOException)
                return true;
        }

        return false;
    }

    public void ClearHistory(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.History.Clear();
            agent.TurnCount = 0;
        }
    }

    public void StopAgent(string agentId)
    {
        _agents.TryRemove(agentId, out _);
    }

    public IProviderDetector? GetDetector(string provider) =>
        _providers.GetDetector(provider);

    internal static OpenAIPromptExecutionSettings BuildExecutionSettings(
        ModelParameters? p,
        string? providerName = null,
        string? modelId = null)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = p?.MaxTokens ?? 4096,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true),
        };

        if (p is null) return settings;

        if (p.Temperature.HasValue) settings.Temperature = p.Temperature.Value;
        if (p.TopP.HasValue) settings.TopP = p.TopP.Value;
        if (p.FrequencyPenalty.HasValue) settings.FrequencyPenalty = p.FrequencyPenalty.Value;
        if (p.PresencePenalty.HasValue) settings.PresencePenalty = p.PresencePenalty.Value;
        if (p.Seed.HasValue) settings.Seed = p.Seed.Value;
        if (p.StopSequences.Count > 0) settings.StopSequences = p.StopSequences;

        // Ollama-specific params go via ExtensionData
        var extra = new Dictionary<string, object>();
        if (p.TopK.HasValue) extra["top_k"] = p.TopK.Value;
        if (p.ContextWindowSize is > 0) extra["num_ctx"] = p.ContextWindowSize.Value;
        if (p.RepeatPenalty.HasValue) extra["repeat_penalty"] = p.RepeatPenalty.Value;

        // Disable thinking mode for Ollama models in daemon/gateway path.
        // Qwen3.5 thinking tokens consume the token budget and produce empty
        // visible responses, causing silent failures on Discord.
        if (string.Equals(providerName, "Ollama", StringComparison.OrdinalIgnoreCase))
            extra["think"] = false;

        if (extra.Count > 0) settings.ExtensionData = extra;

        AgentLoop.ApplyReasoningEffort(
            settings,
            new ProviderModelInfo(modelId ?? "unknown", modelId ?? "unknown", providerName ?? "unknown"),
            p.ReasoningEffort);

        return settings;
    }

    internal sealed class AgentInstance(
        string id, string provider, string model,
        Kernel kernel, ChatHistory history,
        ModelParameters? parameters = null,
        IReadOnlyList<string>? fallbackProviders = null)
    {
        public string Id => id;
        public string Provider => provider;
        public string Model => model;
        public Kernel Kernel => kernel;
        public ChatHistory History => history;
        public ModelParameters? Parameters => parameters;
        public IReadOnlyList<string> FallbackProviders => fallbackProviders ?? [];
        public int TurnCount { get; set; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    }
}

public record AgentInfo(string Id, string Provider, string Model, int TurnCount, DateTimeOffset CreatedAt);
