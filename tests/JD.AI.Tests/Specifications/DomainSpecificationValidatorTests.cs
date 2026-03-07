using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class DomainSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidDomainSpecification_RoundTripsFields()
    {
        var spec = DomainSpecificationParser.Parse(ValidDomainYaml());

        spec.Id.Should().Be("domain.session-management");
        spec.BoundedContext.Should().Be("session-management");
        spec.Entities.Should().ContainSingle(entity => entity.Name == "Session");
        spec.Aggregates.Should().ContainSingle(aggregate => aggregate.RootEntity == "Session");
    }

    [Fact]
    public void Validate_ValidDomainSpecification_ReturnsNoErrors()
    {
        var spec = DomainSpecificationParser.Parse(ValidDomainYaml());

        var errors = DomainSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidDomainSpecification_ReturnsErrors()
    {
        var spec = DomainSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Domain
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            boundedContext: ""
            entities:
              - name: ""
                description: ""
                properties: []
            valueObjects: []
            aggregates:
              - name: ""
                rootEntity: ""
                members: []
            invariants: []
            trace:
              upstream: []
              downstream:
                data: []
                interfaces: []
                architecture: []
            """);

        var errors = DomainSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match domain.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("version must be greater than or equal to 1", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("status must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("boundedContext is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("entities[0].name is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("aggregates[0].name is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("aggregates[0].rootEntity is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("invariants must contain at least one invariant", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.upstream", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = DomainSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingDownstreamReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/domain/examples/domain.example.yaml",
            ValidDomainYaml(architectureRefs: ["src/missing.cs"]));

        var errors = DomainSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/domain/schema/domain.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/domain/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: DomainIndex
            entries:
              - id: domain.session-management
                title: Session Management Domain Model
                path: specs/domain/examples/domain.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/domain/examples/domain.example.yaml", ValidDomainYaml());
        _fixture.CreateFile("specs/capabilities/examples/capabilities.example.yaml", "capability");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/DomainSpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/DomainSpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidDomainYaml(
        IReadOnlyList<string>? architectureRefs = null)
    {
        var architectureLines = string.Join(Environment.NewLine, (architectureRefs ?? ["src/JD.AI.Core/Specifications/DomainSpecification.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Domain
            id: domain.session-management
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-domain-model-architect
              lastReviewed: 2026-03-07
              changeReason: Establish the first canonical domain model specification.
            boundedContext: session-management
            entities:
              - name: Session
                description: Represents an active user interaction session.
                properties:
                  - sessionId
                  - userId
                  - createdAt
                  - expiresAt
            valueObjects:
              - name: SessionToken
                description: Immutable token identifying a session.
                properties:
                  - value
                  - issuedAt
            aggregates:
              - name: SessionAggregate
                rootEntity: Session
                members:
                  - SessionToken
            invariants:
              - A session must have a non-expired token to be considered active.
              - Session expiry must be after creation time.
            trace:
              upstream:
                - specs/capabilities/examples/capabilities.example.yaml
              downstream:
                data:
                  - tests/JD.AI.Tests/Specifications/DomainSpecificationRepositoryTests.cs
                interfaces: []
                architecture:
            {{architectureLines}}
            """;
    }
}
