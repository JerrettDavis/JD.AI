using JD.AI.Tui.Tools;
using JD.SemanticKernel.Extensions.Memory;
using NSubstitute;

namespace JD.AI.Tui.Tests;

public sealed class MemoryToolsTests
{
    private readonly ISemanticMemory _memory = Substitute.For<ISemanticMemory>();

    [Fact]
    public async Task MemoryStore_StoresTextAndReturnsId()
    {
        _memory.StoreAsync("hello", Arg.Any<IDictionary<string, string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("mem-123");

        var tools = new MemoryTools(_memory);
        var result = await tools.MemoryStoreAsync("hello");

        Assert.Contains("mem-123", result);
    }

    [Fact]
    public async Task MemoryStore_PassesCategoryMetadata()
    {
        _memory.StoreAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("id");

        var tools = new MemoryTools(_memory);
        await tools.MemoryStoreAsync("text", category: "decision");

        await _memory.Received(1).StoreAsync(
            "text",
            Arg.Is<IDictionary<string, string>>(d => d.ContainsKey("category") && d["category"] == "decision"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MemorySearch_ReturnsFormattedResults()
    {
        var record = new MemoryRecord { Id = "id-1", Text = "important fact" };
        var results = new List<MemoryResult> { new() { Record = record, RelevanceScore = 0.95, AdjustedScore = 0.95 } };
        _memory.SearchAsync("query", Arg.Any<MemorySearchOptions?>(), Arg.Any<CancellationToken>())
            .Returns(results);

        var tools = new MemoryTools(_memory);
        var result = await tools.MemorySearchAsync("query");

        Assert.Contains("important fact", result);
        Assert.Contains("95", result); // score percentage
    }

    [Fact]
    public async Task MemorySearch_ReportsNoResults()
    {
        _memory.SearchAsync(Arg.Any<string>(), Arg.Any<MemorySearchOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryResult>());

        var tools = new MemoryTools(_memory);
        var result = await tools.MemorySearchAsync("query");

        Assert.Contains("No relevant", result);
    }

    [Fact]
    public async Task MemoryForget_DelegatesToMemory()
    {
        var tools = new MemoryTools(_memory);
        var result = await tools.MemoryForgetAsync("id-1");

        Assert.Contains("Removed", result);
        await _memory.Received(1).ForgetAsync("id-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AllMethods_HandleNullMemoryGracefully()
    {
        var tools = new MemoryTools(memory: null);

        var store = await tools.MemoryStoreAsync("text");
        var search = await tools.MemorySearchAsync("query");
        var forget = await tools.MemoryForgetAsync("id");

        Assert.Contains("not available", store);
        Assert.Contains("not available", search);
        Assert.Contains("not available", forget);
    }
}
