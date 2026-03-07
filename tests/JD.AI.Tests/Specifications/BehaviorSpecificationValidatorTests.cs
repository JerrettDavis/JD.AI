using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class BehaviorSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidBehaviorSpecification_RoundTripsFields()
    {
        var spec = BehaviorSpecificationParser.Parse(ValidBehaviorYaml());

        spec.Id.Should().Be("behavior.validate-pull-request");
        spec.UseCaseRef.Should().Be("usecase.validate-pull-request");
        spec.BddScenarios.Should().ContainSingle(scenario => scenario.Title == "Validation succeeds for conforming specifications");
        spec.StateMachine.Transitions.Should().ContainSingle(transition => transition.On == "all_checks_pass");
    }

    [Fact]
    public void Validate_ValidBehaviorSpecification_ReturnsNoErrors()
    {
        var spec = BehaviorSpecificationParser.Parse(ValidBehaviorYaml());

        var errors = BehaviorSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidBehaviorSpecification_ReturnsErrors()
    {
        var spec = BehaviorSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Behavior
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            useCaseRef: bad
            bddScenarios:
              - title: ""
                given: []
                when: []
                then: []
            stateMachine:
              initialState: Missing
              states:
                - id: Ready
                - id: Ready
              transitions:
                - from: Missing
                  to: Gone
                  on: ""
                  guards:
                    - ""
                  actions:
                    - ""
            assertions: []
            trace:
              upstream: []
              downstream:
                testing: []
                interfaces:
                  - ""
                code: []
            """);

        var errors = BehaviorSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match behavior.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("useCaseRef", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("bddScenarios[0].given", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("stateMachine.states must have unique ids", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("stateMachine.transitions[0].to", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.downstream.code", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = BehaviorSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_UnknownUseCaseReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/behavior/examples/behavior.example.yaml",
            ValidBehaviorYaml(useCaseRef: "usecase.missing"));

        var errors = BehaviorSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("usecase.missing", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingCodeReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/behavior/examples/behavior.example.yaml",
            ValidBehaviorYaml(codeRefs: ["src/missing.cs"]));

        var errors = BehaviorSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
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
        _fixture.CreateFile("specs/usecases/examples/usecases.example.yaml", "usecase");
        _fixture.CreateFile("specs/behavior/schema/behavior.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/behavior/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: BehaviorIndex
            entries:
              - id: behavior.validate-pull-request
                title: Validate Pull Request Behavior
                path: specs/behavior/examples/behavior.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/behavior/examples/behavior.example.yaml", ValidBehaviorYaml());
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/BehaviorSpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/BehaviorSpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidBehaviorYaml(
        string useCaseRef = "usecase.validate-pull-request",
        IReadOnlyList<string>? codeRefs = null)
    {
        var codeLines = string.Join(Environment.NewLine, (codeRefs ?? ["src/JD.AI.Core/Specifications/BehaviorSpecification.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Behavior
            id: behavior.validate-pull-request
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-behavioral-spec-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical behavior specifications for JD.AI.
            useCaseRef: {{useCaseRef}}
            bddScenarios:
              - title: Validation succeeds for conforming specifications
                given:
                  - The pull request changes repository specifications.
                when:
                  - The validation workflow runs.
                then:
                  - Validation transitions to Validated.
            stateMachine:
              initialState: Pending
              states:
                - id: Pending
                - id: Validated
                  terminal: true
                - id: Failed
                  terminal: true
              transitions:
                - from: Pending
                  to: Validated
                  on: all_checks_pass
                  actions:
                    - Publish validation summary.
                - from: Pending
                  to: Failed
                  on: check_failure
                  actions:
                    - Publish diagnostics.
            assertions:
              - Successful validations must produce a summary.
            trace:
              upstream:
                - specs/usecases/examples/usecases.example.yaml
              downstream:
                testing:
                  - tests/JD.AI.Tests/Specifications/BehaviorSpecificationRepositoryTests.cs
                interfaces: []
                code:
            {{codeLines}}
            """;
    }
}
