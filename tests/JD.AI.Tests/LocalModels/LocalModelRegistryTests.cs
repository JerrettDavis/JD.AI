using FluentAssertions;
using JD.AI.Core.LocalModels;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.LocalModels;

public class LocalModelRegistryTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task LoadAsync_EmptyDir_CreatesEmptyRegistry()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        await registry.LoadAsync();

        registry.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);

        var ggufPath = CreateDummyGguf("test-model.gguf");

        registry.Add(new ModelMetadata
        {
            Id = "test",
            DisplayName = "Test Model",
            FilePath = ggufPath,
            FileSizeBytes = 1024,
        });

        await registry.SaveAsync();

        // Load in a new instance
        var registry2 = new LocalModelRegistry(_fixture.DirectoryPath);
        await registry2.LoadAsync();

        registry2.Models.Should().HaveCount(1);
        registry2.Models[0].Id.Should().Be("test");
        registry2.Models[0].DisplayName.Should().Be("Test Model");
    }

    [Fact]
    public async Task ScanDirectoryAsync_FindsGgufFiles()
    {
        CreateDummyGguf("model-a.gguf");
        CreateDummyGguf("model-b.gguf");
        CreateDummyGguf("not-a-model.txt");

        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        await registry.ScanDirectoryAsync(_fixture.DirectoryPath);

        registry.Models.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddFileAsync_RegistersSingleFile()
    {
        var path = CreateDummyGguf("single.gguf");

        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        await registry.AddFileAsync(path);

        registry.Models.Should().HaveCount(1);
        registry.Models[0].FilePath.Should().Be(path);
    }

    [Fact]
    public void Find_ByExactId()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        registry.Add(new ModelMetadata
        {
            Id = "llama-7b-q4",
            DisplayName = "Llama 7B Q4",
            FilePath = "/dummy.gguf",
        });

        registry.Find("llama-7b-q4").Should().NotBeNull();
    }

    [Fact]
    public void Find_BySubstring()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        registry.Add(new ModelMetadata
        {
            Id = "meta-llama-3-8b-instruct-q4_k_m",
            DisplayName = "Meta Llama 3 8B",
            FilePath = "/dummy.gguf",
        });

        registry.Find("llama-3").Should().NotBeNull();
    }

    [Fact]
    public void Find_NoMatch_ReturnsNull()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        registry.Find("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Remove_ById_Succeeds()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        registry.Add(new ModelMetadata
        {
            Id = "to-remove",
            DisplayName = "Remove Me",
            FilePath = "/dummy.gguf",
        });

        registry.Remove("to-remove").Should().BeTrue();
        registry.Models.Should().BeEmpty();
    }

    [Fact]
    public void Remove_NotFound_ReturnsFalse()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        registry.Remove("nope").Should().BeFalse();
    }

    [Fact]
    public void Add_Deduplicates_BySamePath()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        var model = new ModelMetadata
        {
            Id = "model1",
            DisplayName = "Model 1",
            FilePath = "/path/model.gguf",
        };

        registry.Add(model);
        registry.Add(model with { DisplayName = "Model 1 Updated" });

        registry.Models.Should().HaveCount(1);
        registry.Models[0].DisplayName.Should().Be("Model 1 Updated");
    }

    [Fact]
    public async Task Load_RemovesMissingFiles()
    {
        var registry = new LocalModelRegistry(_fixture.DirectoryPath);
        registry.Add(new ModelMetadata
        {
            Id = "ghost",
            DisplayName = "Ghost Model",
            FilePath = Path.Combine(_fixture.DirectoryPath, "ghost.gguf"),
        });
        await registry.SaveAsync();

        // Reload — file doesn't exist, should be pruned
        var registry2 = new LocalModelRegistry(_fixture.DirectoryPath);
        await registry2.LoadAsync();

        registry2.Models.Should().BeEmpty();
    }

    private string CreateDummyGguf(string filename)
    {
        var path = Path.Combine(_fixture.DirectoryPath, filename);
        File.WriteAllBytes(path, new byte[256]);
        return path;
    }
}
