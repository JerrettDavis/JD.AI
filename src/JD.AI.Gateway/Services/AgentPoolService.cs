using System.Collections.Concurrent;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Config;
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
    private readonly ConcurrentDictionary<string, AgentInstance> _agents = new();

    public AgentPoolService(IProviderRegistry providers, IEventBus eventBus)
    {
        _providers = providers;
        _eventBus = eventBus;
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
        CancellationToken ct, ModelParameters? parameters = null)
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

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            history.AddSystemMessage(systemPrompt);

        var id = Guid.NewGuid().ToString("N")[..12];
        var instance = new AgentInstance(id, provider, model, kernel, history, parameters);
        _agents[id] = instance;

        await _eventBus.PublishAsync(
            new GatewayEvent("agent.spawned", id, DateTimeOffset.UtcNow, new { provider, model }), ct);

        return id;
    }

    public async Task<string?> SendMessageAsync(string agentId, string message, CancellationToken ct)
    {
        if (!_agents.TryGetValue(agentId, out var agent)) return null;

        agent.History.AddUserMessage(message);
        var chat = agent.Kernel.GetRequiredService<IChatCompletionService>();

        var settings = BuildExecutionSettings(agent.Parameters);
        var response = await chat.GetChatMessageContentAsync(agent.History, settings, cancellationToken: ct);
        agent.History.AddAssistantMessage(response.Content ?? "");
        agent.TurnCount++;

        await _eventBus.PublishAsync(
            new GatewayEvent("agent.turn_complete", agentId, DateTimeOffset.UtcNow,
                new { Turn = agent.TurnCount }), ct);

        return response.Content;
    }

    public void StopAgent(string agentId)
    {
        _agents.TryRemove(agentId, out _);
    }

    public IProviderDetector? GetDetector(string provider) =>
        _providers.GetDetector(provider);

    private static OpenAIPromptExecutionSettings BuildExecutionSettings(ModelParameters? p)
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

        return settings;
    }

    private sealed class AgentInstance(
        string id, string provider, string model,
        Kernel kernel, ChatHistory history,
        ModelParameters? parameters = null)
    {
        public string Id => id;
        public string Provider => provider;
        public string Model => model;
        public Kernel Kernel => kernel;
        public ChatHistory History => history;
        public ModelParameters? Parameters => parameters;
        public int TurnCount { get; set; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    }
}

public record AgentInfo(string Id, string Provider, string Model, int TurnCount, DateTimeOffset CreatedAt);
