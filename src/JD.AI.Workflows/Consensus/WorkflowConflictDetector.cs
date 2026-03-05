using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Workflows.Consensus;

/// <summary>
/// Detects and describes conflicts when multiple users edit the same workflow concurrently.
/// Compares step-level changes between a common ancestor and two divergent versions,
/// producing a <see cref="ConflictReport"/> with auto-resolved and manual-resolution items.
/// </summary>
public static class WorkflowConflictDetector
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    /// <summary>
    /// Detects conflicts between two versions of a workflow that share a common ancestor.
    /// Non-overlapping changes are auto-merged; overlapping changes require manual resolution.
    /// </summary>
    public static ConflictReport Detect(
        AgentWorkflowDefinition ancestor,
        AgentWorkflowDefinition ours,
        AgentWorkflowDefinition theirs)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(theirs);

        var conflicts = new List<WorkflowConflict>();
        var autoResolved = new List<string>();

        // Check metadata conflicts
        DetectMetadataConflicts(ancestor, ours, theirs, conflicts, autoResolved);

        // Check step-level conflicts
        DetectStepConflicts(ancestor, ours, theirs, conflicts, autoResolved);

        return new ConflictReport
        {
            AncestorVersion = ancestor.Version,
            OurVersion = ours.Version,
            TheirVersion = theirs.Version,
            Conflicts = conflicts,
            AutoResolved = autoResolved,
            HasConflicts = conflicts.Count > 0,
        };
    }

    /// <summary>
    /// Computes a content hash for a workflow definition, useful for
    /// quick equality checks and change detection.
    /// </summary>
    public static string ComputeHash(AgentWorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Normalize: exclude volatile timestamps
        var canonical = new
        {
            definition.Name,
            definition.Version,
            definition.Description,
            definition.IsDeprecated,
            definition.MigrationGuidance,
            Steps = definition.Steps.Select(s => new { s.Name, s.Kind, s.Target, s.Condition }),
            definition.Tags,
        };

        var json = JsonSerializer.Serialize(canonical, JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Attempts a three-way merge of non-conflicting changes.
    /// Returns null if there are unresolvable conflicts.
    /// </summary>
    public static AgentWorkflowDefinition? TryMerge(
        AgentWorkflowDefinition ancestor,
        AgentWorkflowDefinition ours,
        AgentWorkflowDefinition theirs)
    {
        var report = Detect(ancestor, ours, theirs);
        if (report.HasConflicts)
            return null;

        // Start from ancestor, apply non-conflicting changes
        var merged = new AgentWorkflowDefinition
        {
            Name = PickChanged(ancestor.Name, ours.Name, theirs.Name),
            Version = PickChanged(ancestor.Version, ours.Version, theirs.Version),
            Description = PickChanged(ancestor.Description, ours.Description, theirs.Description),
            IsDeprecated = ours.IsDeprecated || theirs.IsDeprecated,
            MigrationGuidance = PickChanged(ancestor.MigrationGuidance, ours.MigrationGuidance, theirs.MigrationGuidance),
            Tags = MergeLists(ancestor.Tags, ours.Tags, theirs.Tags),
            Steps = MergeSteps(ancestor.Steps, ours.Steps, theirs.Steps),
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = ancestor.CreatedAt,
        };

        return merged;
    }

    private static void DetectMetadataConflicts(
        AgentWorkflowDefinition ancestor,
        AgentWorkflowDefinition ours,
        AgentWorkflowDefinition theirs,
        List<WorkflowConflict> conflicts,
        List<string> autoResolved)
    {
        CheckField("Description", ancestor.Description, ours.Description, theirs.Description, conflicts, autoResolved);
        CheckField("Name", ancestor.Name, ours.Name, theirs.Name, conflicts, autoResolved);
    }

    private static void CheckField(
        string fieldName,
        string? ancestorVal,
        string? ourVal,
        string? theirVal,
        List<WorkflowConflict> conflicts,
        List<string> autoResolved)
    {
        var ourChanged = !string.Equals(ancestorVal, ourVal, StringComparison.Ordinal);
        var theirChanged = !string.Equals(ancestorVal, theirVal, StringComparison.Ordinal);

        if (ourChanged && theirChanged && !string.Equals(ourVal, theirVal, StringComparison.Ordinal))
        {
            conflicts.Add(new WorkflowConflict
            {
                Path = fieldName,
                Kind = ConflictKind.MetadataConflict,
                AncestorValue = ancestorVal,
                OurValue = ourVal,
                TheirValue = theirVal,
            });
        }
        else if (ourChanged || theirChanged)
        {
            autoResolved.Add($"{fieldName} updated by {(ourChanged ? "ours" : "theirs")}");
        }
    }

    private static void DetectStepConflicts(
        AgentWorkflowDefinition ancestor,
        AgentWorkflowDefinition ours,
        AgentWorkflowDefinition theirs,
        List<WorkflowConflict> conflicts,
        List<string> autoResolved)
    {
        var ancestorSteps = IndexByCorrelationId(ancestor.Steps);
        var ourSteps = IndexByCorrelationId(ours.Steps);
        var theirSteps = IndexByCorrelationId(theirs.Steps);

        var allIds = ancestorSteps.Keys
            .Union(ourSteps.Keys)
            .Union(theirSteps.Keys)
            .Distinct();

        foreach (var id in allIds)
        {
            ancestorSteps.TryGetValue(id, out var ancestorStep);
            ourSteps.TryGetValue(id, out var ourStep);
            theirSteps.TryGetValue(id, out var theirStep);

            // Both added the same new step — auto-resolve if identical
            if (ancestorStep is null && ourStep is not null && theirStep is not null)
            {
                if (StepEquals(ourStep, theirStep))
                    autoResolved.Add($"Step '{id}' added identically by both");
                else
                    conflicts.Add(new WorkflowConflict
                    {
                        Path = $"Steps[{id}]",
                        Kind = ConflictKind.StepConflict,
                        AncestorValue = null,
                        OurValue = ourStep.Name,
                        TheirValue = theirStep.Name,
                    });
                continue;
            }

            // One side added a new step — auto-resolve
            if (ancestorStep is null)
            {
                autoResolved.Add($"Step '{(ourStep ?? theirStep)!.Name}' added by {(ourStep is not null ? "ours" : "theirs")}");
                continue;
            }

            // Both deleted — auto-resolve
            if (ourStep is null && theirStep is null)
            {
                autoResolved.Add($"Step '{ancestorStep.Name}' deleted by both");
                continue;
            }

            // One deleted, other modified
            if (ourStep is null || theirStep is null)
            {
                var remaining = ourStep ?? theirStep;
                if (!StepEquals(ancestorStep, remaining!))
                {
                    conflicts.Add(new WorkflowConflict
                    {
                        Path = $"Steps[{id}]",
                        Kind = ConflictKind.DeleteModifyConflict,
                        AncestorValue = ancestorStep.Name,
                        OurValue = ourStep?.Name,
                        TheirValue = theirStep?.Name,
                    });
                }
                else
                {
                    autoResolved.Add($"Step '{ancestorStep.Name}' deleted by {(ourStep is null ? "ours" : "theirs")}");
                }
                continue;
            }

            // Both modified
            var ourModified = !StepEquals(ancestorStep, ourStep);
            var theirModified = !StepEquals(ancestorStep, theirStep);

            if (ourModified && theirModified)
            {
                if (StepEquals(ourStep, theirStep))
                    autoResolved.Add($"Step '{id}' modified identically by both");
                else
                    conflicts.Add(new WorkflowConflict
                    {
                        Path = $"Steps[{id}]",
                        Kind = ConflictKind.StepConflict,
                        AncestorValue = ancestorStep.Name,
                        OurValue = ourStep.Name,
                        TheirValue = theirStep.Name,
                    });
            }
            else if (ourModified || theirModified)
            {
                autoResolved.Add($"Step '{id}' modified by {(ourModified ? "ours" : "theirs")}");
            }
        }
    }

    private static Dictionary<string, AgentStepDefinition> IndexByCorrelationId(
        IList<AgentStepDefinition> steps)
    {
        var dict = new Dictionary<string, AgentStepDefinition>(StringComparer.Ordinal);
        foreach (var step in steps)
            dict[step.CorrelationId] = step;
        return dict;
    }

    private static bool StepEquals(AgentStepDefinition a, AgentStepDefinition b) =>
        string.Equals(a.Name, b.Name, StringComparison.Ordinal) &&
        a.Kind == b.Kind &&
        string.Equals(a.Target, b.Target, StringComparison.Ordinal) &&
        string.Equals(a.Condition, b.Condition, StringComparison.Ordinal);

    private static string PickChanged(string? ancestor, string? ours, string? theirs)
    {
        var ourChanged = !string.Equals(ancestor, ours, StringComparison.Ordinal);
        if (ourChanged) return ours ?? string.Empty;
        return theirs ?? ancestor ?? string.Empty;
    }

    private static List<string> MergeLists(
        IList<string> ancestor, IList<string> ours, IList<string> theirs)
    {
        var ancestorSet = new HashSet<string>(ancestor, StringComparer.Ordinal);
        var added = ours.Except(ancestorSet).Union(theirs.Except(ancestorSet));
        var removed = ancestorSet.Except(ours).Union(ancestorSet.Except(theirs));
        return ancestorSet.Union(added).Except(removed).ToList();
    }

    private static List<AgentStepDefinition> MergeSteps(
        IList<AgentStepDefinition> ancestor,
        IList<AgentStepDefinition> ours,
        IList<AgentStepDefinition> theirs)
    {
        var ancestorIndex = IndexByCorrelationId(ancestor);
        var ourIndex = IndexByCorrelationId(ours);
        var theirIndex = IndexByCorrelationId(theirs);

        var result = new List<AgentStepDefinition>();

        // Start with "ours" order, incorporate "theirs" additions
        foreach (var step in ours)
            result.Add(ourIndex.TryGetValue(step.CorrelationId, out var s) ? s : step);

        foreach (var step in theirs)
        {
            if (!ourIndex.ContainsKey(step.CorrelationId) && !ancestorIndex.ContainsKey(step.CorrelationId))
                result.Add(step);
        }

        // Remove steps deleted by either side
        var deletedByOurs = ancestorIndex.Keys.Except(ourIndex.Keys);
        var deletedByTheirs = ancestorIndex.Keys.Except(theirIndex.Keys);
        var allDeleted = new HashSet<string>(deletedByOurs.Union(deletedByTheirs));

        return result.Where(s => !allDeleted.Contains(s.CorrelationId)).ToList();
    }
}

/// <summary>Result of a three-way conflict detection.</summary>
public sealed class ConflictReport
{
    public required string AncestorVersion { get; init; }
    public required string OurVersion { get; init; }
    public required string TheirVersion { get; init; }
    public IReadOnlyList<WorkflowConflict> Conflicts { get; init; } = [];
    public IReadOnlyList<string> AutoResolved { get; init; } = [];
    public bool HasConflicts { get; init; }
}

/// <summary>A single conflict between two versions of a workflow.</summary>
public sealed class WorkflowConflict
{
    public required string Path { get; init; }
    public required ConflictKind Kind { get; init; }
    public string? AncestorValue { get; init; }
    public string? OurValue { get; init; }
    public string? TheirValue { get; init; }
}

/// <summary>Types of workflow conflicts.</summary>
public enum ConflictKind
{
    MetadataConflict,
    StepConflict,
    DeleteModifyConflict,
}
