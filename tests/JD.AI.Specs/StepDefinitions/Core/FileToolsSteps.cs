using FluentAssertions;
using JD.AI.Core.Tools;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class FileToolsSteps
{
    private readonly ScenarioContext _context;

    public FileToolsSteps(ScenarioContext context) => _context = context;

    [Given(@"a temporary directory")]
    public void GivenATemporaryDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jdai-specs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _context.Set(dir, "TempDir");
    }

    [Given(@"a file ""(.*)"" with content ""(.*)""")]
    public void GivenAFileWithContent(string fileName, string content)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var fileDir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(fileDir);
        File.WriteAllText(filePath, content);
    }

    [Given(@"a file ""(.*)"" with content:")]
    public void GivenAFileWithMultilineContent(string fileName, string content)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var fileDir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(fileDir);
        File.WriteAllText(filePath, content);
    }

    [Given(@"a subdirectory ""(.*)""")]
    public void GivenASubdirectory(string subDir)
    {
        var dir = _context.Get<string>("TempDir");
        Directory.CreateDirectory(Path.Combine(dir, subDir));
    }

    [When(@"I read file ""(.*)""")]
    public void WhenIReadFile(string fileName)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var result = FileTools.ReadFile(filePath);
        _context.Set(result, "Result");
    }

    [When(@"I read file ""(.*)"" from line (\d+) to line (\d+)")]
    public void WhenIReadFileWithLineRange(string fileName, int startLine, int endLine)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var result = FileTools.ReadFile(filePath, startLine, endLine);
        _context.Set(result, "Result");
    }

    [When(@"I write ""(.*)"" to file ""(.*)""")]
    public void WhenIWriteToFile(string content, string fileName)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var result = FileTools.WriteFile(filePath, content);
        _context.Set(result, "Result");
    }

    [When(@"I edit file ""(.*)"" replacing ""(.*)"" with ""(.*)""")]
    public void WhenIEditFile(string fileName, string oldStr, string newStr)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        var result = FileTools.EditFile(filePath, oldStr, newStr);
        _context.Set(result, "Result");
    }

    [When(@"I list the directory")]
    public void WhenIListTheDirectory()
    {
        var dir = _context.Get<string>("TempDir");
        var result = FileTools.ListDirectory(dir);
        _context.Set(result, "Result");
    }

    [Then(@"the result should contain ""(.*)""")]
    public void ThenTheResultShouldContain(string expected)
    {
        var result = _context.Get<string>("Result");
        result.Should().Contain(expected);
    }

    [Then(@"the result should not contain ""(.*)""")]
    public void ThenTheResultShouldNotContain(string expected)
    {
        var result = _context.Get<string>("Result");
        result.Should().NotContain(expected);
    }

    [Then(@"the file ""(.*)"" should exist with content ""(.*)""")]
    public void ThenTheFileShouldExistWithContent(string fileName, string expectedContent)
    {
        var dir = _context.Get<string>("TempDir");
        var filePath = Path.Combine(dir, fileName);
        File.Exists(filePath).Should().BeTrue($"file {fileName} should exist");
        var actual = File.ReadAllText(filePath);
        actual.Should().Be(expectedContent);
    }

    [AfterScenario]
    public void Cleanup()
    {
        if (_context.TryGetValue("TempDir", out string? dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
