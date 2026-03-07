using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class SecuritySpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidSecuritySpecification_RoundTripsFields()
    {
        var spec = SecuritySpecificationParser.Parse(ValidSecurityYaml());

        spec.Id.Should().Be("security.api-gateway");
        spec.AuthnModel.Should().Be("oauth2");
        spec.AuthzModel.Should().Be("rbac");
        spec.TrustZones.Should().ContainSingle(zone => zone.Name == "api-gateway");
        spec.Threats.Should().ContainSingle(threat => threat.Id == "threat.token-theft");
        spec.Controls.Should().ContainSingle(control => control.Id == "ctrl.short-lived-tokens");
        spec.ResidualRisks.Should().ContainSingle(risk => risk.ThreatId == "threat.token-theft");
    }

    [Fact]
    public void Validate_ValidSecuritySpecification_ReturnsNoErrors()
    {
        var spec = SecuritySpecificationParser.Parse(ValidSecurityYaml());

        var errors = SecuritySpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidSecuritySpecification_ReturnsErrors()
    {
        var spec = SecuritySpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Security
            id: bad
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            authnModel: kerberos
            authzModel: custom
            trustZones: []
            threats: []
            controls: []
            residualRisks: []
            trace:
              upstream: []
              downstream:
                deployment: []
                operations: []
                testing: []
            """);

        var errors = SecuritySpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match security.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("authnModel", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("authzModel", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trustZones must contain at least one", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("threats must contain at least one", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("controls must contain at least one", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_InvalidEnumValues_ReturnsErrors()
    {
        var spec = SecuritySpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Security
            id: security.test
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - reviewer
              lastReviewed: 2026-03-07
              changeReason: test
            authnModel: oauth2
            authzModel: rbac
            trustZones:
              - name: zone1
                level: top-secret
            threats:
              - id: threat.1
                description: A threat
                severity: extreme
                mitigatedBy: []
            controls:
              - id: ctrl.1
                description: A control
                type: reactive
            residualRisks: []
            trace:
              upstream:
                - specs/capabilities/examples/capabilities.example.yaml
              downstream:
                deployment: []
                operations: []
                testing: []
            """);

        var errors = SecuritySpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("trustZones[0].level must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("threats[0].severity must be one of", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("controls[0].type must be one of", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = SecuritySpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingTestingReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/security/examples/security.example.yaml",
            ValidSecurityYaml(testingRefs: ["tests/missing.cs"]));

        var errors = SecuritySpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("tests/missing.cs", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/security/schema/security.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/security/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: SecurityIndex
            entries:
              - id: security.api-gateway
                title: API Gateway Security
                path: specs/security/examples/security.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/security/examples/security.example.yaml", ValidSecurityYaml());
        _fixture.CreateFile("specs/capabilities/examples/capabilities.example.yaml", "capability");
        _fixture.CreateFile("tests/JD.AI.Tests/Specifications/SecuritySpecificationRepositoryTests.cs", "test");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidSecurityYaml(IReadOnlyList<string>? testingRefs = null)
    {
        var testingLines = string.Join(Environment.NewLine, (testingRefs ?? ["tests/JD.AI.Tests/Specifications/SecuritySpecificationRepositoryTests.cs"]).Select(item => $"      - {item}"));

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Security
            id: security.api-gateway
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-security-architecture-agent
              lastReviewed: 2026-03-07
              changeReason: Establish canonical security specification for JD.AI.
            authnModel: oauth2
            authzModel: rbac
            trustZones:
              - name: api-gateway
                level: dmz
            threats:
              - id: threat.token-theft
                description: An attacker steals an OAuth2 access token.
                severity: critical
                mitigatedBy:
                  - ctrl.short-lived-tokens
            controls:
              - id: ctrl.short-lived-tokens
                description: Access tokens expire within 15 minutes.
                type: preventive
            residualRisks:
              - threatId: threat.token-theft
                justification: Short-lived tokens limit blast radius but cannot prevent in-flight use.
            trace:
              upstream:
                - specs/capabilities/examples/capabilities.example.yaml
              downstream:
                deployment: []
                operations: []
                testing:
            {{testingLines}}
            """;
    }
}
