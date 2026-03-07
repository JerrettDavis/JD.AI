using FluentAssertions;
using JD.AI.Core.Specifications;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Specifications;

public sealed class PersonaSpecificationValidatorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Parse_ValidPersonaSpecification_RoundTripsFields()
    {
        var spec = PersonaSpecificationParser.Parse(ValidPersonaYaml());

        spec.Id.Should().Be("persona.platform-admin");
        spec.ActorType.Should().Be("administrator");
        spec.RoleName.Should().Be("PlatformAdmin");
        spec.Permissions.Allowed.Should().Contain("deployment.promote");
        spec.Trace.Upstream.Should().Contain("specs/vision/examples/vision.example.yaml");
    }

    [Fact]
    public void Validate_ValidPersonaSpecification_ReturnsNoErrors()
    {
        var spec = PersonaSpecificationParser.Parse(ValidPersonaYaml());

        var errors = PersonaSpecificationValidator.Validate(spec);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidPersonaSpecification_ReturnsErrors()
    {
        var spec = PersonaSpecificationParser.Parse("""
            apiVersion: jdai.upss/v1
            kind: Persona
            id: invalid
            version: 0
            status: pending
            metadata:
              owners: []
              reviewers: []
              lastReviewed: no
              changeReason: ""
            actorType: nope
            roleName: ""
            description: ""
            permissions:
              allowed: []
              denied:
                - ""
            trustBoundaries:
              - boundary: ""
                accessLevel: unknown
                justification: ""
            responsibilities: []
            trace:
              upstream: []
              downstream:
                capabilities:
                  - bad
                policies:
                  - bad
                security:
                  - bad
                useCases:
                  - bad
            """);

        var errors = PersonaSpecificationValidator.Validate(spec);

        errors.Should().Contain(error => error.Contains("id must match persona.", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("actorType", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("permissions.allowed", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trustBoundaries[0].accessLevel", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.downstream.capabilities", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("trace.downstream.policies", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_CheckedInArtifacts_ReturnNoErrors()
    {
        var errors = PersonaSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRepository_MissingUpstreamReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/personas/examples/personas.example.yaml",
            ValidPersonaYaml(upstream: ["specs/vision/missing.yaml"]));

        var errors = PersonaSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("specs/vision/missing.yaml", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_MissingPolicyIndex_FailsWhenPolicyRefsExist()
    {
        SeedRepository();
        _fixture.CreateFile(
            "specs/personas/examples/personas.example.yaml",
            ValidPersonaYaml(policies: ["policy.change-control"]));

        var errors = PersonaSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("specs/policies/index.yaml is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRepository_UnknownCapabilityReference_Fails()
    {
        SeedRepository();
        _fixture.CreateFile("specs/capabilities/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: CapabilityIndex
            entries:
              - id: capability.other
                path: specs/capabilities/examples/other.yaml
            """);
        _fixture.CreateFile(
            "specs/personas/examples/personas.example.yaml",
            ValidPersonaYaml(capabilities: ["capability.deployment-management"]));

        var errors = PersonaSpecificationValidator.ValidateRepository(_fixture.DirectoryPath);

        errors.Should().ContainSingle(error => error.Contains("capability.deployment-management", StringComparison.Ordinal));
    }

    private void SeedRepository()
    {
        _fixture.CreateFile("JD.AI.slnx", "<Solution />");
        _fixture.CreateFile("specs/vision/examples/vision.example.yaml", "vision");
        _fixture.CreateFile("specs/personas/schema/personas.schema.json", """{"type":"object"}""");
        _fixture.CreateFile("specs/personas/index.yaml", """
            apiVersion: jdai.upss/v1
            kind: PersonaIndex
            entries:
              - id: persona.platform-admin
                title: Platform Administrator
                path: specs/personas/examples/personas.example.yaml
                status: draft
            """);
        _fixture.CreateFile("specs/personas/examples/personas.example.yaml", ValidPersonaYaml());
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }

    private static string ValidPersonaYaml(
        IReadOnlyList<string>? upstream = null,
        IReadOnlyList<string>? capabilities = null,
        IReadOnlyList<string>? policies = null)
    {
        var upstreamLines = string.Join(Environment.NewLine, (upstream ?? ["specs/vision/examples/vision.example.yaml"]).Select(item => $"    - {item}"));
        var capabilityBlock = BuildBlock(capabilities ?? Array.Empty<string>(), "capabilities");
        var policyBlock = BuildBlock(policies ?? Array.Empty<string>(), "policies");
        var securityBlock = BuildBlock(Array.Empty<string>(), "security");
        var useCaseBlock = BuildBlock(Array.Empty<string>(), "useCases");

        return $$"""
            apiVersion: jdai.upss/v1
            kind: Persona
            id: persona.platform-admin
            version: 1
            status: draft
            metadata:
              owners:
                - JerrettDavis
              reviewers:
                - upss-persona-role-architect
              lastReviewed: 2026-03-07
              changeReason: Establish canonical persona and role definitions for JD.AI.
            actorType: administrator
            roleName: PlatformAdmin
            description: Operates JD.AI environments, manages privileged workflows, and enforces change controls.
            permissions:
              allowed:
                - deployment.promote
                - policy.override_with_approval
              denied:
                - audit_log.delete
            trustBoundaries:
              - boundary: prod-environment
                accessLevel: elevated
                justification: Production operations require controlled administrative access.
            responsibilities:
              - Maintain deployment health and controlled promotion flows.
              - Enforce governance and approval policies for privileged actions.
            trace:
              upstream:
            {{upstreamLines}}
              downstream:
            {{capabilityBlock}}
            {{policyBlock}}
            {{securityBlock}}
            {{useCaseBlock}}
            """;
    }

    private static string BuildBlock(IReadOnlyList<string> items, string name)
    {
        if (items.Count == 0)
            return $"    {name}: []";

        return "    " + name + ":" + Environment.NewLine +
            string.Join(Environment.NewLine, items.Select(item => $"      - {item}"));
    }
}
