using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.History;

namespace JD.AI.Tests.Workflows.History;

public sealed class WorkflowHistoryGraphTests
{
    [Fact]
    public void GetOrAddNode_FirstCall_CreatesNode()
    {
        var graph = new WorkflowHistoryGraph();
        var fp = "test-fingerprint";
        var now = DateTimeOffset.UtcNow;

        var node = graph.GetOrAddNode(fp, () => new WorkflowHistoryNode
        {
            Fingerprint = fp,
            Name = "TestStep",
            Kind = AgentStepKind.Skill,
            Target = "test-target",
            FirstSeenAt = now,
            LastSeenAt = now,
        });

        node.Should().NotBeNull();
        node.Fingerprint.Should().Be(fp);
        node.Name.Should().Be("TestStep");
        graph.NodeCount.Should().Be(1);
    }

    [Fact]
    public void GetOrAddNode_SecondCall_ReturnsSame()
    {
        var graph = new WorkflowHistoryGraph();
        var fp = "test-fingerprint";
        var now = DateTimeOffset.UtcNow;

        var createFunc = () => new WorkflowHistoryNode
        {
            Fingerprint = fp,
            Name = "TestStep",
            Kind = AgentStepKind.Skill,
            Target = "test-target",
            FirstSeenAt = now,
            LastSeenAt = now,
        };

        var node1 = graph.GetOrAddNode(fp, createFunc);
        var node2 = graph.GetOrAddNode(fp, createFunc);

        node1.Should().BeSameAs(node2);
        graph.NodeCount.Should().Be(1);
    }

