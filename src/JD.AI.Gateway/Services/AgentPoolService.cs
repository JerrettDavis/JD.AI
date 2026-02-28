using System.Collections.Concurrent;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

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

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct)
    {
        _agents.Clear();
        return Task.CompletedTask;
    }

    public IReadOnlyList<AgentInfo> ListAgents() =>
        _agents.Values.Select(a => new AgentInfo(a.Id, a.Provider, a.Model, a.TurnCount, a.CreatedAt)).ToList();

    public async Task<string> SpawnAgentAsync(
        string provider, string model, string? systemPrompt, CancellationToken ct)
    {
        var allProviders = await _providers.DetectProvidersAsync(ct);
        var providerInfo = allProviders.FirstOrDefault(p =>
            p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Provider '{provider}' not found or not available.");

        var modelInfo = providerInfo.Models.FirstOrDefault(m =>
            m.Id.Equals(model, StringComparison.OrdinalIgnoreCase)
            || m.DisplayName.Equals(model, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Model '{model}' not found in provider '{provider}'.");

        var detector = _providers.GetDetector(provider)
            ?? throw new InvalidOperationException($"No detector for provider '{provider}'.");

        var kernel = detector.BuildKernel(modelInfo);
        var history = new ChatHistory();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            history.AddSystemMessage(systemPrompt);

        var id = Guid.NewGuid().ToString("N")[..12];
        var instance = new AgentInstance(id, provider, model, kernel, history);
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
        var response = await chat.GetChatMessageContentAsync(agent.History, cancellationToken: ct);
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

    private sealed class AgentInstance(
        string id, string provider, string model,
        Kernel kernel, ChatHistory history)
    {
        public string Id => id;
        public string Provider => provider;
        public string Model => model;
        public Kernel Kernel => kernel;
        public ChatHistory History => history;
        public int TurnCount { get; set; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    }
}

public record AgentInfo(string Id, string Provider, string Model, int TurnCount, DateTimeOffset CreatedAt);
