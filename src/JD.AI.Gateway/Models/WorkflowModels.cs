namespace JD.AI.Gateway.Models;

public record WorkflowRunRequest(string WorkflowName, string? Version, string Prompt, string? SessionId);
public record WorkflowClassifyRequest(string Prompt);
public record WorkflowProcessRequest(string Prompt);
