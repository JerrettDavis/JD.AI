using System.Text.Json;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

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

    /// <summary>
    /// Generates a workflow from a natural language description using LLM.
    /// Falls back to heuristic generation if LLM fails.
    /// </summary>
    public async Task<WorkflowRefinementResult> GenerateAsync(
        string description,
        Kernel kernel,
        string? name = null,
        IReadOnlySet<string>? availableTools = null,
        CancellationToken ct = default)
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        var toolList = availableTools is not null
            ? string.Join("\n", availableTools.Select(t => $"  - {t}"))
            : "(no tool list available — use reasonable tool names)";

        history.AddSystemMessage(
            $$"""
            You are a workflow generator. Create a structured workflow from the user's description.
            Return ONLY a valid JSON object with this exact schema:
            {
              "name": "string (concise workflow name)",
              "version": "1.0",
              "description": "string (1-2 sentence summary)",
              "tags": ["string"],
              "steps": [
                {
                  "name": "string (step name)",
                  "kind": "Tool|Skill|Loop|Conditional",
                  "target": "plugin-function_name or null",
                  "condition": "string or null (for Loop/Conditional)",
                  "subSteps": []
                }
              ]
            }

            Available tools:
            {{toolList}}

            Use real tool names from the list above. Each step should be a concrete action.
            Return ONLY the JSON, no markdown fences.
            """);

        history.AddUserMessage(description);

        var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct)
            .ConfigureAwait(false);

        var responseText = result.Content?.Trim() ?? "";

        // Strip markdown code fences if present (same as RefineAsync)
        if (responseText.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = responseText.IndexOf('\n');
            var lastFence = responseText.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
                responseText = responseText[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            var workflow = new AgentWorkflowDefinition
            {
                Name = name ?? (root.TryGetProperty("name", out var n)
                    ? n.GetString() ?? DeriveWorkflowName(description) : DeriveWorkflowName(description)),
                Version = "1.0",
                Description = root.TryGetProperty("description", out var d)
                    ? d.GetString() ?? description[..Math.Min(description.Length, 200)]
                    : description[..Math.Min(description.Length, 200)],
                Tags = root.TryGetProperty("tags", out var t)
                    ? [.. t.EnumerateArray().Select(e => e.GetString() ?? "")]
                    : ExtractTags(description),
                Steps = root.TryGetProperty("steps", out var s)
                    ? [.. ParseSteps(s)]
                    : [],
            };

            return new WorkflowRefinementResult
            {
                Success = true,
                Workflow = workflow,
                Changelog = "Generated from description",
            };
        }
        catch (JsonException ex)
        {
            // Fallback to heuristic generation
            var fallback = Generate(description, name);
            return new WorkflowRefinementResult
            {
                Success = false,
                Workflow = fallback,
                Changelog = $"LLM generation failed ({ex.Message}), used heuristic fallback",
                RawResponse = responseText,
            };
        }
    }

    /// <summary>
    /// Refines an existing workflow using AI-powered analysis. Applies the user's
    /// natural language feedback to add, remove, reorder, or modify steps.
    /// Returns a new version of the workflow with the refinements applied.
    /// </summary>
    public async Task<WorkflowRefinementResult> RefineAsync(
        AgentWorkflowDefinition workflow,
        string feedback,
        Kernel kernel,
        CancellationToken ct = default)
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        var emitter = new WorkflowEmitter();
        var currentJson = emitter.Emit(workflow, WorkflowExportFormat.Json).Content;

        history.AddSystemMessage(
            """
            You are a workflow refinement assistant. You will be given a workflow definition in JSON
            and user feedback about changes they want. Apply the changes and return ONLY a valid JSON
            object with this exact schema:
            {
              "name": "string",
              "version": "string",
              "description": "string",
              "tags": ["string"],
              "steps": [
                {
                  "name": "string",
                  "kind": "Tool|Skill|Nested|Loop|Conditional",
                  "target": "string or null",
                  "condition": "string or null",
                  "subSteps": []
                }
              ],
              "changelog": "Brief description of what changed"
            }
            Increment the minor version number. Return ONLY the JSON, no markdown fences.
            """);

        history.AddUserMessage(
            $"Current workflow:\n{currentJson}\n\nRequested changes:\n{feedback}");

        var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct)
            .ConfigureAwait(false);

        var responseText = result.Content?.Trim() ?? "";

        // Strip markdown code fences if present
        if (responseText.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = responseText.IndexOf('\n');
            var lastFence = responseText.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
                responseText = responseText[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            var options = JsonDefaults.Options;
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            var refined = new AgentWorkflowDefinition
            {
                Name = root.TryGetProperty("name", out var n) ? n.GetString() ?? workflow.Name : workflow.Name,
                Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? workflow.Version : workflow.Version,
                Description = root.TryGetProperty("description", out var d)
                    ? d.GetString() ?? workflow.Description
                    : workflow.Description,
                Tags = root.TryGetProperty("tags", out var t)
                    ? [.. t.EnumerateArray().Select(e => e.GetString() ?? "")]
                    : [.. workflow.Tags],
                Steps = root.TryGetProperty("steps", out var s)
                    ? [.. ParseSteps(s)]
                    : [.. workflow.Steps],
                UpdatedAt = DateTime.UtcNow,
            };

            var changelog = root.TryGetProperty("changelog", out var cl)
                ? cl.GetString() ?? "Refined"
                : "Refined";

            return new WorkflowRefinementResult
            {
                Success = true,
                Workflow = refined,
                Changelog = changelog,
            };
        }
        catch (JsonException ex)
        {
            return new WorkflowRefinementResult
            {
                Success = false,
                Workflow = workflow,
                Changelog = $"Failed to parse AI response: {ex.Message}",
                RawResponse = responseText,
            };
        }
    }

    /// <summary>
    /// Extracts a workflow definition from conversation history by analyzing
    /// the tool calls and user/assistant interactions from the last N turns.
    /// </summary>
    public AgentWorkflowDefinition ExtractFromHistory(
        IReadOnlyList<ChatMessageContent> messages,
        int turnCount = 10,
        string? name = null)
    {
        var steps = new List<AgentStepDefinition>();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Take the last N user messages and their responses
        var userMessages = messages
            .Where(m => m.Role == AuthorRole.User)
            .TakeLast(turnCount)
            .ToList();

        var toolCalls = new List<(string Tool, string? Args)>();

        // Walk through all messages in the window to find tool calls
        var startIndex = messages.Count > 0 && userMessages.Count > 0
            ? messages.ToList().IndexOf(userMessages[0])
            : Math.Max(0, messages.Count - turnCount * 3);

        if (startIndex < 0) startIndex = 0;

        for (var i = startIndex; i < messages.Count; i++)
        {
            var msg = messages[i];

            // Check for function call content items
            foreach (var item in msg.Items)
            {
                if (item is FunctionCallContent fcc)
                {
                    var toolName = string.IsNullOrEmpty(fcc.PluginName)
                        ? fcc.FunctionName
                        : $"{fcc.PluginName}-{fcc.FunctionName}";
                    var argsStr = fcc.Arguments is not null
                        ? string.Join(", ", fcc.Arguments.Select(kv => $"{kv.Key}={kv.Value}"))
                        : null;
                    toolCalls.Add((toolName, argsStr));
                }
            }
        }

        // Deduplicate consecutive identical tool calls (e.g., repeated read_file)
        string? lastTool = null;
        var mergedCalls = new List<(string Tool, string? Args, int Count)>();
        foreach (var (tool, args) in toolCalls)
        {
            if (string.Equals(tool, lastTool, StringComparison.Ordinal) && mergedCalls.Count > 0)
            {
                var last = mergedCalls[^1];
                mergedCalls[^1] = (last.Tool, last.Args, last.Count + 1);
            }
            else
            {
                mergedCalls.Add((tool, args, 1));
            }
            lastTool = tool;
        }

        // Convert to workflow steps
        foreach (var (tool, args, count) in mergedCalls)
        {
            if (count > 2)
            {
                // Repeated tool calls become a loop
                var bodyStep = AgentStepDefinition.InvokeTool(
                    DeriveToolStepName(tool),
                    tool);
                steps.Add(AgentStepDefinition.LoopUntil(
                    $"Repeat {DeriveToolStepName(tool)}",
                    "items_exhausted",
                    [bodyStep]));
            }
            else
            {
                steps.Add(AgentStepDefinition.InvokeTool(
                    DeriveToolStepName(tool),
                    tool));
            }

            // Extract tags from tool names
            if (tool.Contains("git", StringComparison.OrdinalIgnoreCase)) tags.Add("git");
            if (tool.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                tool.Contains("build", StringComparison.OrdinalIgnoreCase)) tags.Add("build");
            if (tool.Contains("grep", StringComparison.OrdinalIgnoreCase) ||
                tool.Contains("glob", StringComparison.OrdinalIgnoreCase)) tags.Add("search");
            if (tool.Contains("file", StringComparison.OrdinalIgnoreCase)) tags.Add("files");
        }

        // Build description from first user message
        var firstUserMsg = userMessages.FirstOrDefault()?.Content ?? "Extracted workflow";
        var description = firstUserMsg.Length > 200 ? firstUserMsg[..200] + "..." : firstUserMsg;

        var workflowName = name ?? $"Extracted {DateTime.UtcNow:yyyyMMdd-HHmm}";

        return new AgentWorkflowDefinition
        {
            Name = workflowName,
            Version = "1.0",
            Description = description,
            Tags = [.. tags],
            Steps = steps,
        };
    }

    private static IEnumerable<AgentStepDefinition> ParseSteps(JsonElement stepsArray)
    {
        foreach (var stepEl in stepsArray.EnumerateArray())
        {
            var stepName = stepEl.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : "";
            var kindStr = stepEl.TryGetProperty("kind", out var sk) ? sk.GetString() ?? "Tool" : "Tool";
            var target = stepEl.TryGetProperty("target", out var st) ? st.GetString() : null;
            var condition = stepEl.TryGetProperty("condition", out var sc) ? sc.GetString() : null;

            var kind = kindStr.ToUpperInvariant() switch
            {
                "SKILL" => AgentStepKind.Skill,
                "NESTED" => AgentStepKind.Nested,
                "LOOP" => AgentStepKind.Loop,
                "CONDITIONAL" => AgentStepKind.Conditional,
                _ => AgentStepKind.Tool,
            };

            var subSteps = stepEl.TryGetProperty("subSteps", out var ss) && ss.ValueKind == JsonValueKind.Array
                ? [.. ParseSteps(ss)]
                : new List<AgentStepDefinition>();

            yield return new AgentStepDefinition
            {
                Name = stepName,
                Kind = kind,
                Target = target,
                Condition = condition,
                SubSteps = subSteps,
            };
        }
    }

    internal static string DeriveToolStepName(string tool)
    {
        // Convert "plugin-function_name" to "Function Name"
        var parts = tool.Split('-', '_', '.');
        return string.Join(' ', parts.Select(p =>
            p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..] : p));
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

/// <summary>Result of an AI-powered workflow refinement.</summary>
public sealed record WorkflowRefinementResult
{
    public bool Success { get; init; }
    public AgentWorkflowDefinition Workflow { get; init; } = new();
    public string Changelog { get; init; } = "";
    public string? RawResponse { get; init; }
}
