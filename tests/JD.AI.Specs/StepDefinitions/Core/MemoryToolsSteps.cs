using FluentAssertions;
using JD.AI.Core.Tools;
using JD.SemanticKernel.Extensions.Memory;
using NSubstitute;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class MemoryToolsSteps
{
    private readonly ScenarioContext _context;

    public MemoryToolsSteps(ScenarioContext context) => _context = context;

    [Given(@"a configured semantic memory")]
    public void GivenAConfiguredSemanticMemory()
    {
        var memory = Substitute.For<ISemanticMemory>();
        memory.StoreAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult("mem-" + Guid.NewGuid().ToString("N")[..8]));
        memory.SearchAsync(Arg.Any<string>(), Arg.Any<MemorySearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult<IReadOnlyList<MemoryResult>>([]));
        _context.Set(memory, "Memory");
        _context.Set(new MemoryTools(memory), "MemoryTools");
    }

    [Given(@"no semantic memory configured")]
    public void GivenNoSemanticMemoryConfigured()
    {
        _context.Set(new MemoryTools(null), "MemoryTools");
    }

    [Given(@"I have stored ""(.*)"" in memory")]
    public void GivenIHaveStoredInMemory(string text)
    {
        var memory = _context.Get<ISemanticMemory>("Memory");
        memory.SearchAsync(Arg.Any<string>(), Arg.Any<MemorySearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult<IReadOnlyList<MemoryResult>>(
            [
                new MemoryResult { Record = new MemoryRecord { Text = text }, RelevanceScore = 0.9 }
            ]));
    }

    [Given(@"I have stored ""(.*)"" in memory with id ""(.*)""")]
    public void GivenIHaveStoredInMemoryWithId(string text, string id)
    {
        var memory = _context.Get<ISemanticMemory>("Memory");
        memory.StoreAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(id));
    }

    [When(@"I store ""(.*)"" with category ""(.*)""")]
    public async Task WhenIStoreWithCategory(string text, string category)
    {
        var tools = _context.Get<MemoryTools>("MemoryTools");
        var result = await tools.MemoryStoreAsync(text, category);
        _context.Set(result, "MemoryResult");
    }

    [When(@"I search memory for ""(.*)""")]
    public async Task WhenISearchMemoryFor(string query)
    {
        var tools = _context.Get<MemoryTools>("MemoryTools");
        var result = await tools.MemorySearchAsync(query);
        _context.Set(result, "MemoryResult");
    }

    [When(@"I forget memory ""(.*)""")]
    public async Task WhenIForgetMemory(string id)
    {
        var tools = _context.Get<MemoryTools>("MemoryTools");
        var result = await tools.MemoryForgetAsync(id);
        _context.Set(result, "MemoryResult");
    }

    [Then(@"the memory result should contain ""(.*)""")]
    public void ThenTheMemoryResultShouldContain(string expected)
    {
        var result = _context.Get<string>("MemoryResult");
        result.Should().Contain(expected);
    }

    [Then(@"the memory result should be ""(.*)""")]
    public void ThenTheMemoryResultShouldBe(string expected)
    {
        var result = _context.Get<string>("MemoryResult");
        result.Should().Be(expected);
    }
}
