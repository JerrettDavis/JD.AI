using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.History;

namespace JD.AI.Tests.Workflows.History;

public sealed class StepFingerprintTests
{
    [Fact]
    public void Compute_SameInputs_SameFingerprint()
    {
        var step = AgentStepDefinition.RunSkill("MySkill");

        var fp1 = StepFingerprint.Compute(step);
        var fp2 = StepFingerprint.Compute(step);

        fp1.Should().Be(fp2);
    }

    [Fact]
    public void Compute_DifferentKind_DifferentFingerprint()
    {
        var skillStep = AgentStepDefinition.RunSkill("MySkill");
        var toolStep = AgentStepDefinition.InvokeTool("MySkill");

        var skillFp = StepFingerprint.Compute(skillStep);
        var toolFp = StepFingerprint.Compute(toolStep);

        skillFp.Should().NotBe(toolFp);
    }

    [Fact]
    public void Compute_DifferentName_DifferentFingerprint()
    {
        var step1 = AgentStepDefinition.RunSkill("SkillA");
        var step2 = AgentStepDefinition.RunSkill("SkillB");

        var fp1 = StepFingerprint.Compute(step1);
        var fp2 = StepFingerprint.Compute(step2);

        fp1.Should().NotBe(fp2);
    }

    [Fact]
    public void Compute_DifferentTarget_DifferentFingerprint()
    {
        var step1 = AgentStepDefinition.InvokeTool("Tool", "target-a");
        var step2 = AgentStepDefinition.InvokeTool("Tool", "target-b");

        var fp1 = StepFingerprint.Compute(step1);
        var fp2 = StepFingerprint.Compute(step2);

        fp1.Should().NotBe(fp2);
    }

    [Fact]
    public void Compute_NullTargetAndEmptyTarget_SameFingerprint()
    {
        var step1 = new AgentStepDefinition { Name = "Test", Kind = AgentStepKind.Skill, Target = null };
        var step2 = new AgentStepDefinition { Name = "Test", Kind = AgentStepKind.Skill, Target = string.Empty };

        var fp1 = StepFingerprint.Compute(step1);
        var fp2 = StepFingerprint.Compute(step2);

        // Both null and empty normalize to empty string in fingerprint
        fp1.Should().Be(fp2);
    }

    [Fact]
    public void Compute_DirectAndViaStep_SameFingerprint()
    {
        var step = AgentStepDefinition.RunSkill("MySkill");

        var fpDirect = StepFingerprint.Compute(step.Kind, step.Target, step.Name);
        var fpViaStep = StepFingerprint.Compute(step);

        fpDirect.Should().Be(fpViaStep);
    }

    [Fact]
    public void Compute_AllStepKinds_ProduceDifferentFingerprints()
    {
        var skillStep = new AgentStepDefinition { Name = "Test", Kind = AgentStepKind.Skill, Target = "test" };
        var toolStep = new AgentStepDefinition { Name = "Test", Kind = AgentStepKind.Tool, Target = "test" };
        var nestedStep = new AgentStepDefinition { Name = "Test", Kind = AgentStepKind.Nested, Target = "test" };
        var loopStep = new AgentStepDefinition { Name = "Test", Kind = AgentStepKind.Loop, Target = "test" };
        var conditionalStep = new AgentStepDefinition { Name = "Test", Kind = AgentStepKind.Conditional, Target = "test" };
        var agentStep = new AgentStepDefinition { Name = "Test", Kind = AgentStepKind.Agent, Target = "test" };

        var fps = new[]
        {
            StepFingerprint.Compute(skillStep),
            StepFingerprint.Compute(toolStep),
            StepFingerprint.Compute(nestedStep),
            StepFingerprint.Compute(loopStep),
            StepFingerprint.Compute(conditionalStep),
            StepFingerprint.Compute(agentStep),
        };

        // All fingerprints should be unique
        fps.Distinct(StringComparer.Ordinal).Should().HaveCount(6);
    }

    [Fact]
    public void Compute_Deterministic()
    {
        var step = AgentStepDefinition.RunSkill("DeterministicSkill");

        var fingerprints = Enumerable.Range(0, 10)
            .Select(_ => StepFingerprint.Compute(step))
            .ToList();

        // All 10 computations should produce the same result
        fingerprints.Should().AllBe(fingerprints[0]);
    }
}