    [Fact]
    public void GetOrAddNode_FactoryNotCalledOnSecondCall()
    {
        var graph = new WorkflowHistoryGraph();
        var fp = "test-fingerprint";
        var now = DateTimeOffset.UtcNow;
        var factoryCalls = 0;

        var createFunc = () =>
        {
            factoryCalls++;
            return new WorkflowHistoryNode
            {
                Fingerprint = fp,
                Name = "TestStep",
                Kind = AgentStepKind.Skill,
                Target = "test-target",
                FirstSeenAt = now,
                LastSeenAt = now,
            };
        };

        graph.GetOrAddNode(fp, createFunc);
        graph.GetOrAddNode(fp, createFunc);

        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void RecordTransition_FirstCall_CreatesEdge()
    {
        var graph = new WorkflowHistoryGraph();
        var srcFp = "src";
        var tgtFp = "tgt";

        var edge = graph.RecordTransition(srcFp, tgtFp, EdgeKind.Sequential, "workflow1", TimeSpan.FromSeconds(1));

        edge.Should().NotBeNull();
        edge.SourceFingerprint.Should().Be(srcFp);
        edge.TargetFingerprint.Should().Be(tgtFp);
        edge.Kind.Should().Be(EdgeKind.Sequential);
        edge.Weight.Should().Be(1);
        edge.WorkflowNames.Should().Contain("workflow1");
        graph.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void RecordTransition_SecondCall_IncrementsWeight()
    {
        var graph = new WorkflowHistoryGraph();
        var srcFp = "src";
        var tgtFp = "tgt";

        graph.RecordTransition(srcFp, tgtFp, EdgeKind.Sequential, "workflow1", TimeSpan.FromSeconds(1));
        var edge2 = graph.RecordTransition(srcFp, tgtFp, EdgeKind.Sequential, "workflow1", TimeSpan.FromSeconds(2));

        edge2.Weight.Should().Be(2);
        graph.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void RecordTransition_DifferentKind_CreatesNewEdge()
    {
        var graph = new WorkflowHistoryGraph();
        var srcFp = "src";
        var tgtFp = "tgt";

        graph.RecordTransition(srcFp, tgtFp, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        graph.RecordTransition(srcFp, tgtFp, EdgeKind.SubStep, "workflow1", TimeSpan.Zero);

        graph.EdgeCount.Should().Be(2);
    }

    [Fact]
    public void RecordTransition_AddsWorkflowName()
    {
        var graph = new WorkflowHistoryGraph();
        var srcFp = "src";
        var tgtFp = "tgt";

        graph.RecordTransition(srcFp, tgtFp, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        var edge = graph.RecordTransition(srcFp, tgtFp, EdgeKind.Sequential, "workflow2", TimeSpan.Zero);

        edge.WorkflowNames.Should().Contain("workflow1", "workflow2");
    }

    [Fact]
    public void RecordTransition_UpdatesAverageTime()
    {
        var graph = new WorkflowHistoryGraph();
        var srcFp = "src";
        var tgtFp = "tgt";

        graph.RecordTransition(srcFp, tgtFp, EdgeKind.Sequential, "workflow1", TimeSpan.FromSeconds(2));
        var edge = graph.RecordTransition(srcFp, tgtFp, EdgeKind.Sequential, "workflow1", TimeSpan.FromSeconds(4));

        // Average of 2s and 4s = 3s
        edge.AverageTransitionTime.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void GetOutgoingEdges_ReturnsCorrectEdges()
    {
        var graph = new WorkflowHistoryGraph();
        var fp1 = "fp1";
        var fp2 = "fp2";
        var fp3 = "fp3";

        graph.RecordTransition(fp1, fp2, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        graph.RecordTransition(fp1, fp3, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        graph.RecordTransition(fp2, fp3, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);

        var outgoing = graph.GetOutgoingEdges(fp1);

        outgoing.Should().HaveCount(2);
        outgoing.Should().AllSatisfy(e => e.SourceFingerprint.Should().Be(fp1));
        outgoing.Select(e => e.TargetFingerprint).Should().Contain(fp2, fp3);
    }

    [Fact]
    public void GetIncomingEdges_ReturnsCorrectEdges()
    {
        var graph = new WorkflowHistoryGraph();
        var fp1 = "fp1";
        var fp2 = "fp2";
        var fp3 = "fp3";

        graph.RecordTransition(fp1, fp3, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        graph.RecordTransition(fp2, fp3, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);

        var incoming = graph.GetIncomingEdges(fp3);

        incoming.Should().HaveCount(2);
        incoming.Should().AllSatisfy(e => e.TargetFingerprint.Should().Be(fp3));
        incoming.Select(e => e.SourceFingerprint).Should().Contain(fp1, fp2);
    }

    [Fact]
    public void GetReachable_BFS_TraversesCorrectly()
    {
        var graph = new WorkflowHistoryGraph();
        var fp1 = "fp1";
        var fp2 = "fp2";
        var fp3 = "fp3";
        var fp4 = "fp4";

        // Create nodes so they exist
        graph.GetOrAddNode(fp1, () => new WorkflowHistoryNode { Fingerprint = fp1, Name = "Node1", Kind = AgentStepKind.Skill });
        graph.GetOrAddNode(fp2, () => new WorkflowHistoryNode { Fingerprint = fp2, Name = "Node2", Kind = AgentStepKind.Skill });
        graph.GetOrAddNode(fp3, () => new WorkflowHistoryNode { Fingerprint = fp3, Name = "Node3", Kind = AgentStepKind.Skill });
        graph.GetOrAddNode(fp4, () => new WorkflowHistoryNode { Fingerprint = fp4, Name = "Node4", Kind = AgentStepKind.Skill });

        // Create edges: fp1 -> fp2 -> fp3, fp1 -> fp4
        graph.RecordTransition(fp1, fp2, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        graph.RecordTransition(fp2, fp3, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        graph.RecordTransition(fp1, fp4, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);

        var reachable = graph.GetReachable(fp1);

        reachable.Should().HaveCount(3);
        reachable.Select(n => n.Fingerprint).Should().Contain(fp2, fp3, fp4);
    }

    [Fact]
    public void GetReachable_NoOutgoing_ReturnsEmpty()
    {
        var graph = new WorkflowHistoryGraph();
        var fp1 = "fp1";

        graph.GetOrAddNode(fp1, () => new WorkflowHistoryNode { Fingerprint = fp1, Name = "Node1", Kind = AgentStepKind.Skill });

        var reachable = graph.GetReachable(fp1);

        reachable.Should().BeEmpty();
    }

    [Fact]
    public void GetReachable_CycleHandling_DoesNotInfiniteLoop()
    {
        var graph = new WorkflowHistoryGraph();
        var fp1 = "fp1";
        var fp2 = "fp2";
        var fp3 = "fp3";

        graph.GetOrAddNode(fp1, () => new WorkflowHistoryNode { Fingerprint = fp1, Name = "Node1", Kind = AgentStepKind.Skill });
        graph.GetOrAddNode(fp2, () => new WorkflowHistoryNode { Fingerprint = fp2, Name = "Node2", Kind = AgentStepKind.Skill });
        graph.GetOrAddNode(fp3, () => new WorkflowHistoryNode { Fingerprint = fp3, Name = "Node3", Kind = AgentStepKind.Skill });

        // Create cycle: fp1 -> fp2 -> fp3 -> fp1
        graph.RecordTransition(fp1, fp2, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        graph.RecordTransition(fp2, fp3, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);
        graph.RecordTransition(fp3, fp1, EdgeKind.Sequential, "workflow1", TimeSpan.Zero);

        var reachable = graph.GetReachable(fp1);

        // Should not infinite loop and should return all nodes except starting point
        reachable.Should().HaveCount(2);
        reachable.Select(n => n.Fingerprint).Should().Contain(fp2, fp3);
    }

    [Fact]
    public void GetNode_Exists_ReturnsNode()
    {
        var graph = new WorkflowHistoryGraph();
        var fp = "test-fp";

        graph.GetOrAddNode(fp, () => new WorkflowHistoryNode { Fingerprint = fp, Name = "Test", Kind = AgentStepKind.Skill });
        var node = graph.GetNode(fp);

        node.Should().NotBeNull();
        node!.Fingerprint.Should().Be(fp);
    }

    [Fact]
    public void GetNode_DoesNotExist_ReturnsNull()
    {
        var graph = new WorkflowHistoryGraph();
        var node = graph.GetNode("nonexistent");

        node.Should().BeNull();
    }

    [Fact]
    public void Nodes_ReturnsAllNodes()
    {
        var graph = new WorkflowHistoryGraph();

        graph.GetOrAddNode("fp1", () => new WorkflowHistoryNode { Fingerprint = "fp1", Name = "N1", Kind = AgentStepKind.Skill });
        graph.GetOrAddNode("fp2", () => new WorkflowHistoryNode { Fingerprint = "fp2", Name = "N2", Kind = AgentStepKind.Skill });
        graph.GetOrAddNode("fp3", () => new WorkflowHistoryNode { Fingerprint = "fp3", Name = "N3", Kind = AgentStepKind.Skill });

        graph.Nodes.Should().HaveCount(3);
    }

    [Fact]
    public void Edges_ReturnsAllEdges()
    {
        var graph = new WorkflowHistoryGraph();

        graph.RecordTransition("fp1", "fp2", EdgeKind.Sequential, "w1", TimeSpan.Zero);
        graph.RecordTransition("fp2", "fp3", EdgeKind.Sequential, "w1", TimeSpan.Zero);
        graph.RecordTransition("fp1", "fp3", EdgeKind.SubStep, "w1", TimeSpan.Zero);

        graph.Edges.Should().HaveCount(3);
    }
}
