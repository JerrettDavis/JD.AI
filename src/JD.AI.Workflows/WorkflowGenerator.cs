namespace JD.AI.Workflows;

/// <summary>
/// Generates structured workflow definitions from natural language descriptions.
/// Uses a prompt-based approach: the description is analyzed and decomposed into
/// steps with tool mappings, parameters, and control flow.
/// </summary>
public sealed class WorkflowGenerator
{

    /// <summary>
    /// Generates a workflow definition from a natural language description.
    /// This is a deterministic fallback that parses common patterns;
    /// for AI-powered generation, the agent loop itself generates
    /// the workflow and calls SaveAsync on the catalog.
    /// </summary>
    public AgentWorkflowDefinition Generate(string description, string? name = null)
    {
        var steps = DecomposeIntoSteps(description);
        var workflowName = name ?? DeriveWorkflowName(description);

        return new AgentWorkflowDefinition
        {
            Name = workflowName,
            Version = "1.0",
            Description = description.Length > 200 ? description[..200] + "..." : description,
            Tags = ExtractTags(description),
            Steps = steps,
        };
    }

    /// <summary>
    /// Generates a dry-run preview of a workflow without executing.
    /// Shows what each step would do, required tools, and validation.
    /// </summary>
    public WorkflowDryRunResult DryRun(AgentWorkflowDefinition workflow, IReadOnlySet<string>? availableTools = null)
    {
        var steps = new List<DryRunStep>();
        var missingTools = new List<string>();
        var warnings = new List<string>();

        foreach (var step in workflow.Steps)
        {
            var dryStep = PreviewStep(step, availableTools, missingTools, warnings);
            steps.Add(dryStep);
        }

        return new WorkflowDryRunResult
        {
            WorkflowName = workflow.Name,
            Version = workflow.Version,
            TotalSteps = CountSteps(workflow.Steps),
            Steps = steps,
            MissingTools = missingTools,
            Warnings = warnings,
            IsValid = missingTools.Count == 0,
        };
    }

    /// <summary>
    /// Composes multiple workflows into a single composite workflow.
    /// Steps from each child workflow become nested steps in the composite.
    /// </summary>
    public AgentWorkflowDefinition Compose(string name, IReadOnlyList<AgentWorkflowDefinition> workflows)
    {
        var steps = new List<AgentStepDefinition>();
        var allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var wf in workflows)
        {
            var nested = new AgentStepDefinition
            {
                Name = wf.Name,
                Kind = AgentStepKind.Nested,
                Target = wf.Name,
                SubSteps = [.. wf.Steps],
            };
            steps.Add(nested);

            foreach (var tag in wf.Tags)
                allTags.Add(tag);
        }

