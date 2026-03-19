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
    private static readonly Lock EnvLock = new();
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
    public void ApplyCredentialOverridesFromAuthFile_DoesNotForceAccessToken_WhenIdTokenMissing()
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
        Assert.Null(options.AccessToken);
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
    public void ApplyCredentialOverridesFromAuthFile_PrefersJwtToken_WhenAuthModeMissing()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
            {
              "OPENAI_API_KEY": "sk-stale-file-key",
              "tokens": {
                "id_token": "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1c2VyIn0.signature"
              }
            }
            """);

        var options = new CodexSessionOptions { CredentialsPath = authPath };
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);

        Assert.Equal("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1c2VyIn0.signature", options.AccessToken);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_ChatGptModePrefersTokens_OverApiKey()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
            {
              "auth_mode": "chatgpt",
              "OPENAI_API_KEY": "sk-billing-limited",
              "tokens": {
                "id_token": "id-token-from-chatgpt-mode"
              }
            }
            """);

        var options = new CodexSessionOptions { CredentialsPath = authPath };
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);

        Assert.Equal("id-token-from-chatgpt-mode", options.AccessToken);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_ChatGptModeWithoutApiKey_DoesNotForceToken()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
            {
              "auth_mode": "chatgpt",
              "tokens": {
                "id_token": "id-token-from-chatgpt-mode"
              }
            }
            """);

        var options = new CodexSessionOptions { CredentialsPath = authPath };
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);

        Assert.Null(options.AccessToken);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_DoesNotForceToken_WhenEnvApiKeyNotPresent()
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
        Assert.Null(options.AccessToken);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_UsesToken_WhenEnvApiKeyWouldShadowAuthFile()
    {
        var authPath = Path.Combine(_fixture.DirectoryPath, "auth.json");
        File.WriteAllText(authPath, """
            {
              "tokens": {
                "id_token": "id-token-from-file"
              }
            }
            """);
        var options = new CodexSessionOptions { CredentialsPath = authPath };
        OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options, envApiKeyOverride: "stale-env-key");

        Assert.Equal("id-token-from-file", options.AccessToken);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void ComputeCodexKeyringAccountKey_HasExpectedShape()
    {
        var key = OpenAICodexDetector.ComputeCodexKeyringAccountKey(@"C:\Users\alice\.codex");

        Assert.StartsWith("cli|", key, StringComparison.Ordinal);
        Assert.Equal(20, key.Length);
    }

    [Fact]
    public void ComputeCodexKeyringAccountKey_IsDeterministic()
    {
        var a = OpenAICodexDetector.ComputeCodexKeyringAccountKey(@"/home/alice/.codex");
        var b = OpenAICodexDetector.ComputeCodexKeyringAccountKey(@"/home/alice/.codex");
        var c = OpenAICodexDetector.ComputeCodexKeyringAccountKey(@"/home/bob/.codex");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_KeyringMode_DoesNotUseAuthJsonFallback()
    {
        var codexHome = Path.Combine(_fixture.DirectoryPath, "codex-home");
        Directory.CreateDirectory(codexHome);
        File.WriteAllText(Path.Combine(codexHome, "config.toml"),
            "cli_auth_credentials_store = \"keyring\"");
        File.WriteAllText(Path.Combine(codexHome, "auth.json"),
            "{\"OPENAI_API_KEY\":\"sk-from-file\"}");

        lock (EnvLock)
        {
            var previous = Environment.GetEnvironmentVariable("CODEX_HOME");
            try
            {
                Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
                var options = new CodexSessionOptions();

                OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);

                Assert.Null(options.ApiKey);
                Assert.Null(options.AccessToken);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CODEX_HOME", previous);
            }
        }
    }

    [Fact]
    public void ApplyCredentialOverridesFromAuthFile_AutoMode_FallsBackToAuthJsonWhenKeyringMissing()
    {
        var codexHome = Path.Combine(_fixture.DirectoryPath, "codex-home-auto");
        Directory.CreateDirectory(codexHome);
        File.WriteAllText(Path.Combine(codexHome, "config.toml"),
            "cli_auth_credentials_store = \"auto\"");
        File.WriteAllText(Path.Combine(codexHome, "auth.json"),
            "{\"OPENAI_API_KEY\":\"sk-from-file\"}");

        lock (EnvLock)
        {
            var previous = Environment.GetEnvironmentVariable("CODEX_HOME");
            try
            {
                Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
                var options = new CodexSessionOptions();

                OpenAICodexDetector.ApplyCredentialOverridesFromAuthFile(options);

                Assert.Equal("sk-from-file", options.ApiKey);
                Assert.Null(options.AccessToken);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CODEX_HOME", previous);
            }
        }
    }
}
