using FluentAssertions;
using JD.AI.Core.Security;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ApiKeyAuthSteps
{
    private readonly ScenarioContext _context;

    public ApiKeyAuthSteps(ScenarioContext context) => _context = context;

    [Given(@"an API key auth provider")]
    public void GivenAnApiKeyAuthProvider()
    {
        _context.Set(new ApiKeyAuthProvider(), "AuthProvider");
    }

    [Given(@"a registered key ""(.*)"" for user ""(.*)"" with role ""(.*)""")]
    public void GivenARegisteredKey(string key, string name, string role)
    {
        var provider = _context.Get<ApiKeyAuthProvider>("AuthProvider");
        var gatewayRole = Enum.Parse<GatewayRole>(role);
        provider.RegisterKey(key, name, gatewayRole);
    }

    [When(@"I authenticate with key ""(.*)""")]
    public async Task WhenIAuthenticateWithKey(string key)
    {
        var provider = _context.Get<ApiKeyAuthProvider>("AuthProvider");
        var identity = await provider.AuthenticateAsync(key);
        _context.Set(identity, "Identity");
    }

    [Then(@"authentication should succeed")]
    public void ThenAuthenticationShouldSucceed()
    {
        var identity = _context.Get<GatewayIdentity?>("Identity");
        identity.Should().NotBeNull();
    }

    [Then(@"authentication should fail")]
    public void ThenAuthenticationShouldFail()
    {
        var identity = _context.Get<GatewayIdentity?>("Identity");
        identity.Should().BeNull();
    }

    [Then(@"the identity display name should be ""(.*)""")]
    public void ThenTheIdentityDisplayNameShouldBe(string expected)
    {
        var identity = _context.Get<GatewayIdentity?>("Identity");
        identity!.DisplayName.Should().Be(expected);
    }

    [Then(@"the identity role should be ""(.*)""")]
    public void ThenTheIdentityRoleShouldBe(string expected)
    {
        var identity = _context.Get<GatewayIdentity?>("Identity");
        identity!.Role.ToString().Should().Be(expected);
    }
}
