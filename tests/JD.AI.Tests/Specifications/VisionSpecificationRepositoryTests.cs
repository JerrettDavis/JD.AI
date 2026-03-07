using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Specifications;

namespace JD.AI.Tests.Specifications;

public sealed class VisionSpecificationRepositoryTests
{
    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void RepositoryVisionArtifacts_ValidateSuccessfully()
    {
        var errors = VisionSpecificationValidator.ValidateRepository(GetRepoRoot());

        errors.Should().BeEmpty("checked-in vision specs should be valid and traceable");
    }

    [Fact]
    public void VisionSchema_ContainsRequiredContract()
    {
        var schemaPath = Path.Combine(GetRepoRoot(), "specs", "vision", "schema", "vision.schema.json");
        var schemaText = File.ReadAllText(schemaPath);
        var schema = JsonNode.Parse(schemaText)!.AsObject();

        schema["$schema"]!.GetValue<string>().Should().Contain("json-schema.org");
        schema["title"]!.GetValue<string>().Should().Be("JD.AI Vision Specification");
        schema["type"]!.GetValue<string>().Should().Be("object");

        var required = schema["required"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        required.Should().Contain(["apiVersion", "kind", "id", "version", "status", "metadata"]);

        var properties = schema["properties"]!.AsObject();
        properties.ContainsKey("problemStatement").Should().BeTrue();
        properties.ContainsKey("mission").Should().BeTrue();
        properties.ContainsKey("targetUsers").Should().BeTrue();
        properties.ContainsKey("successMetrics").Should().BeTrue();
        properties.ContainsKey("trace").Should().BeTrue();
    }

    [Fact]
    public void VisionExample_ConformsToJsonSchemaShape()
    {
        var repoRoot = GetRepoRoot();
        var examplePath = Path.Combine(repoRoot, "specs", "vision", "examples", "vision.example.yaml");
        var schemaPath = Path.Combine(repoRoot, "specs", "vision", "schema", "vision.schema.json");

        var spec = VisionSpecificationParser.ParseFile(examplePath);
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
