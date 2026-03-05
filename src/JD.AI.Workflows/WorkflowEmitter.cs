using System.Text;
using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Workflows;

/// <summary>
/// Emits workflow definitions in JSON, C# builder, or Mermaid diagram format.
/// </summary>
public sealed class WorkflowEmitter : IWorkflowEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    public WorkflowArtifact Emit(
        AgentWorkflowDefinition definition,
        WorkflowExportFormat format = WorkflowExportFormat.Json)
    {
        var content = format switch
        {
            WorkflowExportFormat.Json => EmitJson(definition),
            WorkflowExportFormat.CSharp => EmitCSharp(definition),
            WorkflowExportFormat.Mermaid => EmitMermaid(definition),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        return new WorkflowArtifact(definition.Name, format, content, DateTime.UtcNow);
    }

    private static string EmitJson(AgentWorkflowDefinition definition) =>
        JsonSerializer.Serialize(definition, JsonOptions);

    private static string EmitCSharp(AgentWorkflowDefinition definition)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Auto-generated workflow: {definition.Name} v{definition.Version}");
        sb.AppendLine($"var workflow = Workflow.Create<AgentWorkflowData>(\"{definition.Name}\")");

        foreach (var step in definition.Steps)
        {
            sb.Append(step.Kind switch
            {
                AgentStepKind.Skill => $"    .Step(new RunSkillStep(\"{step.Name}\", \"{step.Target}\"))",
                AgentStepKind.Tool => $"    .Step(new InvokeToolStep(\"{step.Name}\", \"{step.Target?.Split('.').FirstOrDefault()}\", \"{step.Target?.Split('.').ElementAtOrDefault(1)}\"))",
                _ => $"    // {step.Kind}: {step.Name}",
            });
            sb.AppendLine();
        }

        sb.AppendLine("    .Build();");
        return sb.ToString();
    }

    private static string EmitMermaid(AgentWorkflowDefinition definition)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        for (var i = 0; i < definition.Steps.Count; i++)
        {
            var step = definition.Steps[i];
            var id = $"S{i}";
            var shape = step.Kind switch
            {
                AgentStepKind.Conditional => $"{{{{{step.Name}}}}}",
                AgentStepKind.Loop => $"(({step.Name}))",
                _ => $"[{step.Name}]",
            };

            sb.AppendLine($"    {id}{shape}");

            if (i > 0)
                sb.AppendLine($"    S{i - 1} --> {id}");
        }

        return sb.ToString();
    }
}
