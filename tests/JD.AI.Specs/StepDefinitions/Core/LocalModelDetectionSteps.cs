using FluentAssertions;
using JD.AI.Core.LocalModels;
using JD.AI.Core.Providers;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class LocalModelDetectionSteps
{
    private readonly ScenarioContext _context;

    public LocalModelDetectionSteps(ScenarioContext context) => _context = context;

    [Given(@"a local model detector with an empty models directory")]
    public void GivenALocalModelDetectorWithEmptyDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jdai-models-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var registry = new LocalModelRegistry(dir);
        var detector = new LocalModelDetector(registry);
        _context.Set(detector, "Detector");
        _context.Set(dir, "ModelsDir");
    }

    [Given(@"a local model detector")]
    public void GivenALocalModelDetector()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jdai-models-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var registry = new LocalModelRegistry(dir);
        var detector = new LocalModelDetector(registry);
        _context.Set(detector, "Detector");
        _context.Set(dir, "ModelsDir");
    }

    [Given(@"a local model detector with an inaccessible directory")]
    public void GivenALocalModelDetectorWithInaccessibleDir()
    {
        // Create a temporary directory so the registry constructor succeeds,
        // then immediately delete it so that detection fails consistently
        // across platforms (Windows and Linux both report "not found" style
        // errors rather than "access denied" for truly inaccessible paths).
        var dir = Path.Combine(Path.GetTempPath(), "jdai-inaccessible-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var registry = new LocalModelRegistry(dir);

        // Remove the directory so scanning/loading fails gracefully
        try { Directory.Delete(dir, recursive: true); }
        catch { /* best effort */ }

        var detector = new LocalModelDetector(registry);
        _context.Set(detector, "Detector");
    }

    [When(@"I detect local models")]
    public async Task WhenIDetectLocalModels()
    {
        var detector = _context.Get<LocalModelDetector>("Detector");
        var result = await detector.DetectAsync();
        _context.Set(result, "DetectionResult");
    }

    [Then(@"the provider should not be available")]
    public void ThenTheProviderShouldNotBeAvailable()
    {
        var result = _context.Get<ProviderInfo>("DetectionResult");
        result.IsAvailable.Should().BeFalse();
    }

    [Then(@"the status message should contain ""([^""]+)""")]
    public void ThenTheStatusMessageShouldContain(string expected)
    {
        var result = _context.Get<ProviderInfo>("DetectionResult");
        result.StatusMessage.Should().Contain(expected);
    }

    [Then(@"the status message should contain ""([^""]+)"" or ""([^""]+)""")]
    public void ThenTheStatusMessageShouldContainEither(string expected1, string expected2)
    {
        var result = _context.Get<ProviderInfo>("DetectionResult");
        var msg = result.StatusMessage ?? string.Empty;
        (msg.Contains(expected1, StringComparison.OrdinalIgnoreCase) ||
         msg.Contains(expected2, StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue($"status should contain '{expected1}' or '{expected2}' but was '{msg}'");
    }

    [Then(@"the provider name should be ""(.*)""")]
    public void ThenTheProviderNameShouldBe(string expected)
    {
        var detector = _context.Get<LocalModelDetector>("Detector");
        detector.ProviderName.Should().Be(expected);
    }

    [AfterScenario]
    public void Cleanup()
    {
        if (_context.TryGetValue("ModelsDir", out string? dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
