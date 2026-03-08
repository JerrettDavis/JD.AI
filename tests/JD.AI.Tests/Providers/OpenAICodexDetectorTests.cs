using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Tests.Fixtures;
using JD.SemanticKernel.Connectors.OpenAICodex;

namespace JD.AI.Tests.Providers;

public sealed class OpenAICodexDetectorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly string _cachePath;

    public OpenAICodexDetectorTests()
    {
        _cachePath = Path.Combine(_fixture.DirectoryPath, "models_cache.json");
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void ReadModelsFromCache_FiltersAndSortsVisibleApiModels()
    {
        var json = """
                   {
                     "models": [
                       { "slug": "gpt-5.2", "display_name": "gpt-5.2", "visibility": "list", "supported_in_api": true, "priority": 6 },
                       { "slug": "hidden-model", "display_name": "hidden-model", "visibility": "hide", "supported_in_api": true, "priority": 1 },
                       { "slug": "gpt-5.3-codex", "display_name": "gpt-5.3-codex", "visibility": "list", "supported_in_api": true, "priority": 0 },
                       { "slug": "not-in-api", "display_name": "not-in-api", "visibility": "list", "supported_in_api": false, "priority": 2 },
                       { "slug": "unknown-api-state", "display_name": "unknown-api-state", "visibility": "list", "supported_in_api": null, "priority": 2 },
                       { "slug": "duplicate-model", "display_name": "B-model", "visibility": "list", "supported_in_api": true, "priority": 5 },
                       { "slug": "duplicate-model", "display_name": "A-model", "visibility": "list", "supported_in_api": true, "priority": 3 }
                     ]
                   }
                   """;
        File.WriteAllText(_cachePath, json);

        var models = OpenAICodexDetector.ReadModelsFromCache(_cachePath);

        models.Select(m => m.Id).Should().Equal(
            "gpt-5.3-codex",
            "duplicate-model",
            "gpt-5.2");
        models.Should().OnlyContain(m =>
            m.ProviderName.Equals("OpenAI Codex", StringComparison.Ordinal));
        models[1].DisplayName.Should().Be("A-model");
    }

    [Fact]
    public void ReadModelsFromCache_ReturnsEmptyForInvalidJson()
    {
        File.WriteAllText(_cachePath, "{not-json");

        var models = OpenAICodexDetector.ReadModelsFromCache(_cachePath);

        models.Should().BeEmpty();
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_UsesApiKeyWhenPresent()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
                                    {
                                      "OPENAI_API_KEY": "sk-codex-from-file",
                                      "tokens": {
                                        "id_token": "id-token-ignored"
                                      }
                                    }
                                    """);

        var options = new CodexSessionOptions { CredentialsPath = authPath };

        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);

        options.ApiKey.Should().Be("sk-codex-from-file");
        options.AccessToken.Should().BeNull();
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_DoesNotForceTokenWhenApiKeyMissing()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
                                    {
                                      "tokens": {
                                        "access_token": "access-token",
                                        "id_token": "id-token"
                                      }
                                    }
                                    """);

        var options = new CodexSessionOptions { CredentialsPath = authPath };

        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);

        options.ApiKey.Should().BeNull();
        options.AccessToken.Should().BeNull();
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_IgnoresMalformedJson()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, "{not-json");

        var options = new CodexSessionOptions { CredentialsPath = authPath };

        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);

        options.ApiKey.Should().BeNull();
        options.AccessToken.Should().BeNull();
    }
}
