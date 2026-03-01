using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// Defines a JD.AI agent to register with the OpenClaw gateway.
/// These agents appear in the OpenClaw dashboard alongside native agents.
/// </summary>
public sealed class JdAiAgentDefinition
{
    /// <summary>Unique agent ID (appears in OpenClaw as the agent identifier).</summary>
    public required string Id { get; init; }

    /// <summary>Display name in the OpenClaw UI.</summary>
    public string Name { get; init; } = "";

    /// <summary>Emoji identifier for the agent.</summary>
    public string Emoji { get; init; } = "🤖";

    /// <summary>Theme/persona description.</summary>
    public string Theme { get; init; } = "JD.AI agent";

    /// <summary>System prompt for the agent.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Model identifier (e.g., "ollama/llama3.2").</summary>
    public string? Model { get; init; }

    /// <summary>Tools available to this agent.</summary>
    public List<string> Tools { get; init; } = [];

    /// <summary>Channel bindings: which OpenClaw channels route to this agent.</summary>
    public List<AgentBinding> Bindings { get; init; } = [];
}

/// <summary>Maps an OpenClaw channel/peer to this agent.</summary>
public sealed class AgentBinding
{
    /// <summary>Channel type (e.g., "discord", "signal", "telegram").</summary>
    public required string Channel { get; init; }

    /// <summary>Optional account ID within the channel.</summary>
    public string? AccountId { get; init; }

    /// <summary>Optional peer (direct/group/channel) to bind to.</summary>
    public AgentBindingPeer? Peer { get; init; }

    /// <summary>Optional guild/server ID (Discord).</summary>
    public string? GuildId { get; init; }
}

/// <summary>Peer targeting for bindings.</summary>
public sealed class AgentBindingPeer
{
    public string Kind { get; init; } = "direct"; // direct | group | channel
    public required string Id { get; init; }
}

/// <summary>
/// Registers JD.AI agents with an OpenClaw gateway via config RPC so they appear
/// as native agents in the OpenClaw dashboard alongside OpenClaw's own agents.
///
/// Uses OpenClaw's config.get/config.set RPC methods to:
/// 1. Add JD.AI agents to <c>agents.list</c>
/// 2. Create channel bindings that route to JD.AI agents
/// 3. Clean up registrations on shutdown
/// </summary>
public sealed class OpenClawAgentRegistrar
{
    private readonly OpenClawRpcClient _rpc;
    private readonly ILogger<OpenClawAgentRegistrar> _logger;
    private readonly List<string> _registeredAgentIds = [];

    public OpenClawAgentRegistrar(OpenClawRpcClient rpc, ILogger<OpenClawAgentRegistrar> logger)
    {
        _rpc = rpc;
        _logger = logger;
    }

