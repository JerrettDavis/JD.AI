using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.History;

namespace JD.AI.Tests.Workflows.History;

public sealed class WorkflowHistoryVisualizationTests
{
    // ── Helper ────────────────────────────────────────────────────────────────

    private static WorkflowHistoryNode MakeNode(
        string fingerprint,
        string name,
        AgentStepKind kind = AgentStepKind.Skill,
        long executionCount = 10,
        long successCount = 10) =>
        new()
        {
            Fingerprint = fingerprint,
            Name = name,
            Kind = kind,
            ExecutionCount = executionCount,
            SuccessCount = successCount,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        };

    // ── Empty graph ───────────────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_EmptyGraph_ProducesValidHeader()
    {
        var graph = new WorkflowHistoryGraph();
        var result = graph.ToMermaid();

        result.Should().StartWith("graph TD");
    }

    [Fact]
    public void ToMermaid_EmptyGraph_HasNoNodeOrEdgeLines()
    {
        var graph = new WorkflowHistoryGraph();
        var result = graph.ToMermaid();

        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Should().ContainSingle().Which.Should().Be("graph TD");
    }

    // ── Two nodes, one edge ───────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_TwoNodesOneEdge_ContainsBothNodeIds()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "StepA"));
        graph.GetOrAddNode("fp2", () => MakeNode("fp2", "StepB"));
        graph.RecordTransition("fp1", "fp2", EdgeKind.Sequential, "workflow1", TimeSpan.Zero);

        var result = graph.ToMermaid();

