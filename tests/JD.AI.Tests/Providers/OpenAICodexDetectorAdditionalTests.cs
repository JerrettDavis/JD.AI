using JD.AI.Core.Providers;
using JD.AI.Tests.Fixtures;
using JD.SemanticKernel.Connectors.OpenAICodex;

namespace JD.AI.Tests.Providers;

/// <summary>
/// Additional tests for OpenAICodexDetector — expands coverage of ReadModelsFromCache
/// and ApplyCredentialOverridesFromAuthFile edge cases.
/// </summary>
public sealed class OpenAICodexDetectorAdditionalTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    // ── ReadModelsFromCache ────────────────────────────────────────────────────

    [Fact]
    public void ReadModelsFromCache_NonExistentFile_ReturnsEmpty()
    {
        var result = OpenAICodexDetector.ReadModelsFromCache(
            Path.Combine(_fixture.DirectoryPath, "nonexistent-cache.json"));
        Assert.Empty(result);
    }

    [Fact]
    public void ReadModelsFromCache_EmptyPath_ReturnsEmpty()
    {
        var result = OpenAICodexDetector.ReadModelsFromCache(string.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadModelsFromCache_WhitespacePath_ReturnsEmpty()
    {
        var result = OpenAICodexDetector.ReadModelsFromCache("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void ReadModelsFromCache_NoModelsProperty_ReturnsEmpty()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """{"other": "data"}""");
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadModelsFromCache_ModelsIsNotArray_ReturnsEmpty()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """{"models": "not-an-array"}""");
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadModelsFromCache_EmptyModelsArray_ReturnsEmpty()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """{"models": []}""");
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadModelsFromCache_VisibilityHide_Excluded()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """
            {
              "models": [
                { "slug": "visible-model", "visibility": "list", "supported_in_api": true },
                { "slug": "hidden-model", "visibility": "hide", "supported_in_api": true }
              ]
            }
            """);
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Single(result);
        Assert.Equal("visible-model", result[0].Id);
    }

    [Fact]
    public void ReadModelsFromCache_SupportedInApiFalse_Excluded()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """
            {
              "models": [
                { "slug": "api-model", "visibility": "list", "supported_in_api": true },
                { "slug": "non-api-model", "visibility": "list", "supported_in_api": false }
              ]
            }
            """);
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Single(result);
        Assert.Equal("api-model", result[0].Id);
    }

    [Fact]
    public void ReadModelsFromCache_MissingSlug_EntrySkipped()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """
            {
              "models": [
                { "display_name": "no-slug-model", "visibility": "list", "supported_in_api": true },
                { "slug": "valid-model", "visibility": "list", "supported_in_api": true }
              ]
            }
            """);
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Single(result);
        Assert.Equal("valid-model", result[0].Id);
    }

    [Fact]
    public void ReadModelsFromCache_NoPriority_UsesMaxPriority()
    {
        // Models without priority still appear; sorted last by priority (int.MaxValue)
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """
            {
              "models": [
                { "slug": "low-priority", "visibility": "list", "supported_in_api": true },
                { "slug": "high-priority", "visibility": "list", "supported_in_api": true, "priority": 0 }
              ]
            }
            """);
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Equal(2, result.Count);
        // high-priority should come first
        Assert.Equal("high-priority", result[0].Id);
    }

    [Fact]
    public void ReadModelsFromCache_SetsProviderName()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """
            {
              "models": [
                { "slug": "some-model", "visibility": "list", "supported_in_api": true }
              ]
            }
            """);
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Single(result);
        Assert.Equal("OpenAI Codex", result[0].ProviderName);
    }

    [Fact]
    public void ReadModelsFromCache_UsesDisplayName_WhenAvailable()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """
            {
              "models": [
                { "slug": "model-id", "display_name": "My Display Name", "visibility": "list", "supported_in_api": true }
              ]
            }
            """);
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Equal("My Display Name", result[0].DisplayName);
    }

    [Fact]
    public void ReadModelsFromCache_FallsBackToSlug_WhenNoDisplayName()
    {
        var path = Path.Combine(_fixture.DirectoryPath, "cache.json");
        File.WriteAllText(path, """
            {
              "models": [
                { "slug": "model-slug", "visibility": "list", "supported_in_api": true }
              ]
            }
            """);
        var result = OpenAICodexDetector.ReadModelsFromCache(path);
        Assert.Equal("model-slug", result[0].DisplayName);
    }

    // ── ApplyCredentialOverridesFromAuthFile ──────────────────────────────────

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_NoOp_WhenFileDoesNotExist()
    {
        var options = new CodexSessionOptions
        {
            CredentialsPath = Path.Combine(_fixture.DirectoryPath, "nonexistent-auth.json"),
        };
        // Should not throw — simply no-op
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);
        Assert.Null(options.ApiKey);
        Assert.Null(options.AccessToken);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_UsesAccessToken_WhenIdTokenMissing()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
            {
              "tokens": {
                "access_token": "fallback-access-token"
              }
            }
            """);
        var options = new CodexSessionOptions { CredentialsPath = authPath };
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);
        Assert.Equal("fallback-access-token", options.AccessToken);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_NoOp_WhenNoCredentials()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """{"other": "data"}""");
        var options = new CodexSessionOptions { CredentialsPath = authPath };
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);
        Assert.Null(options.ApiKey);
        Assert.Null(options.AccessToken);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_ApiKeyTakesPrecedence_OverTokens()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
            {
              "OPENAI_API_KEY": "sk-key-wins",
              "tokens": {
                "id_token": "id-token-ignored"
              }
            }
            """);
        var options = new CodexSessionOptions { CredentialsPath = authPath };
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);
        Assert.Equal("sk-key-wins", options.ApiKey);
        Assert.Null(options.AccessToken);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_PrefersIdToken_OverAccessToken()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
            {
              "tokens": {
                "id_token": "id-token-preferred",
                "access_token": "access-token-not-used"
              }
            }
            """);
        var options = new CodexSessionOptions { CredentialsPath = authPath };
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);
        Assert.Equal("id-token-preferred", options.AccessToken);
        Assert.Null(options.ApiKey);
    }
}
