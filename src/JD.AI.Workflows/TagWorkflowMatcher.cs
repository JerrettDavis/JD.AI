namespace JD.AI.Workflows;

/// <summary>
/// Two-tier workflow matcher: exact name match (score=1.0) → tag overlap scoring.
/// Empty/whitespace tags are filtered to prevent spurious matches.
/// </summary>
public sealed class WorkflowMatcher : IWorkflowMatcher
{
    private readonly IWorkflowCatalog _catalog;

    public WorkflowMatcher(IWorkflowCatalog catalog) => _catalog = catalog;

    public async Task<WorkflowMatchResult?> MatchAsync(
        AgentRequest request, CancellationToken ct = default)
    {
        var workflows = await _catalog.ListAsync(ct).ConfigureAwait(false);
        if (workflows.Count == 0) return null;

        // Tier 1: exact name match
        var exact = workflows.FirstOrDefault(wf =>
            request.Message.Contains(wf.Name, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
            return new WorkflowMatchResult(exact, 1.0f, "exact");

        // Tier 2: tag overlap
        var words = request.Message
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .ToHashSet();

        WorkflowMatchResult? best = null;

        foreach (var wf in workflows)
        {
            // Filter empty/whitespace tags
            var validTags = wf.Tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (validTags.Count == 0) continue;

            var matchCount = validTags.Count(tag =>
                words.Contains(tag.ToLowerInvariant()));

            if (matchCount == 0) continue;

            var score = (float)matchCount / validTags.Count;
            if (best is null || score > best.Score)
            {
                best = new WorkflowMatchResult(wf, score, "tags");
            }
        }

        return best;
    }
}

/// <summary>
/// Tag-only workflow matcher — simpler variant without exact name matching.
/// </summary>
public sealed class TagWorkflowMatcher : IWorkflowMatcher
{
    private readonly IWorkflowCatalog _catalog;

    public TagWorkflowMatcher(IWorkflowCatalog catalog) => _catalog = catalog;

    public async Task<WorkflowMatchResult?> MatchAsync(
        AgentRequest request, CancellationToken ct = default)
    {
        var workflows = await _catalog.ListAsync(ct).ConfigureAwait(false);
        if (workflows.Count == 0) return null;

        var words = request.Message
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .ToHashSet();

        WorkflowMatchResult? best = null;

        foreach (var wf in workflows)
        {
            var validTags = wf.Tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (validTags.Count == 0) continue;

            var matchCount = validTags.Count(tag =>
                words.Contains(tag.ToLowerInvariant()));

            if (matchCount == 0) continue;

            var score = (float)matchCount / validTags.Count;
            if (best is null || score > best.Score)
            {
                best = new WorkflowMatchResult(
                    wf, score, $"Matched {matchCount}/{validTags.Count} tags");
            }
        }

        return best;
    }
}
