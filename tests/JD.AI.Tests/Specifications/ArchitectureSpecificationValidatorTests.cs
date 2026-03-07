using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class ArchitectureSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidArchitectureSpecification_RoundTripsFields()
    {
        var spec = ArchitectureSpecificationParser.Parse(ValidArchitectureYaml());

        spec.Id.Should().Be("architecture.jdai-gateway");
        spec.ArchitectureStyle.Should().Be("modular-monolith");
        spec.Systems.Should().ContainSingle(system => system.Name == "JD.AI Gateway");
        spec.Containers.Should().Contain(container => container.Name == "Core Library");
        spec.Components.Should().Contain(component => component.Name == "SpecificationEngine");
        spec.DependencyRules.Should().Contain(rule => rule.From == "CLI Host" && rule.To == "Core Library" && rule.Allowed);
    }

    [Fact]
    public void Validate_ValidArchitectureSpecification_ReturnsNoErrors()
    {
        var spec = ArchitectureSpecificationParser.Parse(ValidArchitectureYaml());

        var errors = ArchitectureSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidArchitectureSpecification_ReturnsErrors()
    {
        var spec = ArchitectureSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Architecture
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            architectureStyle: serverless
            systems: []
            containers: []
            components: []
            dependencyRules: []
            trace:
              upstream: []
              downstream:
                deployment: []
                security: []
                operations: []
            """);

        var errors = ArchitectureSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match architecture.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("architectureStyle", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("systems must contain", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("containers must contain", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("components must contain", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("dependencyRules must contain", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BlankSystemAndRuleNames_ReturnsErrors()
    {
        var spec = ArchitectureSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Architecture
            id: architecture.test
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-architecture-c4-architect
              lastReviewed: 2026-03-07
              changeReason: Test blank names.
            architectureStyle: layered
            systems:
              - name: ""
                description: blank
                type: backend
            containers:
              - name: ""
                technology: .NET
                system: test
            components:
              - name: ""
                container: test
                responsibility: test
            dependencyRules:
              - from: ""
                to: ""
                allowed: true
            trace:
              upstream:
                - specs/vision/examples/vision.example.yaml
              downstream:
                deployment: []
                security: []
                operations: []
            """);

        var errors = ArchitectureSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("systems[0].name", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("containers[0].name", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("components[0].name", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("dependencyRules[0].from", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("dependencyRules[0].to", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = ArchitectureSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingUpstreamReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/architecture/examples/architecture.example.yaml",
            ValidArchitectureYaml(upstreamRefs: ["specs/missing/file.yaml"]));

        var errors = ArchitectureSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("specs/missing/file.yaml", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("specs/architecture/schema/architecture.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/architecture/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: ArchitectureIndex
            entries:
              - id: architecture.jdai-gateway
                title: JD.AI Gateway Architecture
                path: specs/architecture/examples/architecture.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/architecture/examples/architecture.example.yaml", ValidArchitectureYaml());
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidArchitectureYaml(
        IReadOnlyList<string>? upstreamRefs = null)
    {
        var upstreamLines = string.Join(Environment.NewLine, (upstreamRefs ?? ["specs/vision/examples/vision.example.yaml"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Architecture
            id: architecture.jdai-gateway
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-architecture-c4-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical architecture specification for JD.AI.
            architectureStyle: modular-monolith
            systems:
              - name: JD.AI Gateway
                description: Primary entry point for agent-driven workflows.
                type: backend
            containers:
              - name: Core Library
                technology: .NET 9 / C#
                system: JD.AI Gateway
            components:
              - name: SpecificationEngine
                container: Core Library
                responsibility: Parse, validate, and manage UPSS specification documents.
            dependencyRules:
              - from: CLI Host
                to: Core Library
                allowed: true
            trace:
              upstream:
            {{upstreamLines}}
              downstream:
                deployment: []
                security: []
                operations: []
            """;
    }
}
