using System.Text.Json;
using System.Text.Json.Nodes;
using JD.AI.Core.Config;
using JD.AI.Core.Infrastructure;
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

    /// <summary>Model identifier (e.g., "anthropic/claude-opus-4-6").</summary>
    public string? Model { get; init; }

    /// <summary>Tools available to this agent.</summary>
    public IList<string> Tools { get; init; } = [];

    /// <summary>Channel bindings: which OpenClaw channels route to this agent.</summary>
    public IList<AgentBinding> Bindings { get; init; } = [];
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
/// <para>
/// OpenClaw uses optimistic-concurrency whole-config replacement:
/// <list type="number">
///   <item><c>config.get</c> (no params) → returns <c>{ raw, hash, ... }</c></item>
///   <item>Parse raw JSON, modify <c>agents.list</c> array</item>
///   <item><c>config.set</c> with <c>{ raw, baseHash }</c> → atomic write if hash matches</item>
/// </list>
/// </para>
///
/// JD.AI-managed agents are identified by an ID prefix (<c>jdai-</c>).
/// </summary>
public sealed class OpenClawAgentRegistrar
{
    /// <summary>Prefix used to identify JD.AI-managed agents in OpenClaw config.</summary>
    public const string AgentIdPrefix = "jdai-";

    /// <summary>Directory for config backups before writes.</summary>
    public static string ConfigBackupDirectory =>
        Path.Combine(DataDirectories.Root, "openclaw-config-backups");

    private static readonly JsonSerializerOptions IndentedJson = JsonDefaults.Indented;

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

        var agentList = agents.ToList();
        if (agentList.Count == 0)
            return;

