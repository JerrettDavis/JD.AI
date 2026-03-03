using JD.AI.Workflows.Steps;
using Microsoft.SemanticKernel;
using WorkflowFramework;
using WorkflowFramework.Builder;

namespace JD.AI.Workflows;

/// <summary>
/// Builds executable workflows from <see cref="AgentWorkflowDefinition"/> definitions,
/// wiring steps to a Semantic Kernel.
/// </summary>
public sealed class AgentWorkflowBuilder
{
    private readonly Kernel _kernel;

    public AgentWorkflowBuilder(Kernel kernel) => _kernel = kernel;

    /// <summary>Builds an executable workflow from a definition.</summary>
    public IWorkflow<AgentWorkflowData> Build(AgentWorkflowDefinition definition)
    {
        var builder = Workflow.Create<AgentWorkflowData>(definition.Name);

        foreach (var stepDef in definition.Steps)
            builder = AddStep(builder, stepDef);

        return builder.Build();
    }

    /// <summary>Builds with event capture for observability.</summary>
    public IWorkflow<AgentWorkflowData> BuildWithCapture(
        AgentWorkflowDefinition definition,
        WorkflowExecutionCapture capture)
    {
        var builder = Workflow.Create<AgentWorkflowData>(definition.Name);

        foreach (var stepDef in definition.Steps)
            builder = AddStep(builder, stepDef);

        builder = builder.WithEvents(capture);
        return builder.Build();
    }

    private static IWorkflowBuilder<AgentWorkflowData> AddStep(
        IWorkflowBuilder<AgentWorkflowData> builder,
        AgentStepDefinition stepDef)
    {
        return stepDef.Kind switch
        {
            AgentStepKind.Skill => builder.Step(new RunSkillStep(
                stepDef.Name,
                stepDef.Target ?? stepDef.Name)),

            AgentStepKind.Tool when stepDef.Target?.Contains('.') == true =>
                builder.Step(new InvokeToolStep(
                    stepDef.Name,
                    stepDef.Target.Split('.')[0],
                    stepDef.Target.Split('.')[1])),

            AgentStepKind.Tool => builder.Step(new RunSkillStep(
                stepDef.Name,
                stepDef.Target ?? stepDef.Name)),

            _ => builder,
        };
    }

    /// <summary>Creates initial workflow data with the prompt and kernel set.</summary>
    public AgentWorkflowData CreateData(string prompt) =>
        new() { Prompt = prompt, Kernel = _kernel };
}
