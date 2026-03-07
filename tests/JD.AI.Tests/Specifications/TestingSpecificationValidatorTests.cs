using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class TestingSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidTestingSpecification_RoundTripsFields()
    {
        var spec = TestingSpecificationParser.Parse(ValidTestingYaml());

        spec.Id.Should().Be("testing.specification-validation");
        spec.VerificationLevels.Should().Contain("unit");
        spec.BehaviorRefs.Should().ContainSingle(r => r == "behavior.validate-pull-request");
        spec.CoverageTargets.Should().ContainSingle(t => t.Scope == "JD.AI.Core.Specifications");
        spec.GenerationRules.Should().ContainSingle(r => r.Strategy == "manual");
    }

    [Fact]
    public void Validate_ValidTestingSpecification_ReturnsNoErrors()
    {
        var spec = TestingSpecificationParser.Parse(ValidTestingYaml());

        var errors = TestingSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidTestingSpecification_ReturnsErrors()
    {
        var spec = TestingSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Testing
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            verificationLevels:
              - unknown
            behaviorRefs:
              - bad-ref
            qualityRefs:
              - bad-quality
            coverageTargets: []
            generationRules:
              - source: foo
                strategy: invalid
            trace:
              upstream: []
              downstream:
                ci: []
                release: []
            """);

        var errors = TestingSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match testing.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("verificationLevels entry 'unknown'", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("behaviorRefs entry 'bad-ref'", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("qualityRefs entry 'bad-quality'", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("coverageTargets must contain at least one target", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("generationRules entry strategy 'invalid'", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = TestingSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingCiReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/testing/examples/testing.example.yaml",
            ValidTestingYaml(ciRefs: ["tests/missing.cs"]));

        var errors = TestingSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("tests/missing.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingReleaseReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/testing/examples/testing.example.yaml",
            ValidTestingYaml(releaseRefs: ["src/missing.cs"]));

        var errors = TestingSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/testing/schema/testing.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/testing/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: TestingIndex
            entries:
              - id: testing.specification-validation
                title: Specification Validation Testing
                path: specs/testing/examples/testing.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/testing/examples/testing.example.yaml", ValidTestingYaml());
        _fixture.CreateFile("specs/behavior/examples/behavior.example.yaml", "behavior");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/TestingSpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/TestingSpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidTestingYaml(
        IReadOnlyList<string>? ciRefs = null,
        IReadOnlyList<string>? releaseRefs = null)
    {
        var ciLines = string.Join(Environment.NewLine, (ciRefs ?? ["tests/JD.AI.Tests/Specifications/TestingSpecificationRepositoryTests.cs"]).Select(item => $"      - {item}"));
        var releaseLines = string.Join(Environment.NewLine, (releaseRefs ?? ["src/JD.AI.Core/Specifications/TestingSpecification.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Testing
            id: testing.specification-validation
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-testing-strategy-agent
              lastReviewed: 2026-03-07
              changeReason: Establish canonical testing specifications for JD.AI.
            verificationLevels:
              - unit
              - integration
            behaviorRefs:
              - behavior.validate-pull-request
            qualityRefs: []
            coverageTargets:
              - scope: JD.AI.Core.Specifications
                target: "80%"
                metric: line
            generationRules:
              - source: specs/testing/examples/testing.example.yaml
                strategy: manual
            trace:
              upstream:
                - specs/behavior/examples/behavior.example.yaml
              downstream:
                ci:
            {{ciLines}}
                release:
            {{releaseLines}}
            """;
    }
}
