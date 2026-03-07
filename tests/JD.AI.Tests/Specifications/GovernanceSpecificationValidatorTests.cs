using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class GovernanceSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidGovernanceSpecification_RoundTripsFields()
    {
        var spec = GovernanceSpecificationParser.Parse(ValidGovernanceYaml());

        spec.Id.Should().Be("governance.specification-lifecycle");
        spec.OwnershipModel.Should().Be("codeowners");
        spec.ChangeProcess.Should().ContainSingle(process => process.Name == "Specification change proposal");
        spec.ApprovalGates.Should().ContainSingle(gate => gate.Type == "automated");
        spec.ReleasePolicy.Cadence.Should().Be("continuous");
    }

    [Fact]
    public void Validate_ValidGovernanceSpecification_ReturnsNoErrors()
    {
        var spec = GovernanceSpecificationParser.Parse(ValidGovernanceYaml());

        var errors = GovernanceSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidGovernanceSpecification_ReturnsErrors()
    {
        var spec = GovernanceSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Governance
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            ownershipModel: dictator
            changeProcess: []
            approvalGates:
              - name: ""
                type: unknown
                criteria: []
            releasePolicy:
              cadence: biweekly
              branchStrategy: ""
            auditRequirements: []
            trace:
              upstream: []
              downstream:
                allSpecTypes: []
            """);

        var errors = GovernanceSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match governance.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("ownershipModel", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("changeProcess must contain", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("approvalGates[0].type", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("releasePolicy.cadence", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("auditRequirements", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = GovernanceSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingUpstreamReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/governance/examples/governance.example.yaml",
            ValidGovernanceYaml(upstreamRefs: ["specs/vision/missing.yaml"]));

        var errors = GovernanceSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("specs/vision/missing.yaml", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingDownstreamReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/governance/examples/governance.example.yaml",
            ValidGovernanceYaml(allSpecTypeRefs: ["src/missing.cs"]));

        var errors = GovernanceSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("src/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/governance/schema/governance.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/governance/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: GovernanceIndex
            entries:
              - id: governance.specification-lifecycle
                title: Specification Lifecycle Governance
                path: specs/governance/examples/governance.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/governance/examples/governance.example.yaml", ValidGovernanceYaml());
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/GovernanceSpecificationRepositoryTests.cs", "test");
        _fixture.CreateFile("src/JD.AI.Core/Specifications/GovernanceSpecification.cs", "code");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidGovernanceYaml(
        IReadOnlyList<string>? upstreamRefs = null,
        IReadOnlyList<string>? allSpecTypeRefs = null)
    {
        var upstreamLines = string.Join(Environment.NewLine, (upstreamRefs ?? ["specs/vision/examples/vision.example.yaml"]).Select(item => $"      - {item}"));
        var allSpecTypeLines = string.Join(Environment.NewLine, (allSpecTypeRefs ?? ["tests/JD.AI.Tests/Specifications/GovernanceSpecificationRepositoryTests.cs", "src/JD.AI.Core/Specifications/GovernanceSpecification.cs"]).Select(item => $"        - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Governance
            id: governance.specification-lifecycle
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-governance-agent
              lastReviewed: 2026-03-07
              changeReason: Establish canonical governance specifications for JD.AI.
            ownershipModel: codeowners
            changeProcess:
              - name: Specification change proposal
                description: All specification changes must be submitted as pull requests.
                requiredApprovals: 1
            approvalGates:
              - name: CI validation gate
                type: automated
                criteria:
                  - All repository specification validators pass without errors.
            releasePolicy:
              cadence: continuous
              branchStrategy: trunk-based
              hotfixProcess: Fast-track PR with owner approval.
            auditRequirements:
              - Every specification change must include trace links.
            trace:
              upstream:
            {{upstreamLines}}
              downstream:
                allSpecTypes:
            {{allSpecTypeLines}}
            """;
    }
}
