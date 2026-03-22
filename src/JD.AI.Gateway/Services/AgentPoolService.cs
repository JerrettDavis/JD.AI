using System.Collections.Concurrent;
using System.Diagnostics;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Core.Events;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using JD.AI.Core.Sessions;
using JD.AI.Gateway.Config;
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
    private const string MainSessionSuffix = "main";

    private readonly IProviderRegistry _providers;
    private readonly IEventBus _eventBus;
    private readonly SessionStore _sessionStore;
    private readonly ILogger<AgentPoolService> _logger;
    private readonly ConcurrentDictionary<string, AgentInstance> _agents = new();

    /// <summary>Maximum retry attempts for transient Ollama errors.</summary>
    internal const int MaxRetries = 3;

    /// <summary>Base delay between retries (doubles each attempt).</summary>
    internal static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(2);

    public AgentPoolService(
        IProviderRegistry providers, IEventBus eventBus,
        ILogger<AgentPoolService> logger)
        : this(providers, eventBus, logger, sessionStore: null)
    {
    }

    public AgentPoolService(
        IProviderRegistry providers,
        IEventBus eventBus,
        ILogger<AgentPoolService> logger,
        SessionStore? sessionStore)
    {
        _providers = providers;
        _eventBus = eventBus;
        _sessionStore = sessionStore ?? new SessionStore();
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _sessionStore.InitializeAsync().ConfigureAwait(false);
    }

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
        IReadOnlyList<string>? fallbackProviders = null,
        string? preferredAgentId = null)
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
        var history = new ChatHistory();

        var id = ResolveAgentId(preferredAgentId);
        var session = await EnsureSessionAsync(id, provider, model, systemPrompt, ct).ConfigureAwait(false);
        var nextTurnIndex = session.Turns.Count;
        foreach (var turn in session.Turns.OrderBy(t => t.TurnIndex))
        {
            if (string.IsNullOrWhiteSpace(turn.Content))
                continue;

            switch (turn.Role.Trim().ToLowerInvariant())
            {
                case "system":
                    history.AddSystemMessage(turn.Content);
                    break;
                case "user":
                    history.AddUserMessage(turn.Content);
                    break;
                default:
                    history.AddAssistantMessage(turn.Content);
                    break;
            }
        }

        var instance = new AgentInstance(
            id,
            provider,
            model,
            kernel,
            history,
            parameters,
            fallbackProviders,
            session.Id,
            nextTurnIndex);
        _agents[id] = instance;

        await _eventBus.PublishAsync(
            new GatewayEvent("agent.spawned", id, DateTimeOffset.UtcNow, new { provider, model }), ct);

        return id;
    }

    public async Task<string?> SendMessageAsync(string agentId, string message, CancellationToken ct)
    {
        if (!_agents.TryGetValue(agentId, out var agent)) return null;

        agent.History.AddUserMessage(message);
        await SaveTurnAsync(agent, role: "user", content: message, durationMs: 0, ct).ConfigureAwait(false);
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

        var sw = Stopwatch.StartNew();
        string? content;
        try
        {
            var response = await SendWithRetryAsync(
                chat, agent, settings, ct).ConfigureAwait(false);

            content = response.Content ?? "";
            agent.History.AddAssistantMessage(content);
            agent.TurnCount++;
            await SaveTurnAsync(agent, role: "assistant", content: content, durationMs: (long)sw.Elapsed.TotalMilliseconds, ct)
                .ConfigureAwait(false);

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
                content = fallbackResult.Content;
                agent.History.AddAssistantMessage(content);
                agent.TurnCount++;
                await SaveTurnAsync(
                        agent,
                        role: "assistant",
                        content: content,
                        durationMs: (long)sw.Elapsed.TotalMilliseconds,
                        ct,
                        providerOverride: fallbackResult.Provider,
                        modelOverride: fallbackResult.Model)
                    .ConfigureAwait(false);

                turnActivity?.SetTag("jdai.agent.fallback_used", "true");

                await _eventBus.PublishAsync(
                    new GatewayEvent("agent.turn_complete", agentId, DateTimeOffset.UtcNow,
                        new { Turn = agent.TurnCount, Fallback = true }), ct);

                return content;
            }

            throw;
        }

        await _eventBus.PublishAsync(
            new GatewayEvent("agent.turn_complete", agentId, DateTimeOffset.UtcNow,
                new { Turn = agent.TurnCount }), ct);

        return content;
    }

    /// <summary>
    /// Tries each configured fallback provider/model in order. Returns the
    /// response content on first success, or <c>null</c> if all fallbacks fail.
    /// </summary>
    private async Task<FallbackResponse?> TryFallbackProvidersAsync(
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
                    agent.History, fallbackSettings, cancellationToken: ct).ConfigureAwait(false);

                return new FallbackResponse(
                    result.Content ?? "",
                    fbProvider,
                    modelInfo.Id);
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
                    agent.History, settings, cancellationToken: ct).ConfigureAwait(false);

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
        if (_agents.TryRemove(agentId, out var removed))
        {
            _ = CloseSessionBestEffortAsync(removed.SessionId);
        }
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
        IReadOnlyList<string>? fallbackProviders = null,
        string? sessionId = null,
        int nextTurnIndex = 0)
    {
        public string Id => id;
        public string Provider => provider;
        public string Model => model;
        public Kernel Kernel => kernel;
        public ChatHistory History => history;
        public ModelParameters? Parameters => parameters;
        public IReadOnlyList<string> FallbackProviders => fallbackProviders ?? [];
        public string SessionId => sessionId ?? BuildSessionId(id);
        public int NextTurnIndex { get; set; } = nextTurnIndex;
        public int TurnCount { get; set; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    }

    private sealed record FallbackResponse(string Content, string Provider, string Model);

    private static string BuildSessionId(string agentId) =>
        $"agent:{agentId}:{MainSessionSuffix}";

    private string ResolveAgentId(string? preferredAgentId)
    {
        if (string.IsNullOrWhiteSpace(preferredAgentId))
            return Guid.NewGuid().ToString("N")[..12];

        var baseId = preferredAgentId.Trim();
        if (!_agents.ContainsKey(baseId))
            return baseId;

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{baseId}-{suffix}";
            if (!_agents.ContainsKey(candidate))
                return candidate;
        }

        return Guid.NewGuid().ToString("N")[..12];
    }

    private async Task<SessionInfo> EnsureSessionAsync(
        string agentId,
        string provider,
        string model,
        string? systemPrompt,
        CancellationToken ct)
    {
        await _sessionStore.InitializeAsync().ConfigureAwait(false);
        var sessionId = BuildSessionId(agentId);
        var existing = await _sessionStore.GetSessionAsync(sessionId).ConfigureAwait(false);
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await _sessionStore.UpdateSessionAsync(existing).ConfigureAwait(false);
            return existing;
        }

        var projectPath = DataDirectories.Root;
        var created = new SessionInfo
        {
            Id = sessionId,
            Name = $"{agentId} ({MainSessionSuffix})",
            ProjectPath = projectPath,
            ProjectHash = ProjectHasher.Hash(projectPath),
            ProviderName = provider,
            ModelId = model,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
            MessageCount = 0,
            TotalTokens = 0,
        };
        await _sessionStore.CreateSessionAsync(created).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            var systemTurn = new TurnRecord
            {
                SessionId = created.Id,
                TurnIndex = 0,
                Role = "system",
                Content = systemPrompt,
                ProviderName = provider,
                ModelId = model,
                DurationMs = 0,
            };
            await _sessionStore.SaveTurnAsync(systemTurn).ConfigureAwait(false);
            created.MessageCount = 1;
            created.UpdatedAt = DateTime.UtcNow;
            await _sessionStore.UpdateSessionAsync(created).ConfigureAwait(false);
        }

        return await _sessionStore.GetSessionAsync(sessionId).ConfigureAwait(false) ?? created;
    }

    private async Task SaveTurnAsync(
        AgentInstance agent,
        string role,
        string content,
        long durationMs,
        CancellationToken ct,
        string? providerOverride = null,
        string? modelOverride = null)
    {
        ct.ThrowIfCancellationRequested();
        var turn = new TurnRecord
        {
            SessionId = agent.SessionId,
            TurnIndex = agent.NextTurnIndex++,
            Role = role,
            Content = content,
            ProviderName = providerOverride ?? agent.Provider,
            ModelId = modelOverride ?? agent.Model,
            DurationMs = durationMs,
        };
        await _sessionStore.SaveTurnAsync(turn).ConfigureAwait(false);

        var session = await _sessionStore.GetSessionAsync(agent.SessionId).ConfigureAwait(false);
        if (session is null)
            return;

        session.IsActive = true;
        session.UpdatedAt = DateTime.UtcNow;
        session.MessageCount = Math.Max(session.MessageCount, agent.NextTurnIndex);
        await _sessionStore.UpdateSessionAsync(session).ConfigureAwait(false);
    }

    private async Task CloseSessionBestEffortAsync(string sessionId)
    {
        try
        {
            await _sessionStore.CloseSessionAsync(sessionId).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch
        {
            // Session close is best-effort during shutdown/removal.
        }
#pragma warning restore CA1031
    }
}

public record AgentInfo(string Id, string Provider, string Model, int TurnCount, DateTimeOffset CreatedAt);
