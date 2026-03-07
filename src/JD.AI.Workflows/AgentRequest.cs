namespace JD.AI.Workflows;

/// <summary>
/// Represents a request processed by the agent that may require workflow orchestration.
/// </summary>
public record AgentRequest(
    /// <summary>The user's message or prompt.</summary>
    string Message,
    /// <summary>Optional session ID that originated the request.</summary>
    string? SessionId = null);
