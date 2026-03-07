using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class DataSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidDataSpecification_RoundTripsFields()
    {
        var spec = DataSpecificationParser.Parse(ValidDataYaml());

        spec.Id.Should().Be("data.session-store");
        spec.ModelType.Should().Be("relational");
        spec.Schemas.Should().ContainSingle(schema => schema.Name == "Sessions");
        spec.Migrations.Should().ContainSingle(migration => migration.Version == "1.0.0");
        spec.Indexes.Should().Contain(index => index.Name == "IX_Sessions_UserId");
        spec.Constraints.Should().Contain(constraint => constraint.Contains("ExpiresAt", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ValidDataSpecification_ReturnsNoErrors()
    {
        var spec = DataSpecificationParser.Parse(ValidDataYaml());

        var errors = DataSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidDataSpecification_ReturnsErrors()
    {
        var spec = DataSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Data
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            modelType: invalid
            schemas: []
            migrations: []
            indexes: []
            constraints: []
            trace:
              upstream: []
              downstream:
                deployment: []
                operations: []
                testing: []
            """);

        var errors = DataSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match data.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("version must be greater than or equal to 1", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("modelType", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("schemas must contain at least one schema", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("constraints must contain at least one constraint", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BlankSchemaName_ReturnsError()
    {
        var spec = DataSpecificationParser.Parse(ValidDataYaml(schemaName: ""));

        var errors = DataSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("schemas[0].name is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = DataSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingTestingReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/data/examples/data.example.yaml",
            ValidDataYaml(testingRefs: ["tests/missing.cs"]));

        var errors = DataSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("tests/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/behavior/examples/behavior.example.yaml", "behavior");
        _fixture.CreateFile("specs/data/schema/data.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/data/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: DataIndex
            entries:
              - id: data.session-store
                title: Session Store Data Model
                path: specs/data/examples/data.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/data/examples/data.example.yaml", ValidDataYaml());
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/DataSpecificationRepositoryTests.cs", "test");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidDataYaml(
        string schemaName = "Sessions",
        IReadOnlyList<string>? testingRefs = null)
    {
        var testingLines = string.Join(Environment.NewLine, (testingRefs ?? ["tests/JD.AI.Tests/Specifications/DataSpecificationRepositoryTests.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Data
            id: data.session-store
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-data-spec-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical data specification for session persistence.
            modelType: relational
            schemas:
              - name: {{schemaName}}
                description: Tracks active user sessions.
                fields:
                  - Id (uniqueidentifier, PK)
                  - UserId (nvarchar(256), NOT NULL)
            migrations:
              - version: "1.0.0"
                description: Create Sessions table.
                reversible: true
            indexes:
              - name: IX_Sessions_UserId
                table: Sessions
                columns:
                  - UserId
            constraints:
              - ExpiresAt must be greater than CreatedAt.
            trace:
              upstream:
                - specs/behavior/examples/behavior.example.yaml
              downstream:
                deployment: []
                operations: []
                testing:
            {{testingLines}}
            """;
    }
}
