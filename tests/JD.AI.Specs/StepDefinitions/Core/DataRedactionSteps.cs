using FluentAssertions;
using JD.AI.Core.Governance;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class DataRedactionSteps
{
    private readonly ScenarioContext _context;

    public DataRedactionSteps(ScenarioContext context) => _context = context;

    [Given(@"a data redactor with pattern ""(.*)""")]
    public void GivenADataRedactorWithPattern(string pattern)
    {
        _context.Set(new DataRedactor([pattern]), "Redactor");
    }

    [Given(@"a data redactor with patterns:")]
    public void GivenADataRedactorWithPatterns(Table table)
    {
        var patterns = table.Rows.Select(r => r["pattern"]).ToList();
        _context.Set(new DataRedactor(patterns), "Redactor");
    }

    [Given(@"a data redactor with no patterns")]
    public void GivenADataRedactorWithNoPatterns()
    {
        _context.Set(DataRedactor.None, "Redactor");
    }

    [When(@"I redact ""(.*)""")]
    public void WhenIRedact(string input)
    {
        var redactor = _context.Get<DataRedactor>("Redactor");
        var result = redactor.Redact(input);
        _context.Set(result, "RedactedText");
    }

    [When(@"I check ""(.*)"" for sensitive content")]
    public void WhenICheckForSensitiveContent(string input)
    {
        var redactor = _context.Get<DataRedactor>("Redactor");
        var hasSensitive = redactor.HasSensitiveContent(input);
        _context.Set(hasSensitive, "HasSensitive");
    }

    [Then(@"the redacted text should be ""(.*)""")]
    public void ThenTheRedactedTextShouldBe(string expected)
    {
        var result = _context.Get<string>("RedactedText");
        result.Should().Be(expected);
    }

    [Then(@"sensitive content should be detected")]
    public void ThenSensitiveContentShouldBeDetected()
    {
        var result = _context.Get<bool>("HasSensitive");
        result.Should().BeTrue();
    }

    [Then(@"sensitive content should not be detected")]
    public void ThenSensitiveContentShouldNotBeDetected()
    {
        var result = _context.Get<bool>("HasSensitive");
        result.Should().BeFalse();
    }
}