        result.Should().Contain("N_fp1");
        result.Should().Contain("N_fp2");
    }

    [Fact]
    public void ToMermaid_TwoNodesOneEdge_ContainsEdgeLine()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "StepA"));
        graph.GetOrAddNode("fp2", () => MakeNode("fp2", "StepB"));
        graph.RecordTransition("fp1", "fp2", EdgeKind.Sequential, "workflow1", TimeSpan.Zero);

        var result = graph.ToMermaid();

        result.Should().Contain("N_fp1");
        result.Should().Contain("N_fp2");
        result.Should().Contain("-->");
    }

    // ── Node ID deduplication ─────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_SameFingerprint_ProducesSingleNodeDeclaration()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("abc123", () => MakeNode("abc123", "MyStep"));
        // Second call — same fingerprint, returns same node
        graph.GetOrAddNode("abc123", () => MakeNode("abc123", "MyStep"));

        var result = graph.ToMermaid();

        var occurrences = CountOccurrences(result, "N_abc123");
        // Node appears in: declaration line + style line = 2 occurrences max
        // It should NOT appear more than once in declaration lines
        var declarationLines = result.Split('\n')
            .Where(l => l.TrimStart().StartsWith("N_abc123", StringComparison.Ordinal))
            .Count();
        declarationLines.Should().Be(1, "same fingerprint must produce exactly one node declaration");
    }

    // ── Edge kind arrow styles ────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_SequentialEdge_UsesSolidArrow()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("s", () => MakeNode("s", "Source"));
        graph.GetOrAddNode("t", () => MakeNode("t", "Target"));
        graph.RecordTransition("s", "t", EdgeKind.Sequential, "wf", TimeSpan.Zero);

        var result = graph.ToMermaid();

        result.Should().Contain("-->");
        result.Should().NotContain("-.->");
    }

    [Fact]
    public void ToMermaid_SubStepEdge_UsesDottedArrow()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("s", () => MakeNode("s", "Source"));
        graph.GetOrAddNode("t", () => MakeNode("t", "Target"));
        graph.RecordTransition("s", "t", EdgeKind.SubStep, "wf", TimeSpan.Zero);

        var result = graph.ToMermaid();

        result.Should().Contain(".->");
    }

    // ── Success rate styling ──────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_HighSuccessRate_GetsGreenStyle()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "HealthyStep", executionCount: 100, successCount: 95));

        var result = graph.ToMermaid();

        result.Should().Contain("fill:#2d6a2d");
    }

    [Fact]
    public void ToMermaid_MediumSuccessRate_GetsYellowStyle()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "OkStep", executionCount: 100, successCount: 70));

        var result = graph.ToMermaid();

        result.Should().Contain("fill:#c8a600");
    }

    [Fact]
    public void ToMermaid_LowSuccessRate_GetsRedStyle()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "BrokenStep", executionCount: 100, successCount: 10));

        var result = graph.ToMermaid();

        result.Should().Contain("fill:#8b1a1a");
    }

    [Fact]
    public void ToMermaid_ZeroExecutions_GetsRedStyle()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "NeverRun", executionCount: 0, successCount: 0));

        var result = graph.ToMermaid();

        result.Should().Contain("fill:#8b1a1a");
    }

    [Fact]
    public void ToMermaid_ExactlyNinetyPercent_GetsGreenStyle()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "NinetyStep", executionCount: 10, successCount: 9));

        var result = graph.ToMermaid();

        result.Should().Contain("fill:#2d6a2d");
    }

    // ── maxNodes limit ────────────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_MaxNodesLimit_FiltersToTopByExecutionCount()
    {
        var graph = new WorkflowHistoryGraph();

        // Add 5 nodes with different execution counts
        for (var i = 1; i <= 5; i++)
        {
            var node = MakeNode($"fp{i}", $"Step{i}", executionCount: i * 10, successCount: i * 10);
            graph.GetOrAddNode($"fp{i}", () => node);
        }

        // maxNodes=3 should keep fp3, fp4, fp5 (top by execution count)
        var result = graph.ToMermaid(maxNodes: 3);

        result.Should().Contain("N_fp3");
        result.Should().Contain("N_fp4");
        result.Should().Contain("N_fp5");
        result.Should().NotContain("N_fp1");
        result.Should().NotContain("N_fp2");
    }

    [Fact]
    public void ToMermaid_MaxNodesLargerThanGraph_IncludesAllNodes()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "A"));
        graph.GetOrAddNode("fp2", () => MakeNode("fp2", "B"));

        var result = graph.ToMermaid(maxNodes: 100);

        result.Should().Contain("N_fp1");
        result.Should().Contain("N_fp2");
    }

    // ── minEdgeWeight filter ──────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_MinEdgeWeightFiltersLowWeightEdges()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("s", () => MakeNode("s", "Source"));
        graph.GetOrAddNode("t", () => MakeNode("t", "Target"));
        // Record a single transition (weight=1)
        graph.RecordTransition("s", "t", EdgeKind.Sequential, "wf", TimeSpan.Zero);

        // minEdgeWeight=2 should suppress this edge
        var result = graph.ToMermaid(minEdgeWeight: 2);

        result.Should().NotContain("-->");
    }

    // ── Subgraph overload ─────────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_SubgraphFromRoot_IncludesOnlyReachableNodes()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("root", () => MakeNode("root", "Root"));
        graph.GetOrAddNode("child", () => MakeNode("child", "Child"));
        graph.GetOrAddNode("unrelated", () => MakeNode("unrelated", "Unrelated"));
        graph.RecordTransition("root", "child", EdgeKind.Sequential, "wf", TimeSpan.Zero);

        var result = graph.ToMermaid("root", maxDepth: 5);

        result.Should().Contain("N_root");
        result.Should().Contain("N_child");
        result.Should().NotContain("N_unrelated");
    }

    [Fact]
    public void ToMermaid_SubgraphMaxDepth_RespectsDepthLimit()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("n0", () => MakeNode("n0", "N0"));
        graph.GetOrAddNode("n1", () => MakeNode("n1", "N1"));
        graph.GetOrAddNode("n2", () => MakeNode("n2", "N2"));
        graph.GetOrAddNode("n3", () => MakeNode("n3", "N3"));
        graph.RecordTransition("n0", "n1", EdgeKind.Sequential, "wf", TimeSpan.Zero);
        graph.RecordTransition("n1", "n2", EdgeKind.Sequential, "wf", TimeSpan.Zero);
        graph.RecordTransition("n2", "n3", EdgeKind.Sequential, "wf", TimeSpan.Zero);

        // maxDepth=2 should reach n0, n1, n2 but NOT n3
        var result = graph.ToMermaid("n0", maxDepth: 2);

        result.Should().Contain("N_n0");
        result.Should().Contain("N_n1");
        result.Should().Contain("N_n2");
        result.Should().NotContain("N_n3");
    }

    // ── Label escaping ────────────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_LabelWithSpecialChars_EscapesCorrectly()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("fp1", () => MakeNode("fp1", "Step [with brackets]"));

        var result = graph.ToMermaid();

        // Brackets should be escaped to parentheses
        result.Should().Contain("Step (with brackets)");
    }

    // ── Edge weight label ─────────────────────────────────────────────────────

    [Fact]
    public void ToMermaid_EdgeWeightInLabel()
    {
        var graph = new WorkflowHistoryGraph();
        graph.GetOrAddNode("s", () => MakeNode("s", "Source"));
        graph.GetOrAddNode("t", () => MakeNode("t", "Target"));
        graph.RecordTransition("s", "t", EdgeKind.Sequential, "wf", TimeSpan.Zero);
        graph.RecordTransition("s", "t", EdgeKind.Sequential, "wf", TimeSpan.Zero);
        graph.RecordTransition("s", "t", EdgeKind.Sequential, "wf", TimeSpan.Zero);

        var result = graph.ToMermaid();

        result.Should().Contain("w:3");
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
