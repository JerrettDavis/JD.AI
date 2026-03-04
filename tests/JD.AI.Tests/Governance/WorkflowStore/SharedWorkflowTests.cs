using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JD.AI.Workflows.Store;

namespace JD.AI.Tests.Governance.WorkflowStore;

public class SharedWorkflowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var workflow = new SharedWorkflow();

        workflow.Id.Should().NotBeNullOrEmpty();
        workflow.Id.Should().HaveLength(16, "Id is a 16-char hex substring of a new Guid");
        workflow.Name.Should().BeEmpty();
        workflow.Version.Should().Be("1.0.0");
        workflow.Description.Should().BeEmpty();
        workflow.Author.Should().BeEmpty();
        workflow.Tags.Should().BeEmpty();
        workflow.RequiredTools.Should().BeEmpty();
        workflow.Visibility.Should().Be(WorkflowVisibility.Team);
        workflow.PublishedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        workflow.DefinitionJson.Should().BeEmpty();
    }

    [Fact]
    public void Id_IsUnique_PerInstance()
    {
        var a = new SharedWorkflow();
        var b = new SharedWorkflow();
        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void InitProperties_CanBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var workflow = new SharedWorkflow
        {
            Id = "abc123def456789a",
            Name = "my-workflow",
            Version = "2.3.1",
            Description = "A test workflow",
            Author = "alice",
            Tags = ["ci", "deploy"],
            RequiredTools = ["git", "docker"],
            Visibility = WorkflowVisibility.Organization,
            PublishedAt = now,
            DefinitionJson = "{\"name\":\"my-workflow\"}",
        };

        workflow.Id.Should().Be("abc123def456789a");
        workflow.Name.Should().Be("my-workflow");
        workflow.Version.Should().Be("2.3.1");
        workflow.Description.Should().Be("A test workflow");
        workflow.Author.Should().Be("alice");
        workflow.Tags.Should().BeEquivalentTo(["ci", "deploy"]);
        workflow.RequiredTools.Should().BeEquivalentTo(["git", "docker"]);
        workflow.Visibility.Should().Be(WorkflowVisibility.Organization);
        workflow.PublishedAt.Should().Be(now);
        workflow.DefinitionJson.Should().Be("{\"name\":\"my-workflow\"}");
    }

    [Fact]
    public void JsonSerializationRoundTrip_PreservesAllProperties()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var original = new SharedWorkflow
        {
            Id = "abc1234567890def",
            Name = "deploy-pipeline",
            Version = "1.2.3",
            Description = "Deploys to production",
            Author = "bob",
            Tags = ["deploy", "prod"],
            RequiredTools = ["kubectl"],
            Visibility = WorkflowVisibility.Public,
            PublishedAt = now,
            DefinitionJson = "{\"steps\":[]}",
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var restored = JsonSerializer.Deserialize<SharedWorkflow>(json, JsonOptions);

        restored.Should().NotBeNull();
        restored!.Id.Should().Be(original.Id);
        restored.Name.Should().Be(original.Name);
        restored.Version.Should().Be(original.Version);
        restored.Description.Should().Be(original.Description);
        restored.Author.Should().Be(original.Author);
        restored.Tags.Should().BeEquivalentTo(original.Tags);
        restored.RequiredTools.Should().BeEquivalentTo(original.RequiredTools);
        restored.Visibility.Should().Be(original.Visibility);
        restored.PublishedAt.Should().Be(original.PublishedAt);
        restored.DefinitionJson.Should().Be(original.DefinitionJson);
    }

    [Fact]
    public void JsonSerialization_UsesExpectedPropertyNames()
    {
        var workflow = new SharedWorkflow
        {
            Name = "test",
            Version = "1.0.0",
            Visibility = WorkflowVisibility.Private,
        };

        var json = JsonSerializer.Serialize(workflow, JsonOptions);

        // camelCase property names
        json.Should().Contain("\"name\":");
        json.Should().Contain("\"version\":");
        json.Should().Contain("\"visibility\":");
        json.Should().Contain("\"definitionJson\":");
        json.Should().Contain("\"publishedAt\":");
        json.Should().Contain("\"requiredTools\":");

        // Enum serialized as string
        json.Should().Contain("\"Private\"");
    }

    [Theory]
    [InlineData(WorkflowVisibility.Private)]
    [InlineData(WorkflowVisibility.Team)]
    [InlineData(WorkflowVisibility.Organization)]
    [InlineData(WorkflowVisibility.Public)]
    public void WorkflowVisibility_RoundTrips(WorkflowVisibility visibility)
    {
        var workflow = new SharedWorkflow { Visibility = visibility };
        var json = JsonSerializer.Serialize(workflow, JsonOptions);
        var restored = JsonSerializer.Deserialize<SharedWorkflow>(json, JsonOptions);

        restored!.Visibility.Should().Be(visibility);
    }
}
