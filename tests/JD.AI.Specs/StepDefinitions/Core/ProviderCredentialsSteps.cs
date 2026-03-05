using FluentAssertions;
using JD.AI.Core.Providers.Credentials;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ProviderCredentialsSteps
{
    private readonly ScenarioContext _context;

    public ProviderCredentialsSteps(ScenarioContext context) => _context = context;

    [Given(@"a credential store backed by a temporary directory")]
    public void GivenACredentialStoreBackedByATemporaryDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-cred-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var store = new EncryptedFileStore(tempDir);
        _context.Set<ICredentialStore>(store);
        _context.Set(tempDir, "credTempDir");
    }

    [Given(@"a credential ""(.*)"" is stored with value ""(.*)""")]
    [When(@"a credential ""(.*)"" is stored with value ""(.*)""")]
    public async Task GivenACredentialIsStoredWithValue(string key, string value)
    {
        var store = _context.Get<ICredentialStore>();
        await store.SetAsync(key, value);
    }

    [When(@"the credential ""(.*)"" is retrieved")]
    public async Task WhenTheCredentialIsRetrieved(string key)
    {
        var store = _context.Get<ICredentialStore>();
        var value = await store.GetAsync(key);
        _context.Set(value, "retrievedCredential");
    }

    [When(@"the credential ""(.*)"" is removed")]
    public async Task WhenTheCredentialIsRemoved(string key)
    {
        var store = _context.Get<ICredentialStore>();
        await store.RemoveAsync(key);
    }

    [When(@"keys are listed with prefix ""(.*)""")]
    public async Task WhenKeysAreListedWithPrefix(string prefix)
    {
        var store = _context.Get<ICredentialStore>();
        var keys = await store.ListKeysAsync(prefix);
        _context.Set(keys, "keyList");
    }

    [Then(@"the retrieved credential should be ""(.*)""")]
    public void ThenTheRetrievedCredentialShouldBe(string expected)
    {
        var value = _context.Get<string?>("retrievedCredential");
        value.Should().Be(expected);
    }

    [Then(@"the retrieved credential should be null")]
    public void ThenTheRetrievedCredentialShouldBeNull()
    {
        _context.TryGetValue<string?>("retrievedCredential", out var value);
        value.Should().BeNull();
    }

    [Then(@"the key list should contain (\d+) entries")]
    public void ThenTheKeyListShouldContainEntries(int count)
    {
        var keys = _context.Get<IReadOnlyList<string>>("keyList");
        keys.Should().HaveCount(count);
    }

    [Then(@"the key list should include ""(.*)""")]
    public void ThenTheKeyListShouldInclude(string key)
    {
        var keys = _context.Get<IReadOnlyList<string>>("keyList");
        keys.Should().Contain(key);
    }

    [Then(@"the credential store should report as available")]
    public void ThenTheCredentialStoreShouldReportAsAvailable()
    {
        var store = _context.Get<ICredentialStore>();
        store.IsAvailable.Should().BeTrue();
    }

    [Then(@"the credential store should have a non-empty store name")]
    public void ThenTheCredentialStoreShouldHaveANonEmptyStoreName()
    {
        var store = _context.Get<ICredentialStore>();
        store.StoreName.Should().NotBeNullOrEmpty();
    }

    [AfterScenario("@credentials")]
    public void Cleanup()
    {
        if (_context.TryGetValue<string>("credTempDir", out var dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, true); } catch { /* best-effort cleanup */ }
        }
    }
}
