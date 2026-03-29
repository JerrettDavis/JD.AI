using System.Diagnostics;
using JD.AI.Workflows.Steps;
using WorkflowFramework;
using WorkflowFramework.Builder;

namespace JD.AI.Workflows;

/// <summary>
/// Bridges JD.AI workflow definitions to WorkflowFramework's execution engine.
/// Translates AgentWorkflowDefinition into Workflow&lt;AgentWorkflowData&gt; and runs it.
/// </summary>
public sealed class WorkflowBridge : IWorkflowBridge
{
    /// <summary>
    /// Builds a WorkflowFramework workflow from an AgentWorkflowDefinition.
    /// </summary>
    public IWorkflow<AgentWorkflowData> Build(AgentWorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var builder = Workflow.Create<AgentWorkflowData>(definition.Name);

        foreach (var stepDef in definition.Steps)
            builder = AddStep(builder, stepDef);

        return builder.Build();
    }

    /// <summary>
    /// Builds and executes a workflow, returning a bridge result.
    /// </summary>
    public async Task<WorkflowBridgeResult> ExecuteAsync(
        AgentWorkflowDefinition definition,
        AgentWorkflowData data,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(data);

        var sw = Stopwatch.StartNew();
        var errors = new List<string>();

        try
        {
            var workflow = Build(definition);
            var context = new WorkflowContext<AgentWorkflowData>(data, ct);
            var result = await workflow.ExecuteAsync(context).ConfigureAwait(false);

            sw.Stop();

            if (!result.IsSuccess)
            {
                foreach (var err in result.Errors)
                    errors.Add($"[{err.StepName}] {err.Exception.Message}");
            }

            return new WorkflowBridgeResult
            {
                Success = result.IsSuccess,
                FinalOutput = data.FinalResult,
                StepOutputs = new Dictionary<string, string>(
                    data.StepOutputs, StringComparer.Ordinal),
                Errors = errors,
                Duration = sw.Elapsed,
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            errors.Add("Workflow execution was cancelled.");
            return new WorkflowBridgeResult
            {
                Success = false,
                FinalOutput = data.FinalResult,
                StepOutputs = new Dictionary<string, string>(
                    data.StepOutputs, StringComparer.Ordinal),
                Errors = errors,
                Duration = sw.Elapsed,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            errors.Add(ex.Message);
            return new WorkflowBridgeResult
            {
                Success = false,
                FinalOutput = data.FinalResult,
                StepOutputs = new Dictionary<string, string>(
                    data.StepOutputs, StringComparer.Ordinal),
                Errors = errors,
                Duration = sw.Elapsed,
            };
        }
    }

    /// <summary>
    /// Maps an AgentStepDefinition to the appropriate WorkflowFramework step
    /// and adds it to the builder.
    /// </summary>
    internal static IWorkflowBuilder<AgentWorkflowData> AddStep(
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

            AgentStepKind.Agent => builder.Step(new AgentDecisionStep(
                stepDef.Name,
                stepDef.Target ?? "{prompt}",
                stepDef.AllowedPlugins)),

            AgentStepKind.Conditional => AddConditionalStep(builder, stepDef),

            AgentStepKind.Loop => AddLoopStep(builder, stepDef),

            AgentStepKind.Nested => AddNestedStep(builder, stepDef),

            _ => builder, // Unknown kinds are silently skipped
        };
    }

    private static IWorkflowBuilder<AgentWorkflowData> AddConditionalStep(
        IWorkflowBuilder<AgentWorkflowData> builder,
        AgentStepDefinition stepDef)
    {
        // Use WorkflowFramework's If/Then/EndIf.
        // The condition evaluates the Condition string against step outputs.
        var condBuilder = builder.If(ctx => EvaluateCondition(ctx, stepDef.Condition));

        if (stepDef.SubSteps.Count > 0)
        {
            // Then branch: wrap all sub-steps in a single composite step
            var compositeStep = new CompositeStep(
                stepDef.Name, stepDef.SubSteps);
            var elseBuilder = condBuilder.Then(compositeStep);
            return elseBuilder.EndIf();
        }

        // No sub-steps: condition with no-op then branch
        var noOpElse = condBuilder.Then(new NoOpStep(stepDef.Name));
        return noOpElse.EndIf();
    }

    private static IWorkflowBuilder<AgentWorkflowData> AddLoopStep(
        IWorkflowBuilder<AgentWorkflowData> builder,
        AgentStepDefinition stepDef)
    {
        // Implement loop as a custom step that repeatedly executes sub-steps
        // until the condition is met.
        return builder.Step(new LoopStep(stepDef.Name, stepDef.Condition, stepDef.SubSteps));
    }

    private static IWorkflowBuilder<AgentWorkflowData> AddNestedStep(
        IWorkflowBuilder<AgentWorkflowData> builder,
        AgentStepDefinition stepDef)
    {
        // Build a sub-workflow from the sub-steps and run it inline.
        return builder.Step(new NestedWorkflowStep(stepDef.Name, stepDef.SubSteps));
    }

    /// <summary>
    /// Evaluates a condition string against the workflow context.
    /// Supports checking if a key exists in StepOutputs, or simple "true"/"false" literals.
    /// </summary>
    internal static bool EvaluateCondition(
        IWorkflowContext<AgentWorkflowData> context, string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return false;

        // Literal true/false
        if (bool.TryParse(condition, out var literal))
            return literal;

        // Check if condition matches a step output key that has a non-empty value
        if (context.Data.StepOutputs.TryGetValue(condition, out var value))
            return !string.IsNullOrWhiteSpace(value);

        // Check if condition references the final result
        if (condition.Equals("hasFinalResult", StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(context.Data.FinalResult);

        // Check if condition references the prompt
        if (condition.Equals("hasPrompt", StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(context.Data.Prompt);

        return false;
    }
}

/// <summary>
/// A step that does nothing — used as a placeholder in conditional branches.
/// </summary>
internal sealed class NoOpStep : IStep<AgentWorkflowData>
{
    public string Name { get; }

    public NoOpStep(string name) => Name = name;

    public Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context) => Task.CompletedTask;
}

/// <summary>
/// A composite step that executes multiple sub-step definitions sequentially.
/// Used to wrap conditional Then branches containing multiple sub-steps.
/// </summary>
internal sealed class CompositeStep : IStep<AgentWorkflowData>
{
    private readonly IList<AgentStepDefinition> _subStepDefs;

    public string Name { get; }

    public CompositeStep(string name, IList<AgentStepDefinition> subStepDefs)
    {
        Name = name;
        _subStepDefs = subStepDefs;
    }

    public async Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        foreach (var stepDef in _subStepDefs)
        {
            if (context.IsAborted)
                break;

            var step = CreateStep(stepDef);
            await step.ExecuteAsync(context).ConfigureAwait(false);
        }
    }

    private static IStep<AgentWorkflowData> CreateStep(AgentStepDefinition stepDef)
    {
        return stepDef.Kind switch
        {
            AgentStepKind.Skill => new RunSkillStep(
                stepDef.Name, stepDef.Target ?? stepDef.Name),
            AgentStepKind.Tool when stepDef.Target?.Contains('.') == true =>
                new InvokeToolStep(
                    stepDef.Name,
                    stepDef.Target.Split('.')[0],
                    stepDef.Target.Split('.')[1]),
            AgentStepKind.Tool => new RunSkillStep(
                stepDef.Name, stepDef.Target ?? stepDef.Name),
            AgentStepKind.Agent => new AgentDecisionStep(
                stepDef.Name,
                stepDef.Target ?? "{prompt}",
                stepDef.AllowedPlugins),
            _ => new NoOpStep(stepDef.Name),
        };
    }
}

/// <summary>
/// A step that loops executing sub-steps until a condition is met.
/// Includes a safety limit of 100 iterations to prevent infinite loops.
/// </summary>
internal sealed class LoopStep : IStep<AgentWorkflowData>
{
    private readonly string? _condition;
    private readonly IList<AgentStepDefinition> _subStepDefs;
    private const int MaxIterations = 100;

    public string Name { get; }

    public LoopStep(string name, string? condition, IList<AgentStepDefinition> subStepDefs)
    {
        Name = name;
        _condition = condition;
        _subStepDefs = subStepDefs;
    }

    public async Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        var iteration = 0;
        while (iteration < MaxIterations)
        {
            if (context.IsAborted || context.CancellationToken.IsCancellationRequested)
                break;

            // Check if loop-until condition is now true — if so, stop
            if (WorkflowBridge.EvaluateCondition(context, _condition))
                break;

            foreach (var stepDef in _subStepDefs)
            {
                if (context.IsAborted)
                    return;

                var innerBuilder = Workflow.Create<AgentWorkflowData>($"{Name}_iter{iteration}");
                innerBuilder = WorkflowBridge.AddStep(innerBuilder, stepDef);
                var innerWf = innerBuilder.Build();
                await innerWf.ExecuteAsync(context).ConfigureAwait(false);
            }

            iteration++;
        }
    }
}

/// <summary>
/// A step that executes a nested sub-workflow from sub-step definitions.
/// </summary>
internal sealed class NestedWorkflowStep : IStep<AgentWorkflowData>
{
    private readonly IList<AgentStepDefinition> _subStepDefs;

    public string Name { get; }

    public NestedWorkflowStep(string name, IList<AgentStepDefinition> subStepDefs)
    {
        Name = name;
        _subStepDefs = subStepDefs;
    }

    public async Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        var nestedBuilder = Workflow.Create<AgentWorkflowData>(Name);

        foreach (var stepDef in _subStepDefs)
            nestedBuilder = WorkflowBridge.AddStep(nestedBuilder, stepDef);

        var nestedWf = nestedBuilder.Build();
        await nestedWf.ExecuteAsync(context).ConfigureAwait(false);
    }
}