        var descriptions = workflows.Select(w => w.Name);
        return new AgentWorkflowDefinition
        {
            Name = name,
            Version = "1.0",
            Description = $"Composite: {string.Join(" → ", descriptions)}",
            Tags = [.. allTags],
            Steps = steps,
        };
    }

    /// <summary>
    /// Formats a dry-run result for display.
    /// </summary>
    public static string FormatDryRun(WorkflowDryRunResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📋 Dry Run: {result.WorkflowName} v{result.Version}");
        sb.AppendLine($"   Steps: {result.TotalSteps} | Valid: {(result.IsValid ? "✅" : "❌")}");
        sb.AppendLine();

        var idx = 1;
        foreach (var step in result.Steps)
        {
            var prefix = step.Kind switch
            {
                AgentStepKind.Loop => "🔄",
                AgentStepKind.Conditional => "❓",
                AgentStepKind.Nested => "📦",
                _ => "▶️",
            };
            sb.AppendLine($"  {idx}. {prefix} {step.Name}");
            sb.AppendLine($"     Tool: {step.ToolOrTarget ?? "(agent decision)"}");
            sb.AppendLine($"     Action: {step.Description}");
            if (step.SubSteps.Count > 0)
            {
                var subIdx = 'a';
                foreach (var sub in step.SubSteps)
                {
                    sb.AppendLine($"     {idx}{subIdx}. {sub.Name} → {sub.ToolOrTarget ?? "(agent)"}");
                    subIdx++;
                }
            }
            idx++;
        }

        if (result.MissingTools.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"  ⚠️  Missing tools: {string.Join(", ", result.MissingTools)}");
        }

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine();
            foreach (var w in result.Warnings)
                sb.AppendLine($"  ⚠️  {w}");
        }

        return sb.ToString().TrimEnd();
    }

    private static List<AgentStepDefinition> DecomposeIntoSteps(string description)
    {
        var steps = new List<AgentStepDefinition>();
        var sentences = description.Split(['.', ',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var sentence in sentences)
        {
            if (string.IsNullOrWhiteSpace(sentence)) continue;

            var lower = sentence.ToLowerInvariant();

            // Detect loop patterns
            if (lower.Contains("for each") || lower.Contains("foreach") || lower.Contains("loop over") || lower.Contains("iterate"))
            {
                steps.Add(AgentStepDefinition.LoopUntil(
                    DeriveStepName(sentence),
                    "items_exhausted",
                    [AgentStepDefinition.InvokeTool(DeriveStepName(sentence) + "_body", MapToTool(sentence))]));
                continue;
            }

            // Detect conditional patterns
            if (lower.Contains("if ") || lower.Contains("check ") || lower.Contains("verify "))
            {
                steps.Add(AgentStepDefinition.If(
                    DeriveStepName(sentence),
                    lower,
                    [AgentStepDefinition.InvokeTool(DeriveStepName(sentence) + "_action", MapToTool(sentence))]));
                continue;
            }

            // Default: tool or skill step
            var tool = MapToTool(sentence);
            steps.Add(AgentStepDefinition.InvokeTool(DeriveStepName(sentence), tool));
        }

        return steps;
    }

    private static string MapToTool(string description)
    {
        var lower = description.ToLowerInvariant();

        if (lower.Contains("clone") || lower.Contains("git clone")) return "shell-run_command";
        if (lower.Contains("diff") || lower.Contains("compare")) return "git-git_diff";
        if (lower.Contains("read") || lower.Contains("open") || lower.Contains("inspect")) return "file-read_file";
        if (lower.Contains("write") || lower.Contains("create") || lower.Contains("generate file")) return "file-write_file";
        if (lower.Contains("edit") || lower.Contains("modify") || lower.Contains("update")) return "file-edit_file";
        if (lower.Contains("search") || lower.Contains("find") || lower.Contains("grep")) return "search-grep";
        if (lower.Contains("test") || lower.Contains("build") || lower.Contains("run") || lower.Contains("execute")) return "shell-run_command";
        if (lower.Contains("commit") || lower.Contains("push") || lower.Contains("pull")) return "git-git_commit";
        if (lower.Contains("list") || lower.Contains("directory") || lower.Contains("tree")) return "file-list_directory";
        if (lower.Contains("fetch") || lower.Contains("download") || lower.Contains("url")) return "web-web_fetch";

        return "think-think";
    }

    private static string DeriveStepName(string sentence)
    {
        var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(4)
            .Select(w => w.ToLowerInvariant().Trim('.', ',', ';', ':', '!', '?'));
        return string.Join('_', words);
    }

    private static string DeriveWorkflowName(string description)
    {
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(5)
            .Select(w =>
            {
                var clean = w.Trim('.', ',', ';', ':', '!', '?');
                return char.ToUpperInvariant(clean[0]) + clean[1..].ToLowerInvariant();
            });
        return string.Join(' ', words);
    }

    private static List<string> ExtractTags(string description)
    {
        var tags = new List<string>();
        var lower = description.ToLowerInvariant();

        if (lower.Contains("test")) tags.Add("testing");
        if (lower.Contains("review") || lower.Contains("pr")) tags.Add("code-review");
        if (lower.Contains("deploy") || lower.Contains("release")) tags.Add("deployment");
        if (lower.Contains("refactor")) tags.Add("refactoring");
        if (lower.Contains("document") || lower.Contains("docs")) tags.Add("documentation");
        if (lower.Contains("security") || lower.Contains("vulnerability")) tags.Add("security");
        if (lower.Contains("build") || lower.Contains("compile")) tags.Add("build");

        return tags;
    }

    private static DryRunStep PreviewStep(AgentStepDefinition step, IReadOnlySet<string>? availableTools, List<string> missingTools, List<string> warnings)
    {
        var dryStep = new DryRunStep
        {
            Name = step.Name,
            Kind = step.Kind,
            ToolOrTarget = step.Target,
            Description = step.Kind switch
            {
                AgentStepKind.Tool => $"Invoke tool '{step.Target}'",
                AgentStepKind.Skill => $"Execute skill '{step.Target}'",
                AgentStepKind.Nested => $"Run nested workflow '{step.Target}'",
                AgentStepKind.Loop => $"Loop until '{step.Condition}' with {step.SubSteps.Count} sub-steps",
                AgentStepKind.Conditional => $"If '{step.Condition}' then {step.SubSteps.Count} steps",
                _ => step.Name,
            },
        };

        // Check tool availability
        if (step.Kind == AgentStepKind.Tool && step.Target is not null && availableTools is not null)
        {
            if (!availableTools.Contains(step.Target))
                missingTools.Add(step.Target);
        }

        // Warn about loops without bounds
        if (step.Kind == AgentStepKind.Loop && string.IsNullOrWhiteSpace(step.Condition))
            warnings.Add($"Step '{step.Name}' is a loop with no termination condition.");

        // Process sub-steps
        foreach (var sub in step.SubSteps)
        {
            var subDry = PreviewStep(sub, availableTools, missingTools, warnings);
            dryStep.SubSteps.Add(subDry);
        }

        return dryStep;
    }

    private static int CountSteps(IList<AgentStepDefinition> steps)
    {
        var count = 0;
        foreach (var step in steps)
        {
            count++;
            count += CountSteps(step.SubSteps);
        }
        return count;
    }
}

/// <summary>Result of a workflow dry-run.</summary>
public sealed record WorkflowDryRunResult
{
    public string WorkflowName { get; init; } = "";
    public string Version { get; init; } = "1.0";
    public int TotalSteps { get; init; }
    public IList<DryRunStep> Steps { get; init; } = [];
    public IList<string> MissingTools { get; init; } = [];
    public IList<string> Warnings { get; init; } = [];
    public bool IsValid { get; init; }
}

/// <summary>A single step preview in a dry-run.</summary>
public sealed record DryRunStep
{
    public string Name { get; init; } = "";
    public AgentStepKind Kind { get; init; }
    public string? ToolOrTarget { get; init; }
    public string Description { get; init; } = "";
    public IList<DryRunStep> SubSteps { get; init; } = new List<DryRunStep>();
}
