using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class CapabilitySpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidCapabilitySpecification_RoundTripsFields()
    {
        var spec = CapabilitySpecificationParser.Parse(ValidCapabilityYaml());

        spec.Id.Should().Be("capability.spec-validation");
        spec.Name.Should().Be("Specification Validation");
        spec.Actors.Should().Contain("persona.platform-admin");
        spec.Trace.VisionRefs.Should().Contain("vision.jdai.product");
    }

    [Fact]
    public void Validate_ValidCapabilitySpecification_ReturnsNoErrors()
    {
        var spec = CapabilitySpecificationParser.Parse(ValidCapabilityYaml());

        var errors = CapabilitySpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidCapabilitySpecification_ReturnsErrors()
    {
        var spec = CapabilitySpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Capability
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: nope
              changeReason: ""
            name: ""
            description: ""
            maturity: unknown
            actors:
              - bad
            dependencies:
              - bad
            relatedUseCases:
              - bad
            trace:
              visionRefs:
                - bad
              upstream: []
              downstream:
                useCases:
                  - bad
                architecture:
                  - ""
                testing:
                  - ""
            """);

        var errors = CapabilitySpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match capability.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("maturity", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("actors[0]", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("dependencies[0]", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.visionRefs[0]", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = CapabilitySpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_UnknownVisionReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/capabilities/examples/capabilities.example.yaml",
            ValidCapabilityYaml(visionRefs: ["vision.missing"]));

        var errors = CapabilitySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("vision.missing", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingUseCaseIndex_FailsWhenDeclared()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/capabilities/examples/capabilities.example.yaml",
            ValidCapabilityYaml(relatedUseCases: ["usecase.validate-pull-request"]));

        var errors = CapabilitySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("specs/usecases/index.yaml is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_UnknownCapabilityDependency_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/capabilities/examples/capabilities.example.yaml",
            ValidCapabilityYaml(dependencies: ["capability.other"]));

        var errors = CapabilitySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("capability.other", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/vision/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: VisionIndex
            entries:
              - id: vision.jdai.product
                title: JD.AI Product Vision
                path: specs/vision/examples/vision.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("docs/architecture/index.md", "# Architecture");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/CapabilitySpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("specs/capabilities/schema/capabilities.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/capabilities/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: CapabilityIndex
            entries:
              - id: capability.spec-validation
                title: Specification Validation
                path: specs/capabilities/examples/capabilities.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/capabilities/examples/capabilities.example.yaml", ValidCapabilityYaml());
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidCapabilityYaml(
        IReadOnlyList<string>? visionRefs = null,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyList<string>? relatedUseCases = null)
    {
        var visionLines = string.Join(Environment.NewLine, (visionRefs ?? ["vision.jdai.product"]).Select(item => $"    - {item}"));
        var dependencyBlock = BuildBlock(dependencies ?? Array.Empty<string>(), "dependencies");
        var useCaseBlock = BuildBlock(relatedUseCases ?? Array.Empty<string>(), "relatedUseCases");

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Capability
            id: capability.spec-validation
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-capability-map-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical capability mapping for JD.AI.
            name: Specification Validation
            description: Validate repo-native specifications for structure, traceability, and drift.
            maturity: beta
            actors:
              - persona.platform-admin
              - persona.delivery-agent
            {{dependencyBlock}}
            {{useCaseBlock}}
            trace:
              visionRefs:
            {{visionLines}}
              upstream:
                - specs/vision/examples/vision.example.yaml
              downstream:
                useCases: []
                architecture:
                  - docs/architecture/index.md
                testing:
                  - tests/JD.AI.Tests/Specifications/CapabilitySpecificationRepositoryTests.cs
            """;
    }

    private static string BuildBlock(IReadOnlyList<string> items, string name)
    {
        if (items.Count == 0)
            return $"{name}: []";

        return name + ":" + Environment.NewLine +
            string.Join(Environment.NewLine, items.Select(item => $"  - {item}"));
    }
}
