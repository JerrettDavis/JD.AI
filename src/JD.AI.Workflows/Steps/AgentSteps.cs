using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using WorkflowFramework;

namespace JD.AI.Workflows.Steps;

/// <summary>
/// Typed data that flows through an agent workflow.
/// </summary>
public class AgentWorkflowData
{
    /// <summary>The user's original prompt or instruction.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Accumulated outputs from each step keyed by step name.</summary>
    public IDictionary<string, string> StepOutputs { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The final aggregated result.</summary>
    public string? FinalResult { get; set; }

    /// <summary>Semantic Kernel instance shared across steps.</summary>
    public Kernel? Kernel { get; set; }
}

/// <summary>
/// A WorkflowFramework step that invokes a Semantic Kernel skill (prompt) against
/// the configured LLM with auto-tool-calling enabled.
/// </summary>
public sealed class RunSkillStep : IStep<AgentWorkflowData>
{
    private readonly string _skillPrompt;

    public string Name { get; }

    /// <param name="name">Human-readable step name.</param>
    /// <param name="skillPrompt">
    /// Prompt template for this skill. Use <c>{prompt}</c> to reference
    /// the workflow's input prompt, and <c>{previous}</c> for the prior step's output.
    /// </param>
    public RunSkillStep(string name, string skillPrompt)
    {
        Name = name;
        _skillPrompt = skillPrompt;
    }

    public async Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        var kernel = context.Data.Kernel
            ?? throw new InvalidOperationException("Kernel not set on workflow data.");

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        var prompt = _skillPrompt
            .Replace("{prompt}", context.Data.Prompt, StringComparison.Ordinal)
            .Replace("{previous}", GetPreviousOutput(context), StringComparison.Ordinal);

        history.AddUserMessage(prompt);

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };

        var result = await chat.GetChatMessageContentAsync(
            history, settings, kernel, context.CancellationToken).ConfigureAwait(false);

        var output = result.Content ?? string.Empty;
        context.Data.StepOutputs[Name] = output;
        context.Data.FinalResult = output;
    }

    private static string GetPreviousOutput(IWorkflowContext<AgentWorkflowData> context)
    {
        if (context.Data.StepOutputs.Count == 0)
            return string.Empty;

        return context.Data.StepOutputs.Values.Last();
    }
}

/// <summary>
/// A WorkflowFramework step that invokes a specific Semantic Kernel function by plugin/function name.
/// </summary>
public sealed class InvokeToolStep : IStep<AgentWorkflowData>
{
    private readonly string _pluginName;
    private readonly string _functionName;
    private readonly IDictionary<string, string> _arguments;

    public string Name { get; }

    public InvokeToolStep(string name, string pluginName, string functionName,
        IDictionary<string, string>? arguments = null)
    {
        Name = name;
        _pluginName = pluginName;
        _functionName = functionName;
        _arguments = arguments ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public async Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        var kernel = context.Data.Kernel
            ?? throw new InvalidOperationException("Kernel not set on workflow data.");

        var function = kernel.Plugins
            .GetFunction(_pluginName, _functionName);

        var args = new KernelArguments();
        foreach (var (key, value) in _arguments)
            args[key] = value;

        var result = await kernel.InvokeAsync(function, args, context.CancellationToken)
            .ConfigureAwait(false);

        var output = result.ToString();
        context.Data.StepOutputs[Name] = output;
        context.Data.FinalResult = output;
    }
}

/// <summary>
/// A step that validates a condition against the workflow data and aborts if not met.
/// </summary>
public sealed class ValidateStep : IStep<AgentWorkflowData>
{
    private readonly Func<AgentWorkflowData, bool> _predicate;
    private readonly string _failureMessage;

    public string Name { get; }

    public ValidateStep(string name, Func<AgentWorkflowData, bool> predicate,
        string failureMessage = "Validation failed")
    {
        Name = name;
        _predicate = predicate;
        _failureMessage = failureMessage;
    }

    public Task ExecuteAsync(IWorkflowContext<AgentWorkflowData> context)
    {
        if (!_predicate(context.Data))
        {
            context.IsAborted = true;
            context.Errors.Add(new WorkflowError(
                Name,
                new InvalidOperationException(_failureMessage),
                DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }
}
