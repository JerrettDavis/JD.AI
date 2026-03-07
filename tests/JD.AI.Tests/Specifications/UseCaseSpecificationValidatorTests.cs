using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class UseCaseSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidUseCaseSpecification_RoundTripsFields()
    {
        var spec = UseCaseSpecificationParser.Parse(ValidUseCaseYaml());

        spec.Id.Should().Be("usecase.validate-pull-request");
        spec.Actor.Should().Be("persona.delivery-agent");
        spec.CapabilityRef.Should().Be("capability.spec-validation");
        spec.WorkflowSteps.Should().Contain("Run schema and policy validators.");
    }

    [Fact]
    public void Validate_ValidUseCaseSpecification_ReturnsNoErrors()
    {
        var spec = UseCaseSpecificationParser.Parse(ValidUseCaseYaml());

        var errors = UseCaseSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidUseCaseSpecification_ReturnsErrors()
    {
        var spec = UseCaseSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: UseCase
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            actor: bad
            capabilityRef: bad
            preconditions: []
            workflowSteps: []
            expectedOutcomes: []
            failureScenarios: []
            trace:
              upstream: []
              downstream:
                behavior:
                  - ""
                testing:
                  - ""
                interfaces:
                  - ""
            """);

        var errors = UseCaseSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match usecase.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("actor must match persona.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("capabilityRef", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("workflowSteps", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.downstream.behavior", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = UseCaseSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_UnknownCapabilityReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/usecases/examples/usecases.example.yaml",
            ValidUseCaseYaml(capabilityRef: "capability.missing"));

        var errors = UseCaseSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("capability.missing", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingDownstreamFile_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/usecases/examples/usecases.example.yaml",
            ValidUseCaseYaml(testingRefs: ["tests/missing.cs"]));

        var errors = UseCaseSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("tests/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/capabilities/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: CapabilityIndex
            entries:
              - id: capability.spec-validation
                title: Specification Validation
                path: specs/capabilities/examples/capabilities.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/capabilities/examples/capabilities.example.yaml", "capability");
        _fixture.CreateFile("specs/usecases/schema/usecases.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/usecases/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: UseCaseIndex
            entries:
              - id: usecase.validate-pull-request
                title: Validate Pull Request
                path: specs/usecases/examples/usecases.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/capabilities/examples/capabilities.example.yaml", "capability");
        _fixture.CreateFile("specs/capabilities/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: CapabilityIndex
            entries:
              - id: capability.spec-validation
                title: Specification Validation
                path: specs/capabilities/examples/capabilities.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/capabilities/examples/capabilities.example.yaml", "capability");
        _fixture.CreateFile("specs/usecases/examples/usecases.example.yaml", ValidUseCaseYaml());
        _fixture.CreateFile("specs/capabilities/examples/capabilities.example.yaml", "capability");
        _fixture.CreateFile("specs/personas/examples/personas.example.yaml", "persona");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/UseCaseSpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("docs/reference/commands.md", "# Commands");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidUseCaseYaml(
        string capabilityRef = "capability.spec-validation",
        IReadOnlyList<string>? testingRefs = null)
    {
        var testingLines = string.Join(Environment.NewLine, (testingRefs ?? ["tests/JD.AI.Tests/Specifications/UseCaseSpecificationRepositoryTests.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: UseCase
            id: usecase.validate-pull-request
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-usecase-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical use case definitions for JD.AI.
            actor: persona.delivery-agent
            capabilityRef: {{capabilityRef}}
            preconditions:
              - Pull request includes modified repository specifications.
            workflowSteps:
              - Run schema and policy validators.
              - Publish traceability report.
            expectedOutcomes:
              - Pull request is blocked on validation failures.
            failureScenarios:
              - Missing upstream capability reference causes validation failure.
            trace:
              upstream:
                - specs/capabilities/examples/capabilities.example.yaml
              downstream:
                behavior:
                  - docs/reference/commands.md
                testing:
            {{testingLines}}
                interfaces: []
            """;
    }
}
