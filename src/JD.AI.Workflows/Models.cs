namespace JD.AI.Workflows;

/// <summary>
/// Agent-level workflow definition that maps to a <c>WorkflowFramework</c> workflow.
/// This is the serializable, versioned description persisted to the catalog.
/// </summary>
public sealed class AgentWorkflowDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = string.Empty;
    public IList<string> Tags { get; init; } = [];
    public IList<AgentStepDefinition> Steps { get; init; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Step kind within an agent workflow.</summary>
public enum AgentStepKind { Skill, Tool, Nested, Loop, Conditional }

/// <summary>A single step definition within an <see cref="AgentWorkflowDefinition"/>.</summary>
public sealed class AgentStepDefinition
{
    public string Name { get; set; } = string.Empty;
    public AgentStepKind Kind { get; set; }
    public string? Target { get; set; }
    public string? Condition { get; set; }
    public IList<AgentStepDefinition> SubSteps { get; init; } = [];
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public static AgentStepDefinition RunSkill(string name) =>
        new() { Name = name, Kind = AgentStepKind.Skill, Target = name };

    public static AgentStepDefinition InvokeTool(string name) =>
        new() { Name = name, Kind = AgentStepKind.Tool, Target = name };

    public static AgentStepDefinition Nested(string workflowName) =>
        new() { Name = workflowName, Kind = AgentStepKind.Nested, Target = workflowName };

    public static AgentStepDefinition LoopUntil(string condition, params AgentStepDefinition[] subSteps) =>
        new()
        {
            Name = $"Loop until {condition}",
            Kind = AgentStepKind.Loop,
            Condition = condition,
            SubSteps = [.. subSteps],
        };

    public static AgentStepDefinition If(string condition, params AgentStepDefinition[] subSteps) =>
        new()
        {
            Name = $"If {condition}",
            Kind = AgentStepKind.Conditional,
            Condition = condition,
            SubSteps = [.. subSteps],
        };
}

/// <summary>Match result from the workflow catalog.</summary>
public sealed record WorkflowMatchResult(
    AgentWorkflowDefinition Definition,
    float Score,
    string MatchReason);

/// <summary>Exported workflow artifact in a specific DSL format.</summary>
public sealed record WorkflowArtifact(
    string WorkflowName,
    WorkflowExportFormat Format,
    string Content,
    DateTime GeneratedAt);

/// <summary>Supported export formats.</summary>
public enum WorkflowExportFormat { Json, CSharp, Mermaid }
