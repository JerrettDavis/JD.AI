using FluentAssertions;
using JD.AI.Core.LocalModels.Sources;

namespace JD.AI.Tests.LocalModels;

public sealed class HuggingFaceModelSourceTests
{
    // ── GetDownloadUrl ────────────────────────────────────────────────────

    [Fact]
    public void GetDownloadUrl_ReturnsValidUri()
    {
        var uri = HuggingFaceModelSource.GetDownloadUrl("TheBloke/Llama-2-7B-GGUF", "llama-2-7b.Q4_K_M.gguf");

        uri.Should().NotBeNull();
        uri.IsAbsoluteUri.Should().BeTrue();
    }

    [Fact]
    public void GetDownloadUrl_ContainsRepoId()
    {
        var uri = HuggingFaceModelSource.GetDownloadUrl("TheBloke/Llama-2-7B-GGUF", "model.gguf");

        uri.ToString().Should().Contain("TheBloke/Llama-2-7B-GGUF");
    }

    [Fact]
    public void GetDownloadUrl_ContainsFilename()
    {
        var uri = HuggingFaceModelSource.GetDownloadUrl("owner/repo", "my-model.gguf");

        uri.ToString().Should().Contain("my-model.gguf");
    }

    [Fact]
    public void GetDownloadUrl_PointsToResolveMain()
    {
        var uri = HuggingFaceModelSource.GetDownloadUrl("owner/repo", "file.gguf");

        uri.ToString().Should().Contain("/resolve/main/");
    }

    [Fact]
    public void GetDownloadUrl_UsesHttps()
    {
        var uri = HuggingFaceModelSource.GetDownloadUrl("owner/repo", "file.gguf");

        uri.Scheme.Should().Be("https");
    }

    [Fact]
    public void GetDownloadUrl_HostIsHuggingFace()
    {
        var uri = HuggingFaceModelSource.GetDownloadUrl("owner/repo", "file.gguf");

        uri.Host.Should().Be("huggingface.co");
    }

    // ── HuggingFaceSearchResult record ────────────────────────────────────

    [Fact]
    public void SearchResult_DefaultValues()
    {
        var result = new HuggingFaceSearchResult();

        result.Id.Should().BeNull();
        result.ModelId.Should().BeNull();
        result.Author.Should().BeNull();
        result.Downloads.Should().Be(0);
        result.Tags.Should().BeNull();
    }

    [Fact]
    public void SearchResult_AllPropertiesSettable()
    {
        var result = new HuggingFaceSearchResult
        {
            Id = "TheBloke/Llama-2-7B-GGUF",
            ModelId = "TheBloke/Llama-2-7B-GGUF",
            Author = "TheBloke",
            Downloads = 50000,
            Tags = ["gguf", "llama"],
        };

        result.Id.Should().Be("TheBloke/Llama-2-7B-GGUF");
        result.Author.Should().Be("TheBloke");
        result.Downloads.Should().Be(50000);
        result.Tags.Should().HaveCount(2);
    }

    [Fact]
    public void SearchResult_RecordEquality()
    {
        var a = new HuggingFaceSearchResult { Id = "x", Downloads = 100 };
        var b = new HuggingFaceSearchResult { Id = "x", Downloads = 100 };
        a.Should().Be(b);
    }

    // ── ScanAsync (empty/missing dir) ─────────────────────────────────────

    [Fact]
    public async Task ScanAsync_NonexistentCacheDir_ReturnsEmpty()
    {
        var source = new HuggingFaceModelSource("/nonexistent/cache/path");

        // Set HF_HOME to a nonexistent directory so scan finds nothing
        var original = Environment.GetEnvironmentVariable("HF_HOME");
        try
        {
            Environment.SetEnvironmentVariable("HF_HOME", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var models = await source.ScanAsync();
            models.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HF_HOME", original);
        }
    }
}
