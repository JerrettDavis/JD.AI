using System.Text;
using FluentAssertions;
using JD.AI.Workflows;
using JD.AI.Workflows.Security;

namespace JD.AI.Tests.Workflows.Security;

public sealed class WorkflowIntegrityValidatorTests : IDisposable
{
    private static readonly byte[] TestKey = Encoding.UTF8.GetBytes("test-integrity-key-exactly-32b!");
    private readonly string _tempDir;

    public WorkflowIntegrityValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-wiv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    private static AgentWorkflowDefinition ValidWorkflow() => new()
    {
        Name = "test-workflow",
        Version = "1.0.0",
        Description = "Test",
        Steps = [AgentStepDefinition.RunSkill("step1")],
    };

    [Fact]
    public void Validate_ValidWorkflow_ReturnsValid()
    {
        var validator = new WorkflowIntegrityValidator();
        var result = validator.Validate(ValidWorkflow());

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyName_ReportsIssue()
    {
        var validator = new WorkflowIntegrityValidator();
        var workflow = ValidWorkflow();
        workflow.Name = "";

        var result = validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("name"));
    }

    [Fact]
    public void Validate_NoSteps_ReportsIssue()
    {
        var validator = new WorkflowIntegrityValidator();
        var workflow = new AgentWorkflowDefinition
        {
            Name = "empty", Version = "1.0.0",
        };

        var result = validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("step"));
    }

    [Fact]
    public void Validate_WithValidSignature_ReturnsValid()
    {
        var validator = new WorkflowIntegrityValidator(signingKey: TestKey);
        var workflow = ValidWorkflow();
        var sig = WorkflowSignature.Sign(workflow, TestKey);

        var result = validator.Validate(workflow, signature: sig);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingSignature_WhenRequired_ReportsIssue()
    {
        var validator = new WorkflowIntegrityValidator(signingKey: TestKey);
        var workflow = ValidWorkflow();

        var result = validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("unsigned"));
    }

    [Fact]
    public void Validate_InvalidSignature_ReportsIssue()
    {
        var validator = new WorkflowIntegrityValidator(signingKey: TestKey);
        var workflow = ValidWorkflow();

        var result = validator.Validate(workflow, signature: "deadbeef".PadRight(64, '0'));

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("invalid"));
    }

    [Fact]
    public void Validate_TrustedPublisher_ReturnsValid()
    {
        var registryPath = Path.Combine(_tempDir, "trust.json");
        var registry = new TrustedPublisherRegistry(registryPath);
        registry.Trust("alice");

        var validator = new WorkflowIntegrityValidator(trustRegistry: registry);
        var workflow = ValidWorkflow();

        var result = validator.Validate(workflow, author: "alice");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UntrustedPublisher_ReportsIssue()
    {
        var registryPath = Path.Combine(_tempDir, "trust.json");
        var registry = new TrustedPublisherRegistry(registryPath);

        var validator = new WorkflowIntegrityValidator(trustRegistry: registry);
        var workflow = ValidWorkflow();

        var result = validator.Validate(workflow, author: "evil-user");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("trusted"));
    }

    [Fact]
    public void Validate_ToolStepWithoutTarget_ReportsIssue()
    {
        var validator = new WorkflowIntegrityValidator();
        var workflow = new AgentWorkflowDefinition
        {
            Name = "bad-tool", Version = "1.0.0",
            Steps = [new AgentStepDefinition { Name = "step", Kind = AgentStepKind.Tool }],
        };

        var result = validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("target"));
    }

    [Fact]
    public void Validate_LoopWithoutCondition_ReportsIssue()
    {
        var validator = new WorkflowIntegrityValidator();
        var workflow = new AgentWorkflowDefinition
        {
            Name = "bad-loop", Version = "1.0.0",
            Steps = [new AgentStepDefinition { Name = "loop", Kind = AgentStepKind.Loop }],
        };

        var result = validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("condition"));
    }
}
