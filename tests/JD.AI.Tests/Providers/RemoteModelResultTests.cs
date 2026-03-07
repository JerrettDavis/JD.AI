using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Providers.ModelSearch;

namespace JD.AI.Tests.Providers;

public sealed class RemoteModelResultTests
{
    [Fact]
    public void Construction_AllProperties()
    {
        var result = new RemoteModelResult(
            "llama3.2:7b", "Llama 3.2 7B", "Ollama",
            Size: "4.1 GB", Status: "available",
            Description: "Meta's latest model");
        result.Id.Should().Be("llama3.2:7b");
        result.DisplayName.Should().Be("Llama 3.2 7B");
        result.ProviderName.Should().Be("Ollama");
        result.Size.Should().Be("4.1 GB");
        result.Status.Should().Be("available");
        result.Description.Should().Be("Meta's latest model");
    }

    [Fact]
    public void DefaultCapabilities_ChatAndToolCalling()
    {
        var result = new RemoteModelResult("m", "M", "P", null, "ok", null);
        result.Capabilities.Should().HaveFlag(ModelCapabilities.Chat);
        result.Capabilities.Should().HaveFlag(ModelCapabilities.ToolCalling);
    }

    [Fact]
    public void CustomCapabilities()
    {
        var result = new RemoteModelResult("m", "M", "P", null, "ok", null,
            Capabilities: ModelCapabilities.Chat | ModelCapabilities.Vision);
        result.Capabilities.Should().HaveFlag(ModelCapabilities.Vision);
        result.Capabilities.Should().NotHaveFlag(ModelCapabilities.Embeddings);
    }

    [Fact]
    public void RecordEquality()
    {
        var a = new RemoteModelResult("m", "M", "P", null, "ok", null);
        var b = new RemoteModelResult("m", "M", "P", null, "ok", null);
        a.Should().Be(b);
    }
}