    /// <summary>
    /// Registers JD.AI agents with the OpenClaw gateway so they appear in the dashboard.
    /// </summary>
    public async Task RegisterAgentsAsync(
        IEnumerable<JdAiAgentDefinition> agents,
        CancellationToken ct = default)
    {
        if (!_rpc.IsConnected)
        {
            _logger.LogWarning("Cannot register agents — not connected to OpenClaw");
            return;
        }

        // Get current agents.list
        var currentList = await GetAgentListAsync(ct);

        foreach (var agent in agents)
        {
            try
            {
                await RegisterSingleAgentAsync(agent, currentList, ct);
                _registeredAgentIds.Add(agent.Id);
                _logger.LogInformation(
                    "Registered JD.AI agent '{Id}' ({Name}) with OpenClaw",
                    agent.Id, agent.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register agent '{Id}' with OpenClaw", agent.Id);
            }
        }

        // Register bindings
        foreach (var agent in agents.Where(a => a.Bindings.Count > 0))
        {
            try
            {
                await RegisterBindingsAsync(agent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register bindings for agent '{Id}'", agent.Id);
            }
        }
    }

    /// <summary>
    /// Removes previously registered JD.AI agents from the OpenClaw gateway.
    /// </summary>
    public async Task UnregisterAgentsAsync(CancellationToken ct = default)
    {
        if (!_rpc.IsConnected || _registeredAgentIds.Count == 0)
            return;

        foreach (var agentId in _registeredAgentIds)
        {
            try
            {
                await RemoveAgentAsync(agentId, ct);
                _logger.LogInformation("Unregistered JD.AI agent '{Id}' from OpenClaw", agentId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error unregistering agent '{Id}' (may already be removed)", agentId);
            }
        }

        // Remove bindings pointing to JD.AI agents
        try
        {
            await RemoveBindingsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error cleaning up bindings");
        }

        _registeredAgentIds.Clear();
    }

    /// <summary>Gets the list of registered JD.AI agent IDs.</summary>
    public IReadOnlyList<string> RegisteredAgentIds => _registeredAgentIds;

    private async Task RegisterSingleAgentAsync(
        JdAiAgentDefinition agent,
        JsonElement? currentList,
        CancellationToken ct)
    {
        // Build the agent entry for OpenClaw's agents.list
        var agentEntry = new Dictionary<string, object?>
        {
            ["id"] = agent.Id,
            ["name"] = string.IsNullOrEmpty(agent.Name) ? $"JD.AI: {agent.Id}" : agent.Name,
            ["identity"] = new Dictionary<string, object>
            {
                ["name"] = string.IsNullOrEmpty(agent.Name) ? agent.Id : agent.Name,
                ["emoji"] = agent.Emoji,
                ["theme"] = agent.Theme,
            },
            // Use a workspace path under the JD.AI state directory
            ["workspace"] = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".jdai", "openclaw-workspaces", agent.Id),
            // Mark as JD.AI-managed so we can identify and clean up later
            ["_jdaiManaged"] = true,
        };

        if (agent.Model is not null)
            agentEntry["model"] = agent.Model;

        if (agent.Tools.Count > 0)
        {
            agentEntry["tools"] = new Dictionary<string, object>
            {
                ["allow"] = agent.Tools,
            };
        }

        // Check if agent already exists in the list
        var exists = false;
        if (currentList.HasValue && currentList.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var existing in currentList.Value.EnumerateArray())
            {
                if (existing.TryGetProperty("id", out var id) &&
                    id.GetString() == agent.Id)
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            _logger.LogDebug("Agent '{Id}' already exists in OpenClaw — updating", agent.Id);
        }

        // Use config.set to add/update the agent
        // OpenClaw config RPC supports dot-path notation
        await SetConfigAsync($"agents.list.{agent.Id}", agentEntry, ct);

        // Ensure workspace directory exists
        var workspacePath = (string)agentEntry["workspace"]!;
        if (!Directory.Exists(workspacePath))
        {
            Directory.CreateDirectory(workspacePath);

            // Create a minimal AGENTS.md for the workspace
            var agentsMdPath = Path.Combine(workspacePath, "AGENTS.md");
            if (!File.Exists(agentsMdPath))
            {
                var agentsMd = $"""
                    # {agent.Name ?? agent.Id}

                    This agent is managed by JD.AI Gateway.
                    Messages routed to this agent are processed by JD.AI's Semantic Kernel runtime.

                    {(agent.SystemPrompt is not null ? $"## System Instructions\n\n{agent.SystemPrompt}" : "")}
                    """;
                await File.WriteAllTextAsync(agentsMdPath, agentsMd, ct);
            }
        }
    }

    private async Task RegisterBindingsAsync(JdAiAgentDefinition agent, CancellationToken ct)
    {
        foreach (var binding in agent.Bindings)
        {
            var bindingEntry = new Dictionary<string, object?>
            {
                ["agentId"] = agent.Id,
                ["match"] = BuildMatchObject(binding),
                ["_jdaiManaged"] = true,
            };

            // Append to bindings array
            await AppendBindingAsync(bindingEntry, ct);

            _logger.LogDebug(
                "Registered binding: {Channel} → agent '{Agent}'",
                binding.Channel, agent.Id);
        }
    }

    private static Dictionary<string, object?> BuildMatchObject(AgentBinding binding)
    {
        var match = new Dictionary<string, object?> { ["channel"] = binding.Channel };

        if (binding.AccountId is not null)
            match["accountId"] = binding.AccountId;

        if (binding.GuildId is not null)
            match["guildId"] = binding.GuildId;

        if (binding.Peer is not null)
        {
            match["peer"] = new Dictionary<string, object>
            {
                ["kind"] = binding.Peer.Kind,
                ["id"] = binding.Peer.Id,
            };
        }

        return match;
    }

    private async Task RemoveAgentAsync(string agentId, CancellationToken ct)
    {
        await SetConfigAsync($"agents.list.{agentId}", null, ct);
    }

    private async Task RemoveBindingsAsync(CancellationToken ct)
    {
        // Get current bindings and filter out JD.AI-managed ones
        var response = await _rpc.RequestAsync("config.get", new { key = "bindings" }, ct);
        if (!response.Ok || !response.Payload.HasValue)
            return;

        var bindings = response.Payload.Value;
        if (bindings.ValueKind != JsonValueKind.Array)
            return;

        var kept = new List<JsonElement>();
        foreach (var binding in bindings.EnumerateArray())
        {
            // Keep non-JD.AI-managed bindings
            if (!binding.TryGetProperty("_jdaiManaged", out var managed) ||
                !managed.GetBoolean())
            {
                kept.Add(binding);
            }
        }

        // Replace bindings with the filtered list
        await SetConfigAsync("bindings", kept, ct);
    }

    private async Task<JsonElement?> GetAgentListAsync(CancellationToken ct)
    {
        try
        {
            var response = await _rpc.RequestAsync("config.get", new { key = "agents.list" }, ct);
            return response.Ok ? response.Payload : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read agents.list from OpenClaw config");
            return null;
        }
    }

    private async Task SetConfigAsync(string key, object? value, CancellationToken ct)
    {
        var response = await _rpc.RequestAsync("config.set", new { key, value }, ct);
        if (!response.Ok)
        {
            var error = response.Error?.GetProperty("message").GetString() ?? "unknown";
            _logger.LogWarning("config.set '{Key}' failed: {Error}", key, error);
        }
    }

    private async Task AppendBindingAsync(object bindingEntry, CancellationToken ct)
    {
        // Try config.push for array append, fall back to get+set
        try
        {
            var response = await _rpc.RequestAsync("config.push",
                new { key = "bindings", value = bindingEntry }, ct);

            if (response.Ok)
                return;
        }
        catch
        {
            // Fall through to get+set
        }

        // Fallback: get current bindings, append, set
        var getResponse = await _rpc.RequestAsync("config.get", new { key = "bindings" }, ct);
        var current = new List<object>();

        if (getResponse.Ok && getResponse.Payload.HasValue &&
            getResponse.Payload.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in getResponse.Payload.Value.EnumerateArray())
            {
                current.Add(JsonSerializer.Deserialize<object>(item.GetRawText())!);
            }
        }

        current.Add(bindingEntry);
        await SetConfigAsync("bindings", current, ct);
    }
}
