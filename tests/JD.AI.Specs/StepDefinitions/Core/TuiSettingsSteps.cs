using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Core.PromptCaching;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class TuiSettingsSteps
{
    private readonly ScenarioContext _context;
    public TuiSettingsSteps(ScenarioContext context) => _context = context;

    [Given(@"a temporary data directory for TUI settings")]
    public void GivenATemporaryDataDirectoryForTuiSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-tui-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _context.Set(tempDir, "tuiTempDir");

        // Override the DataDirectories root so TuiSettings reads/writes to our temp dir
        DataDirectories.SetRoot(tempDir);
    }

    [Given(@"TUI settings are saved with spinner style ""(.*)""")]
    public void GivenTuiSettingsAreSavedWithSpinnerStyle(string style)
    {
        var spinnerStyle = Enum.Parse<SpinnerStyle>(style);
        var settings = new TuiSettings { SpinnerStyle = spinnerStyle };
        settings.Save();
    }

    [Given(@"TUI settings are saved with system prompt budget (\d+)")]
    public void GivenTuiSettingsAreSavedWithSystemPromptBudget(int budget)
    {
        var settings = new TuiSettings { SystemPromptBudgetPercent = budget };
        settings.Save();
    }

    [Given(@"TUI settings are saved with prompt cache TTL ""(.*)""")]
    public void GivenTuiSettingsAreSavedWithPromptCacheTtl(string ttlStr)
    {
        var ttl = Enum.Parse<PromptCacheTtl>(ttlStr);
        var settings = new TuiSettings { PromptCacheTtl = ttl };
        settings.Save();
    }

    [When(@"TUI settings are loaded")]
    public void WhenTuiSettingsAreLoaded()
    {
        var settings = TuiSettings.Load();
        _context.Set(settings, "loadedTuiSettings");
    }

    [Then(@"the spinner style should be ""(.*)""")]
    public void ThenTheSpinnerStyleShouldBe(string expected)
    {
        var settings = _context.Get<TuiSettings>("loadedTuiSettings");
        settings.SpinnerStyle.Should().Be(Enum.Parse<SpinnerStyle>(expected));
    }

    [Then(@"the system prompt budget percent should be (\d+)")]
    public void ThenTheSystemPromptBudgetPercentShouldBe(int expected)
    {
        var settings = _context.Get<TuiSettings>("loadedTuiSettings");
        settings.SystemPromptBudgetPercent.Should().Be(expected);
    }

    [Then(@"the output style should be ""(.*)""")]
    public void ThenTheOutputStyleShouldBe(string expected)
    {
        var settings = _context.Get<TuiSettings>("loadedTuiSettings");
        settings.OutputStyle.Should().Be(Enum.Parse<OutputStyle>(expected));
    }

    [Then(@"prompt caching should be enabled")]
    public void ThenPromptCachingShouldBeEnabled()
    {
        var settings = _context.Get<TuiSettings>("loadedTuiSettings");
        settings.PromptCacheEnabled.Should().BeTrue();
    }

    [Then(@"vim mode should be disabled")]
    public void ThenVimModeShouldBeDisabled()
    {
        var settings = _context.Get<TuiSettings>("loadedTuiSettings");
        settings.VimMode.Should().BeFalse();
    }

    [Then(@"the prompt cache TTL should be ""(.*)""")]
    public void ThenThePromptCacheTtlShouldBe(string expected)
    {
        var settings = _context.Get<TuiSettings>("loadedTuiSettings");
        settings.PromptCacheTtl.Should().Be(Enum.Parse<PromptCacheTtl>(expected));
    }

    [AfterScenario("@tui")]
    public void Cleanup()
    {
        // Reset DataDirectories to avoid affecting other tests
        DataDirectories.Reset();

        if (_context.TryGetValue<string>("tuiTempDir", out var dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
        }
    }
}
