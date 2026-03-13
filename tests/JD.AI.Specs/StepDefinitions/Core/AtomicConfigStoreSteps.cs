using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class AtomicConfigStoreSteps : IDisposable
{
    private readonly ScenarioContext _context;
    private AtomicConfigStore? _store;
    private string? _tempConfigPath;

    public AtomicConfigStoreSteps(ScenarioContext context) => _context = context;

    [Given(@"an atomic config store backed by a temporary file")]
    public void GivenAnAtomicConfigStoreBackedByATemporaryFile()
    {
        _tempConfigPath = Path.Combine(
            Path.GetTempPath(),
            $"jdai-config-test-{Guid.NewGuid():N}",
            "config.json");
        _store = new AtomicConfigStore(_tempConfigPath);
        _context.Set(_store);
    }

    [Given(@"the global default provider is set to ""(.*)""")]
    [When(@"the default provider is set to ""(.*)""")]
    public async Task WhenTheDefaultProviderIsSetTo(string provider)
    {
        var store = _context.Get<AtomicConfigStore>();
        await store.SetDefaultProviderAsync(provider);
    }

    [Given(@"the project ""(.*)"" default provider is set to ""(.*)""")]
    public async Task GivenTheProjectDefaultProviderIsSetTo(string projectPath, string provider)
    {
        var store = _context.Get<AtomicConfigStore>();
        await store.SetDefaultProviderAsync(provider, projectPath);
    }

    [When(@"the default model is set to ""(.*)""")]
    public async Task WhenTheDefaultModelIsSetTo(string model)
    {
        var store = _context.Get<AtomicConfigStore>();
        await store.SetDefaultModelAsync(model);
    }

    [When(@"the configuration is read")]
    public async Task WhenTheConfigurationIsRead()
    {
        var store = _context.Get<AtomicConfigStore>();
        var config = await store.ReadAsync();
        _context.Set(config, "readConfig");

        var provider = config.Defaults.Provider;
        var model = config.Defaults.Model;
        _context.Set(provider, "readDefaultProvider");
        _context.Set(model, "readDefaultModel");
    }

    [When(@"the default provider is read for project ""(.*)""")]
    public async Task WhenTheDefaultProviderIsReadForProject(string projectPath)
    {
        var store = _context.Get<AtomicConfigStore>();
        var provider = await store.GetDefaultProviderAsync(projectPath);
        _context.Set(provider, "readDefaultProvider");
    }

    [When(@"(\d+) concurrent writes set different default models")]
    public async Task WhenConcurrentWritesSetDifferentDefaultModels(int count)
    {
        var store = _context.Get<AtomicConfigStore>();
        var models = Enumerable.Range(0, count).Select(i => $"model-{i}").ToList();
        _context.Set(models, "writtenModels");

        var tasks = models.Select(model => store.SetDefaultModelAsync(model));
        await Task.WhenAll(tasks);
    }

    [Then(@"the default provider should be null")]
    public void ThenTheDefaultProviderShouldBeNull()
    {
        _context.TryGetValue<string?>("readDefaultProvider", out var provider);
        provider.Should().BeNull();
    }

    [Then(@"the default model should be null")]
    public void ThenTheDefaultModelShouldBeNull()
    {
        _context.TryGetValue<string?>("readDefaultModel", out var model);
        model.Should().BeNull();
    }

    [Then(@"the default provider should be ""(.*)""")]
    public void ThenTheDefaultProviderShouldBe(string expected)
    {
        _context.TryGetValue<string?>("readDefaultProvider", out var provider);
        provider.Should().Be(expected);
    }

    [Then(@"the default model should be ""(.*)""")]
    public void ThenTheDefaultModelShouldBe(string expected)
    {
        _context.TryGetValue<string?>("readDefaultModel", out var model);
        model.Should().Be(expected);
    }

    [Then(@"the default model should be one of the written values")]
    public void ThenTheDefaultModelShouldBeOneOfTheWrittenValues()
    {
        _context.TryGetValue<string?>("readDefaultModel", out var model);
        var written = _context.Get<List<string>>("writtenModels");
        model.Should().NotBeNull();
        written.Should().Contain(model!);
    }

    [Then(@"the config object should not be null")]
    public void ThenTheConfigObjectShouldNotBeNull()
    {
        var config = _context.Get<JdAiConfig>("readConfig");
        config.Should().NotBeNull();
    }

    [Then(@"the config defaults should not be null")]
    public void ThenTheConfigDefaultsShouldNotBeNull()
    {
        var config = _context.Get<JdAiConfig>("readConfig");
        config.Defaults.Should().NotBeNull();
    }

    [When(@"tool pattern ""(.*)"" is allowed globally")]
    public async Task WhenToolPatternIsAllowedGlobally(string toolPattern)
    {
        var store = _context.Get<AtomicConfigStore>();
        await store.AddToolPermissionRuleAsync(toolPattern, allow: true, projectScope: false);
    }

    [When(@"tool pattern ""(.*)"" is denied for project ""(.*)""")]
    public async Task WhenToolPatternIsDeniedForProject(string toolPattern, string projectPath)
    {
        var store = _context.Get<AtomicConfigStore>();
        await store.AddToolPermissionRuleAsync(toolPattern, allow: false, projectScope: true, projectPath: projectPath);
    }

    [When(@"tool permissions are read for project ""(.*)""")]
    public async Task WhenToolPermissionsAreReadForProject(string projectPath)
    {
        var store = _context.Get<AtomicConfigStore>();
        var profile = await store.GetToolPermissionProfileAsync(projectPath);
        _context.Set(profile, "toolPermissionProfile");
    }

    [Then(@"global allowed tools should contain ""(.*)""")]
    public void ThenGlobalAllowedToolsShouldContain(string toolPattern)
    {
        var profile = _context.Get<ToolPermissionProfile>("toolPermissionProfile");
        profile.GlobalAllowed.Should().Contain(toolPattern);
    }

    [Then(@"project denied tools should contain ""(.*)""")]
    public void ThenProjectDeniedToolsShouldContain(string toolPattern)
    {
        var profile = _context.Get<ToolPermissionProfile>("toolPermissionProfile");
        profile.ProjectDenied.Should().Contain(toolPattern);
    }

    public void Dispose()
    {
        _store?.Dispose();
        if (_tempConfigPath != null)
        {
            var dir = Path.GetDirectoryName(_tempConfigPath);
            if (dir != null && Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { /* best-effort */ }
            }
        }
    }
}
