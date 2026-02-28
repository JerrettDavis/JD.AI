using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>
/// SK kernel plugin injected into subagents running within a team,
/// providing access to the shared scratchpad and event stream.
/// </summary>
public sealed class TeamContextTools
{
    private readonly TeamContext _context;
    private readonly string _agentName;

    public TeamContextTools(TeamContext context, string agentName)
    {
        _context = context;
        _agentName = agentName;
    }

    [KernelFunction("read_scratchpad")]
    [Description("Read a value from the team's shared scratchpad. Returns null if key not found.")]
    public string ReadScratchpad(
        [Description("The key to read")] string key) =>
        _context.ReadScratchpad(key) ?? "(not found)";

    [KernelFunction("write_scratchpad")]
    [Description("Write a value to the team's shared scratchpad for other agents to see.")]
    public string WriteScratchpad(
        [Description("The key to write")] string key,
        [Description("The value to store")] string value)
    {
        _context.WriteScratchpad(key, value);
        _context.RecordEvent(_agentName, AgentEventType.Decision, $"Wrote scratchpad: {key}");
        return $"Stored '{key}' in scratchpad.";
    }

    [KernelFunction("log_finding")]
    [Description("Log a finding or observation to the team event stream for other agents to see.")]
    public string LogFinding(
        [Description("The finding or observation")] string content)
    {
        _context.RecordEvent(_agentName, AgentEventType.Finding, content);
        return "Finding logged.";
    }

    [KernelFunction("get_event_log")]
    [Description("Get the team's recent event log to see what other agents have done.")]
    public string GetEventLog()
    {
        var events = _context.GetEventsSnapshot();
        if (events.Count == 0)
            return "No events recorded yet.";

        var recent = events.TakeLast(20).ToList();
        var lines = recent.Select(e =>
            $"[{e.Timestamp:HH:mm:ss}] {e.AgentName} ({e.EventType}): {e.Content}");
        return string.Join('\n', lines);
    }

    [KernelFunction("get_team_goal")]
    [Description("Get the high-level goal this team is working toward.")]
    public string GetTeamGoal() => _context.Goal;

    [KernelFunction("get_agent_result")]
    [Description("Get a completed agent's output by name.")]
    public string GetAgentResult(
        [Description("Name of the agent whose result to retrieve")] string agentName)
    {
        var result = _context.GetResult(agentName);
        return result?.Output ?? $"Agent '{agentName}' has not completed yet.";
    }
}
