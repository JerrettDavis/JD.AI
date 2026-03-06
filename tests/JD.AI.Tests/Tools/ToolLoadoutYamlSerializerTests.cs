using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class ToolLoadoutYamlSerializerTests
{
    [Fact]
    public void RoundTrip_MinimalLoadout()
    {
        var original = new ToolLoadout("test");

        var yaml = ToolLoadoutYamlSerializer.Serialize(original);
        var restored = ToolLoadoutYamlSerializer.Deserialize(yaml);

        restored.Name.Should().Be("test");
        restored.ParentLoadoutName.Should().BeNull();
        restored.DefaultPlugins.Should().BeEmpty();
        restored.IncludedCategories.Should().BeEmpty();
        restored.DisabledPlugins.Should().BeEmpty();
        restored.DiscoverablePatterns.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_FullLoadout()
    {
        var original = new ToolLoadout("custom")
        {
            ParentLoadoutName = "developer",
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "myPlugin", "otherPlugin" },
            IncludedCategories = new HashSet<ToolCategory> { ToolCategory.Git, ToolCategory.Search },
            DisabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ssh" },
            DiscoverablePatterns = new List<string> { "docker*", "k8s*" }.AsReadOnly(),
        };

        var yaml = ToolLoadoutYamlSerializer.Serialize(original);
        var restored = ToolLoadoutYamlSerializer.Deserialize(yaml);

        restored.Name.Should().Be("custom");
        restored.ParentLoadoutName.Should().Be("developer");
        restored.DefaultPlugins.Should().HaveCount(2);
        restored.DefaultPlugins.Should().Contain("myPlugin");
        restored.DefaultPlugins.Should().Contain("otherPlugin");
        restored.IncludedCategories.Should().HaveCount(2);
        restored.IncludedCategories.Should().Contain(ToolCategory.Git);
        restored.IncludedCategories.Should().Contain(ToolCategory.Search);
        restored.DisabledPlugins.Should().ContainSingle().Which.Should().Be("ssh");
        restored.DiscoverablePatterns.Should().HaveCount(2);
    }

    [Fact]
    public void Serialize_OmitsNullCollections()
    {
        var loadout = new ToolLoadout("empty");
        var yaml = ToolLoadoutYamlSerializer.Serialize(loadout);

        yaml.Should().Contain("name: empty");
        yaml.Should().NotContain("includeCategories");
        yaml.Should().NotContain("includePlugins");
        yaml.Should().NotContain("excludePlugins");
        yaml.Should().NotContain("discoverablePatterns");
    }

    [Fact]
    public void Deserialize_HandlesUnknownCategory()
    {
        var yaml = """
            name: test
            includeCategories:
              - Git
              - NonExistentCategory
            """;

        var loadout = ToolLoadoutYamlSerializer.Deserialize(yaml);

        loadout.IncludedCategories.Should().ContainSingle()
            .Which.Should().Be(ToolCategory.Git);
    }

    [Fact]
    public void Deserialize_CaseInsensitiveCategory()
    {
        var yaml = """
            name: test
            includeCategories:
              - git
              - SEARCH
            """;

        var loadout = ToolLoadoutYamlSerializer.Deserialize(yaml);

        loadout.IncludedCategories.Should().HaveCount(2);
        loadout.IncludedCategories.Should().Contain(ToolCategory.Git);
        loadout.IncludedCategories.Should().Contain(ToolCategory.Search);
    }

    [Fact]
    public void Deserialize_IgnoresUnmatchedProperties()
    {
        var yaml = """
            name: test
            unknownField: value
            anotherUnknown: 42
            """;

        var loadout = ToolLoadoutYamlSerializer.Deserialize(yaml);
        loadout.Name.Should().Be("test");
    }

    [Fact]
    public void Serialize_NullLoadout_Throws() =>
        FluentActions.Invoking(() => ToolLoadoutYamlSerializer.Serialize(null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void Deserialize_NullYaml_Throws() =>
        FluentActions.Invoking(() => ToolLoadoutYamlSerializer.Deserialize(null!))
            .Should().Throw<ArgumentNullException>();
}
