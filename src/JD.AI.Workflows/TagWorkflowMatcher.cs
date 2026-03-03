namespace JD.AI.Workflows;

/// <summary>
/// Tag-based workflow matcher — finds catalog entries by matching tags against request keywords.
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
            var matchCount = wf.Tags.Count(tag =>
                words.Contains(tag.ToLowerInvariant()));

            if (matchCount == 0 || wf.Tags.Count == 0) continue;

            var score = (float)matchCount / wf.Tags.Count;
            if (best is null || score > best.Score)
            {
                best = new WorkflowMatchResult(
                    wf, score, $"Matched {matchCount}/{wf.Tags.Count} tags");
            }
        }

        return best;
    }
}