        try
        {
            // Read full config + hash
            var (configNode, baseHash) = await ReadConfigAsync(ct);
            if (configNode is null)
            {
                _logger.LogError("Failed to read OpenClaw config — cannot register agents");
                return;
            }

            // Ensure agents.list exists
            EnsureAgentsList(configNode);
            var list = configNode["agents"]!["list"]!.AsArray();

            var successfulIds = new List<string>();
            foreach (var agent in agentList)
            {
                try
                {
                    AddOrUpdateAgent(list, agent);
                    successfulIds.Add(agent.Id);
                    _logger.LogInformation(
                        "Prepared JD.AI agent '{Id}' ({Name}) for registration",
                        agent.Id, agent.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to prepare agent '{Id}'", agent.Id);
                }
            }

            // Write the updated config atomically
            if (baseHash is null)
            {
                _logger.LogError("OpenClaw config hash is null — cannot write safely");
                return;
            }

            await WriteConfigAsync(configNode, baseHash, ct);

            // Only track as registered AFTER successful write
            _registeredAgentIds.AddRange(successfulIds);

            // Register channel bindings for the agents
            await RegisterBindingsAsync(agentList, ct);

            _logger.LogInformation(
                "Registered {Count} JD.AI agent(s) with OpenClaw",
                _registeredAgentIds.Count);

            // Ensure workspace directories exist
            foreach (var agent in agentList.Where(a => _registeredAgentIds.Contains(a.Id)))
            {
                EnsureWorkspaceDirectory(agent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agents with OpenClaw");
        }
    }

    /// <summary>
    /// Removes previously registered JD.AI agents from the OpenClaw gateway.
    /// </summary>
    public async Task UnregisterAgentsAsync(CancellationToken ct = default)
    {
        if (!_rpc.IsConnected || _registeredAgentIds.Count == 0)
            return;

        try
        {
            var (configNode, baseHash) = await ReadConfigAsync(ct);
            if (configNode is null)
                return;

            var list = configNode["agents"]?["list"]?.AsArray();
            if (list is null)
            {
                _registeredAgentIds.Clear();
                return;
            }

            // Remove all JD.AI-managed agents (by prefix)
            var removed = 0;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var id = list[i]?["id"]?.GetValue<string>();
                if (id is not null && id.StartsWith(AgentIdPrefix, StringComparison.Ordinal))
                {
                    list.RemoveAt(i);
                    removed++;
                    _logger.LogInformation("Unregistered agent '{Id}' from OpenClaw", id);
                }
            }

            // Remove JD.AI-managed bindings
            var bindings = configNode["bindings"]?.AsArray();
            if (bindings is not null)
            {
                for (var i = bindings.Count - 1; i >= 0; i--)
                {
                    var agentId = bindings[i]?["agentId"]?.GetValue<string>();
                    if (agentId is not null && agentId.StartsWith(AgentIdPrefix, StringComparison.Ordinal))
                        bindings.RemoveAt(i);
                }

                if (bindings.Count == 0)
                    configNode.AsObject().Remove("bindings");
            }

            // Remove empty list to keep config clean
            if (list.Count == 0)
                configNode["agents"]!.AsObject().Remove("list");

            if (baseHash is not null && removed > 0)
                await WriteConfigAsync(configNode, baseHash, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error unregistering agents (may already be removed)");
        }

        _registeredAgentIds.Clear();
    }

    /// <summary>Gets the list of registered JD.AI agent IDs.</summary>
    public IReadOnlyList<string> RegisteredAgentIds => _registeredAgentIds;

    /// <summary>
    /// Registers channel bindings so OpenClaw routes messages to JD.AI agents.
    /// Bindings are top-level in the OpenClaw config: <c>bindings: [{ agentId, match: { channel, ... } }]</c>.
    /// </summary>
    private async Task RegisterBindingsAsync(
        IEnumerable<JdAiAgentDefinition> agents, CancellationToken ct)
    {
        var allBindings = agents
            .Where(a => a.Bindings.Count > 0 && _registeredAgentIds.Contains(a.Id))
            .SelectMany(a => a.Bindings.Select(b => (a.Id, Binding: b)))
            .ToList();

        if (allBindings.Count == 0)
            return;

        var (configNode, baseHash) = await ReadConfigAsync(ct);
        if (configNode is null)
            return;

        // Ensure top-level bindings array
        if (configNode["bindings"] is null)
            configNode["bindings"] = new JsonArray();
        var bindingsArray = configNode["bindings"]!.AsArray();

        // Remove existing JD.AI bindings (by agentId prefix)
        for (var i = bindingsArray.Count - 1; i >= 0; i--)
        {
            var agentId = bindingsArray[i]?["agentId"]?.GetValue<string>();
            if (agentId is not null && agentId.StartsWith(AgentIdPrefix, StringComparison.Ordinal))
            {
                bindingsArray.RemoveAt(i);
            }
        }

        // Add new bindings
        foreach (var (agentId, binding) in allBindings)
        {
            var matchNode = new JsonObject { ["channel"] = binding.Channel };

            if (binding.AccountId is not null)
                matchNode["accountId"] = binding.AccountId;

            if (binding.Peer is not null)
                matchNode["peer"] = new JsonObject
                {
                    ["kind"] = binding.Peer.Kind,
                    ["id"] = binding.Peer.Id,
                };

            bindingsArray.Add(new JsonObject
            {
                ["agentId"] = agentId,
                ["match"] = matchNode,
            });

            _logger.LogInformation(
                "Added binding: {Channel} → agent '{AgentId}'",
                binding.Channel, agentId);
        }

        if (baseHash is null)
            return;

        await WriteConfigAsync(configNode, baseHash, ct);
    }

    /// <summary>
    /// Reads the full OpenClaw config and its hash for optimistic concurrency.
    /// </summary>
    internal async Task<(JsonNode? Config, string? Hash)> ReadConfigAsync(CancellationToken ct)
    {
        var response = await _rpc.RequestAsync("config.get", null, ct);
        if (!response.Ok || !response.Payload.HasValue)
        {
            _logger.LogWarning("config.get failed: {Error}",
                response.Error?.GetProperty("message").GetString() ?? "unknown");
            return (null, null);
        }

        var raw = response.Payload.Value.GetProperty("raw").GetString()!;
        var hash = response.Payload.Value.GetProperty("hash").GetString()!;
        var configNode = JsonNode.Parse(raw);

        return (configNode, hash);
    }

    /// <summary>
    /// Writes the full config back to OpenClaw with optimistic concurrency.
    /// </summary>
    internal async Task WriteConfigAsync(JsonNode config, string baseHash, CancellationToken ct)
    {
        // Backup current config before overwriting
        try
        {
            var (currentConfig, _) = await ReadConfigAsync(ct);
            if (currentConfig is not null)
            {
                var backupDir = ConfigBackupDirectory;
                Directory.CreateDirectory(backupDir);
                var backupFile = Path.Combine(backupDir,
                    $"config-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
                await File.WriteAllTextAsync(backupFile,
                    currentConfig.ToJsonString(IndentedJson), ct);

                // Keep only last 10 backups
                var backups = Directory.GetFiles(backupDir, "config-*.json")
                    .OrderByDescending(f => f)
                    .Skip(10)
                    .ToArray();
                foreach (var old in backups)
                    File.Delete(old);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to backup config before write — proceeding anyway");
        }

        var raw = config.ToJsonString(IndentedJson);
        var response = await _rpc.RequestAsync("config.set", new { raw, baseHash }, ct);

        if (!response.Ok)
        {
            var errorMsg = response.Error?.GetProperty("message").GetString() ?? "unknown";
            throw new InvalidOperationException($"config.set failed: {errorMsg}");
        }

        _logger.LogDebug("OpenClaw config updated successfully");
    }

    private static void EnsureAgentsList(JsonNode config)
    {
        if (config["agents"] is null)
            config["agents"] = new JsonObject();
        if (config["agents"]!["list"] is null)
            config["agents"]!["list"] = new JsonArray();
    }

    private void AddOrUpdateAgent(JsonArray list, JdAiAgentDefinition agent)
    {
        // Build the new entry FIRST — if this throws, the list is untouched
        var entry = new JsonObject
        {
            ["id"] = agent.Id,
            ["name"] = string.IsNullOrEmpty(agent.Name) ? $"JD.AI: {agent.Id}" : agent.Name,
            ["workspace"] = DataDirectories.OpenClawWorkspace(agent.Id),
            ["identity"] = new JsonObject
            {
                ["name"] = string.IsNullOrEmpty(agent.Name) ? agent.Id : agent.Name,
                ["emoji"] = agent.Emoji,
                ["theme"] = agent.Theme,
            },
        };

        // Per-agent model is a flat string in OpenClaw (not {primary: "..."} like defaults)
        if (agent.Model is not null)
        {
            entry["model"] = agent.Model;
        }

        // Now that entry is fully built, replace in-place or append
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i]?["id"]?.GetValue<string>(), agent.Id, StringComparison.Ordinal))
            {
                list[i] = entry; // Atomic replace — no window where entry is missing
                _logger.LogDebug("Replaced existing agent '{Id}' in OpenClaw", agent.Id);
                return;
            }
        }

        // No existing entry — append
        list.Add(entry);
    }

    private void EnsureWorkspaceDirectory(JdAiAgentDefinition agent)
    {
        var workspacePath = DataDirectories.OpenClawWorkspace(agent.Id);

        if (Directory.Exists(workspacePath))
            return;

        Directory.CreateDirectory(workspacePath);

        var agentsMdPath = Path.Combine(workspacePath, "AGENTS.md");
        if (!File.Exists(agentsMdPath))
        {
            var agentsMd = $"""
                # {(string.IsNullOrEmpty(agent.Name) ? agent.Id : agent.Name)}

                This agent is managed by JD.AI Gateway.
                Messages routed to this agent are processed by JD.AI's Semantic Kernel runtime.

                {(agent.SystemPrompt is not null ? $"## System Instructions\n\n{agent.SystemPrompt}" : "")}
                """;
            File.WriteAllText(agentsMdPath, agentsMd);
        }
    }
}
