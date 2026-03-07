using JD.AI.Core.Agents;

namespace JD.AI.Specs.Support.Actors;

/// <summary>
/// Encapsulates user interactions with the agent session for BDD scenarios.
/// </summary>
public sealed class UserActor
{
    private readonly AgentSession _session;
    private readonly AgentLoop _loop;

    public UserActor(AgentSession session)
    {
        _session = session;
        _loop = new AgentLoop(session);
    }

    public AgentSession Session => _session;
    public AgentLoop Loop => _loop;
    public string? LastResponse { get; private set; }
    public Exception? LastException { get; private set; }

    public async Task SendMessageAsync(string message)
    {
        try
        {
            LastResponse = await _loop.RunTurnAsync(message);
            LastException = null;
        }
        catch (Exception ex)
        {
            LastException = ex;
            LastResponse = null;
        }
    }

    public async Task SendMessageStreamingAsync(string message)
    {
        try
        {
            LastResponse = await _loop.RunTurnStreamingAsync(message);
            LastException = null;
        }
        catch (Exception ex)
        {
            LastException = ex;
            LastResponse = null;
        }
    }
}
