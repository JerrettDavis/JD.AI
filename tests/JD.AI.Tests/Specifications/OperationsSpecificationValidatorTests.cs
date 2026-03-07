using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class OperationsSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidOperationsSpecification_RoundTripsFields()
    {
        var spec = OperationsSpecificationParser.Parse(ValidOperationsYaml());

        spec.Id.Should().Be("operations.jdai-gateway");
        spec.Service.Should().Be("jdai-gateway");
        spec.Runbooks.Should().ContainSingle(runbook => runbook.Name == "Gateway Health Degradation");
        spec.IncidentLevels.Should().Contain(level => level.Level == "sev1");
        spec.EscalationPaths.Should().ContainSingle(path => path.Level == "sev1");
    }

    [Fact]
    public void Validate_ValidOperationsSpecification_ReturnsNoErrors()
    {
        var spec = OperationsSpecificationParser.Parse(ValidOperationsYaml());

        var errors = OperationsSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidOperationsSpecification_ReturnsErrors()
    {
        var spec = OperationsSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Operations
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            service: ""
            runbooks:
              - name: ""
                triggerCondition: ""
                steps: []
            incidentLevels:
              - level: sev5
                description: ""
                responseTime: ""
            responseSlos: []
            escalationPaths:
              - level: ""
                contacts: []
            trace:
              upstream: []
              downstream:
                governance: []
                audits: []
            """);

        var errors = OperationsSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match operations.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("service is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("runbooks[0].name", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("runbooks[0].triggerCondition", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("runbooks[0].steps", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("incidentLevels[0].level must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("incidentLevels[0].description", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("escalationPaths[0].level", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("escalationPaths[0].contacts", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.upstream", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = OperationsSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingGovernanceReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/operations/examples/operations.example.yaml",
            ValidOperationsYaml(governanceRefs: ["tests/missing.cs"]));

        var errors = OperationsSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("tests/missing.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingAuditReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/operations/examples/operations.example.yaml",
            ValidOperationsYaml(auditRefs: ["src/missing.cs"]));

        var errors = OperationsSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("specs/operations/schema/operations.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/operations/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: OperationsIndex
            entries:
              - id: operations.jdai-gateway
                title: JD.AI Gateway Operations
                path: specs/operations/examples/operations.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/operations/examples/operations.example.yaml", ValidOperationsYaml());
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/OperationsSpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/OperationsSpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidOperationsYaml(
        IReadOnlyList<string>? governanceRefs = null,
        IReadOnlyList<string>? auditRefs = null)
    {
        var governanceLines = string.Join(Environment.NewLine, (governanceRefs ?? ["tests/JD.AI.Tests/Specifications/OperationsSpecificationRepositoryTests.cs"]).Select(item => $"      - {item}"));
        var auditLines = string.Join(Environment.NewLine, (auditRefs ?? ["src/JD.AI.Core/Specifications/OperationsSpecification.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Operations
            id: operations.jdai-gateway
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-operations-runbook-agent
              lastReviewed: 2026-03-07
              changeReason: Establish canonical operations specifications for JD.AI.
            service: jdai-gateway
            runbooks:
              - name: Gateway Health Degradation
                description: Steps to diagnose and restore gateway health.
                triggerCondition: P95 latency exceeds 500ms for 5 consecutive minutes.
                steps:
                  - Check gateway pod status and recent restart events.
                  - Review upstream dependency health endpoints.
            incidentLevels:
              - level: sev1
                description: Complete service outage affecting all users.
                responseTime: 5 minutes
              - level: sev2
                description: Partial degradation affecting a subset of users.
                responseTime: 15 minutes
            responseSlos:
              - level: sev1
                acknowledgeWithin: 5 minutes
                resolveWithin: 1 hour
            escalationPaths:
              - level: sev1
                contacts:
                  - on-call-primary
                  - engineering-lead
            trace:
              upstream:
                - specs/vision/examples/vision.example.yaml
              downstream:
                governance:
            {{governanceLines}}
                audits:
            {{auditLines}}
            """;
    }
}
