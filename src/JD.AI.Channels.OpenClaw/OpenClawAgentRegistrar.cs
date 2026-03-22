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
    internal const string OpenClawDefaultAgentId = "main";

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

            // Snapshot before mutation for backup
            var preModificationRaw = configNode.ToJsonString(IndentedJson);

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

            await WriteConfigAsync(configNode, baseHash, preModificationRaw, ct);

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
    public async Task UnregisterAgentsAsync(
        IEnumerable<string>? managedAgentIds = null,
        CancellationToken ct = default)
    {
        if (!_rpc.IsConnected)
            return;

        try
        {
            var (configNode, baseHash) = await ReadConfigAsync(ct);
            if (configNode is null)
                return;

            // Snapshot before mutation for backup
            var preModificationRaw = configNode.ToJsonString(IndentedJson);

            var (removedAgents, removedBindings) = RemoveManagedAgentsAndBindings(configNode, managedAgentIds);
            var defaultAgentRecovered = EnsureDefaultMainAgent(configNode);
            if (removedAgents > 0)
            {
                _logger.LogInformation(
                    "Unregistered {Count} JD.AI agent(s) from OpenClaw config",
                    removedAgents);
            }

            if (defaultAgentRecovered)
            {
                _logger.LogInformation(
                    "Recovered OpenClaw default agent '{AgentId}' after JD.AI cleanup",
                    OpenClawDefaultAgentId);
            }

            if (baseHash is not null && (removedAgents > 0 || removedBindings > 0 || defaultAgentRecovered))
                await WriteConfigAsync(configNode, baseHash, preModificationRaw, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error unregistering agents (may already be removed)");
        }

        _registeredAgentIds.Clear();
    }

    /// <summary>
    /// Removes JD.AI-managed agents/bindings (prefixed with <see cref="AgentIdPrefix"/>)
    /// from a parsed OpenClaw config document.
    /// </summary>
    internal static (int RemovedAgents, int RemovedBindings) RemoveManagedAgentsAndBindings(JsonNode configNode) =>
        RemoveManagedAgentsAndBindings(configNode, managedAgentIds: null);

    /// <summary>
    /// Removes JD.AI-managed agents/bindings from a parsed OpenClaw config document.
    /// Managed entries are those with the JD.AI prefix and any explicit IDs supplied.
    /// </summary>
    internal static (int RemovedAgents, int RemovedBindings) RemoveManagedAgentsAndBindings(
        JsonNode configNode,
        IEnumerable<string>? managedAgentIds)
    {
        ArgumentNullException.ThrowIfNull(configNode);

        var explicitManagedIds = managedAgentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        static string NormalizeId(string id) => id.Trim();
        var hasExplicitManagedIds = explicitManagedIds is { Count: > 0 };
        bool IsManaged(string? agentId)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                return false;

            var normalized = NormalizeId(agentId);
            return normalized.StartsWith(AgentIdPrefix, StringComparison.Ordinal)
                || (hasExplicitManagedIds && explicitManagedIds!.Contains(normalized));
        }

        var removedAgents = 0;
        var removedBindings = 0;

        var list = configNode["agents"]?["list"]?.AsArray();
        if (list is not null)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var id = list[i]?["id"]?.GetValue<string>();
                if (IsManaged(id))
                {
                    list.RemoveAt(i);
                    removedAgents++;
                }
            }

            if (list.Count == 0)
                configNode["agents"]!.AsObject().Remove("list");
        }

        var bindings = configNode["bindings"]?.AsArray();
        if (bindings is not null)
        {
            for (var i = bindings.Count - 1; i >= 0; i--)
            {
                var agentId = bindings[i]?["agentId"]?.GetValue<string>();
                if (IsManaged(agentId))
                {
                    bindings.RemoveAt(i);
                    removedBindings++;
                }
            }

            if (bindings.Count == 0)
                configNode.AsObject().Remove("bindings");
        }

        return (removedAgents, removedBindings);
    }

    internal static bool EnsureDefaultMainAgent(JsonNode configNode)
    {
        ArgumentNullException.ThrowIfNull(configNode);

        var root = configNode.AsObject();
        if (root["agents"] is not JsonObject agentsObject)
        {
            agentsObject = new JsonObject();
            root["agents"] = agentsObject;
        }

        if (agentsObject["list"] is not JsonArray list)
        {
            list = [];
            agentsObject["list"] = list;
        }

        var mainAgentIndex = -1;
        var hasAnyAgent = false;
        var hasDefault = false;

        for (var i = 0; i < list.Count; i++)
        {
            var node = list[i];
            if (node is not JsonObject agent)
                continue;

            var id = agent["id"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            hasAnyAgent = true;

            if (string.Equals(id, OpenClawDefaultAgentId, StringComparison.OrdinalIgnoreCase))
                mainAgentIndex = i;

            if (agent["default"]?.GetValue<bool>() == true)
                hasDefault = true;
        }

        var changed = false;

        if (!hasAnyAgent || mainAgentIndex < 0)
        {
            list.Add(new JsonObject
            {
                ["id"] = OpenClawDefaultAgentId,
                ["name"] = "Assistant",
                ["default"] = !hasDefault,
            });
            mainAgentIndex = list.Count - 1;
            changed = true;
        }

        if (!hasDefault && mainAgentIndex >= 0 && list[mainAgentIndex] is JsonObject mainAgent)
        {
            mainAgent["default"] = true;
            changed = true;
        }

        return changed;
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

        // Snapshot before mutation for backup
        var preModificationRaw = configNode.ToJsonString(IndentedJson);

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

        await WriteConfigAsync(configNode, baseHash, preModificationRaw, ct);
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
    /// <param name="config">The modified config to write.</param>
    /// <param name="baseHash">Hash from the original <c>config.get</c> for optimistic concurrency.</param>
    /// <param name="preModificationRaw">
    /// Serialized JSON of the config <b>before</b> mutation, used for backup.
    /// Callers should serialize the config immediately after reading it, before any changes.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    internal async Task WriteConfigAsync(
        JsonNode config, string baseHash, string? preModificationRaw, CancellationToken ct)
    {
        // Backup the pre-modification config (no extra RPC needed)
        if (preModificationRaw is not null)
        {
            try
            {
                var backupDir = ConfigBackupDirectory;
                Directory.CreateDirectory(backupDir);
                var backupFile = Path.Combine(backupDir,
                    $"config-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
                await File.WriteAllTextAsync(backupFile, preModificationRaw, ct);

                // Keep only last 10 backups
                var backups = Directory.GetFiles(backupDir, "config-*.json")
                    .OrderByDescending(f => f)
                    .Skip(10)
                    .ToArray();
                foreach (var old in backups)
                    File.Delete(old);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backup config before write — proceeding anyway");
            }
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
