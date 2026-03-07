using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class AdrSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidAdrSpecification_RoundTripsFields()
    {
        var spec = AdrSpecificationParser.Parse(ValidAdrYaml());

        spec.Id.Should().Be("adr.modular-monolith-architecture");
        spec.Date.Should().Be("2026-03-07");
        spec.Context.Should().Contain("architecture style");
        spec.Decision.Should().Contain("modular monolith");
        spec.Alternatives.Should().HaveCount(2);
        spec.Consequences.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Validate_ValidAdrSpecification_ReturnsNoErrors()
    {
        var spec = AdrSpecificationParser.Parse(ValidAdrYaml());

        var errors = AdrSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidAdrSpecification_ReturnsErrors()
    {
        var spec = AdrSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Adr
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            date: not-a-date
            context: ""
            decision: ""
            alternatives: []
            consequences: []
            supersedes:
              - bad-id
            conflictsWith: []
            trace:
              upstream: []
              downstream:
                implementation: []
                governance: []
            """);

        var errors = AdrSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match adr.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("version must be greater than or equal to 1", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("context is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("decision is required", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("date must be a valid", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("alternatives must contain at least one alternative", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("consequences must contain at least one consequence", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("supersedes[0] must match adr.", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = AdrSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingImplementationReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/adrs/examples/adrs.example.yaml",
            ValidAdrYaml(implementationRefs: ["src/missing.cs"]));

        var errors = AdrSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("specs/adrs/schema/adrs.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/adrs/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: AdrIndex
            entries:
              - id: adr.modular-monolith-architecture
                title: Modular Monolith Architecture
                path: specs/adrs/examples/adrs.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/adrs/examples/adrs.example.yaml", ValidAdrYaml());
        _fixture.CreateFile("src/JD.AI.Core/Specifications/AdrSpecification.cs", "code");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/AdrSpecificationRepositoryTests.cs", "test");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidAdrYaml(IReadOnlyList<string>? implementationRefs = null)
    {
        var implLines = string.Join(Environment.NewLine, (implementationRefs ?? ["src/JD.AI.Core/Specifications/AdrSpecification.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Adr
            id: adr.modular-monolith-architecture
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-adr-curator
              lastReviewed: 2026-03-07
              changeReason: Establish canonical ADR specifications for JD.AI.
            date: 2026-03-07
            context: The system needs a well-defined architecture style that balances modularity and simplicity.
            decision: Adopt a modular monolith architecture with explicit module boundaries.
            alternatives:
              - title: Microservices from the start
                description: Decompose the system into independently deployable services from day one.
                pros:
                  - Independent scaling per service.
                cons:
                  - High operational overhead for a small team.
              - title: Traditional layered monolith
                description: Organize code by technical layer without module boundaries.
                pros:
                  - Simple initial setup.
                cons:
                  - Cross-cutting dependencies accumulate quickly.
            consequences:
              - All bounded contexts must communicate through well-defined public APIs.
              - Future service extraction requires only replacing in-process calls with network calls.
            supersedes: []
            conflictsWith: []
            trace:
              upstream:
                - specs/vision/examples/vision.example.yaml
              downstream:
                implementation:
            {{implLines}}
                governance:
                  - tests/JD.AI.Tests/Specifications/AdrSpecificationRepositoryTests.cs
            """;
    }
}
