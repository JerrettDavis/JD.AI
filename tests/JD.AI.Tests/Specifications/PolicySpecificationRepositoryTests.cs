using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Specifications;

namespace JD.AI.Tests.Specifications;

public sealed class PolicySpecificationRepositoryTests
{
    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void RepositoryPolicyArtifacts_ValidateSuccessfully()
    {
        var errors = PolicySpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void PolicySchema_ContainsRequiredContract()
    {
        var schemaPath = Path.Combine(GetRepoRoot(), "specs", "policies", "schema", "policies.schema.json");
        var schema = JsonNode.Parse(File.ReadAllText(schemaPath))!.AsObject();

        schema["title"]!.GetValue<string>().Should().Be("JD.AI Policy Specification");

        var required = schema["required"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        required.Should().Contain(["apiVersion", "kind", "id", "policyType", "severity", "scope", "rules", "exceptions", "enforcement", "trace"]);
    }

    [Fact]
    public void PolicyExample_ConformsToJsonSchemaShape()
    {
        var repoRoot = GetRepoRoot();
        var examplePath = Path.Combine(repoRoot, "specs", "policies", "examples", "policies.example.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "policies", "schema", "policies.schema.json");

        var spec = PolicySpecificationParser.ParseFile(examplePath);
        var json = JsonSerializer.Serialize(spec, CamelCaseJson);
        var schema = OutputSchemaValidator.LoadSchema(schemaPath);

        var errors = OutputSchemaValidator.Validate(json, schema);

        errors.Should().BeEmpty();
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "JD.AI.slnx")))
            current = current.Parent;

        Assert.NotNull(current);
        return current!.FullName;
    }
}
