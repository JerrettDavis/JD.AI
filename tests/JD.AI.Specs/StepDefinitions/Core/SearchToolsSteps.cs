using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class SearchToolsSteps
{
    private readonly ScenarioContext _context;

    public SearchToolsSteps(ScenarioContext context) => _context = context;

    [Given(@"a temporary directory with search files")]
    public void GivenATemporaryDirectoryWithSearchFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jdai-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "sample.txt"), "hello world\nfoo bar\nhello again");
        File.WriteAllText(Path.Combine(dir, "code.cs"), "var x = 1;\nvar hello = true;");
        File.WriteAllText(Path.Combine(dir, "readme.md"), "# Documentation\nNo hello here actually hello");
        _context.Set(dir, "SearchDir");
    }

    [When(@"I grep for ""([^""]+)"" in the search directory")]
    public void WhenIGrepForInTheSearchDirectory(string pattern)
    {
        var dir = _context.Get<string>("SearchDir");
        var result = SearchTools.Grep(pattern, dir);
        _context.Set(result, "GrepResult");
    }

    [When(@"I grep for ""([^""]+)"" case-insensitively in the search directory")]
    public void WhenIGrepCaseInsensitively(string pattern)
    {
        var dir = _context.Get<string>("SearchDir");
        var result = SearchTools.Grep(pattern, dir, ignoreCase: true);
        _context.Set(result, "GrepResult");
    }

    [When(@"I grep for ""([^""]+)"" with glob ""([^""]+)"" in the search directory")]
    public void WhenIGrepWithGlob(string pattern, string glob)
    {
        var dir = _context.Get<string>("SearchDir");
        var result = SearchTools.Grep(pattern, dir, glob: glob);
        _context.Set(result, "GrepResult");
    }

    [When(@"I glob for ""(.*)"" in the search directory")]
    public void WhenIGlobForInTheSearchDirectory(string pattern)
    {
        var dir = _context.Get<string>("SearchDir");
        var result = SearchTools.Glob(pattern, dir);
        _context.Set(result, "GlobResult");
    }

    [Then(@"the grep result should contain ""(.*)""")]
    public void ThenTheGrepResultShouldContain(string expected)
    {
        var result = _context.Get<string>("GrepResult");
        result.Should().Contain(expected);
    }

    [Then(@"the grep result should be ""(.*)""")]
    public void ThenTheGrepResultShouldBe(string expected)
    {
        var result = _context.Get<string>("GrepResult");
        result.Should().Be(expected);
    }

    [Then(@"the glob result should contain ""(.*)""")]
    public void ThenTheGlobResultShouldContain(string expected)
    {
        var result = _context.Get<string>("GlobResult");
        result.Should().Contain(expected);
    }

    [Then(@"the glob result should be ""(.*)""")]
    public void ThenTheGlobResultShouldBe(string expected)
    {
        var result = _context.Get<string>("GlobResult");
        result.Should().Be(expected);
    }

    [AfterScenario]
    public void Cleanup()
    {
        if (_context.TryGetValue("SearchDir", out string? dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
