using System.Text;
using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.Security;

namespace JD.AI.Tests.Workflows.Security;

public sealed class WorkflowSignatureTests
{
    private static readonly byte[] TestKey = Encoding.UTF8.GetBytes("test-workflow-signing-key-32b!!");

    private static AgentWorkflowDefinition CreateSampleWorkflow() => new()
    {
        Name = "build-pipeline",
        Version = "1.0.0",
        Description = "Build and test pipeline",
        Steps =
        [
            AgentStepDefinition.RunSkill("run-tests"),
            AgentStepDefinition.InvokeTool("build", "dotnet-build"),
        ],
    };

    [Fact]
    public void Sign_ProducesHexString()
    {
        var workflow = CreateSampleWorkflow();

        var signature = WorkflowSignature.Sign(workflow, TestKey);

        signature.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        var workflow = CreateSampleWorkflow();
        var signature = WorkflowSignature.Sign(workflow, TestKey);

        WorkflowSignature.Verify(workflow, TestKey, signature).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongKey_ReturnsFalse()
    {
        var workflow = CreateSampleWorkflow();
        var signature = WorkflowSignature.Sign(workflow, TestKey);
        var wrongKey = Encoding.UTF8.GetBytes("wrong-key-definitely-not-right!");

        WorkflowSignature.Verify(workflow, wrongKey, signature).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedWorkflow_ReturnsFalse()
    {
        var workflow = CreateSampleWorkflow();
        var signature = WorkflowSignature.Sign(workflow, TestKey);

        workflow.Name = "evil-pipeline";

        WorkflowSignature.Verify(workflow, TestKey, signature).Should().BeFalse();
    }

    [Fact]
    public void Verify_ModifiedSteps_ReturnsFalse()
    {
        var workflow = CreateSampleWorkflow();
        var signature = WorkflowSignature.Sign(workflow, TestKey);

        workflow.Steps.Add(AgentStepDefinition.InvokeTool("backdoor"));

        WorkflowSignature.Verify(workflow, TestKey, signature).Should().BeFalse();
    }

    [Fact]
    public void Sign_IgnoresTimestampChanges()
    {
        var workflow = CreateSampleWorkflow();
        var sig1 = WorkflowSignature.Sign(workflow, TestKey);

        workflow.CreatedAt = DateTime.UtcNow.AddDays(-1);
        workflow.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        var sig2 = WorkflowSignature.Sign(workflow, TestKey);

        sig1.Should().Be(sig2);
    }

    [Fact]
    public void Sign_DeterministicForSameContent()
    {
        var w1 = CreateSampleWorkflow();
        var w2 = CreateSampleWorkflow();

        WorkflowSignature.Sign(w1, TestKey)
            .Should().Be(WorkflowSignature.Sign(w2, TestKey));
    }

    [Fact]
    public void Sign_NullDefinition_Throws()
    {
        var act = () => WorkflowSignature.Sign(null!, TestKey);
        act.Should().Throw<ArgumentNullException>();
    }
}
