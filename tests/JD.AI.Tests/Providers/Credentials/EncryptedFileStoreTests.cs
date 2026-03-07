using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Providers.Credentials;

public sealed class EncryptedFileStoreTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;

    public EncryptedFileStoreTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        var result = await _store.GetAsync("nonexistent-key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsValue()
    {
        await _store.SetAsync("test-key", "test-value");

        var result = await _store.GetAsync("test-key");

        result.Should().Be("test-value");
    }

    [Fact]
    public async Task RemoveAsync_RemovesKey()
    {
        await _store.SetAsync("remove-me", "value");

        await _store.RemoveAsync("remove-me");

        var result = await _store.GetAsync("remove-me");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListKeysAsync_ReturnsMatchingKeys()
    {
        await _store.SetAsync("prefix:alpha", "a");
        await _store.SetAsync("prefix:beta", "b");
        await _store.SetAsync("other:gamma", "c");

        var keys = await _store.ListKeysAsync("prefix:");

        keys.Should().HaveCount(2);
        keys.Should().Contain(k => k.Equals("prefix:alpha", StringComparison.OrdinalIgnoreCase));
        keys.Should().Contain(k => k.Equals("prefix:beta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetAsync_Overwrite_ReturnsNewValue()
    {
        await _store.SetAsync("overwrite-key", "old-value");

        await _store.SetAsync("overwrite-key", "new-value");

        var result = await _store.GetAsync("overwrite-key");
        result.Should().Be("new-value");
    }

    [Fact]
    public async Task RotateKeyAsync_PreservesExistingSecrets()
    {
        await _store.SetAsync("key-one", "value-one");

        await _store.RotateKeyAsync();
        await _store.SetAsync("key-two", "value-two");

        (await _store.GetAsync("key-one")).Should().Be("value-one");
        (await _store.GetAsync("key-two")).Should().Be("value-two");
    }

    [Fact]
    public async Task GetAsync_MountedSecretFallback_ReturnsValue()
    {
        var mountDir = Path.Combine(_fixture.DirectoryPath, "mounted");
        Directory.CreateDirectory(mountDir);
        await File.WriteAllTextAsync(Path.Combine(mountDir, "provider__token"), "mounted-token-value");

        var store = new EncryptedFileStore(
            basePath: _fixture.DirectoryPath,
            options: new EncryptedFileStoreOptions
            {
                MountedSecretsPath = mountDir,
            });

        var value = await store.GetAsync("provider:token");

        value.Should().Be("mounted-token-value");
    }

    [Fact]
    public async Task GetAsync_LegacyCiphertext_RemainsReadable_OnNonWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const string Key = "legacy-key";
        const string Plaintext = "legacy-value";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Key)))[..16];
        var mapPath = Path.Combine(_fixture.DirectoryPath, "keymap.json");
        var payloadPath = Path.Combine(_fixture.DirectoryPath, $"{hash}.enc");
        await File.WriteAllTextAsync(mapPath, JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Key] = hash,
        }));
        await File.WriteAllBytesAsync(payloadPath, LegacyProtect(Encoding.UTF8.GetBytes(Plaintext)));

        var value = await _store.GetAsync(Key);

        value.Should().Be(Plaintext);
    }

    [Fact]
    public async Task VaultBackend_StoresReadsAndListsCredentials()
    {
        var handler = new FakeVaultHandler();
        var httpClient = new HttpClient(handler);

        var store = new EncryptedFileStore(
            basePath: _fixture.DirectoryPath,
            httpClient: httpClient,
            options: new EncryptedFileStoreOptions
            {
                VaultAddress = "https://vault.example.test",
                VaultToken = "test-token",
                VaultMount = "secret",
                VaultPrefix = "jdai/credentials",
            });

        await store.SetAsync("vault:key", "vault-value");
        var value = await store.GetAsync("vault:key");
        var keys = await store.ListKeysAsync("vault:");

        value.Should().Be("vault-value");
        keys.Should().Contain("vault:key");

        await store.RemoveAsync("vault:key");
        (await store.GetAsync("vault:key")).Should().BeNull();
    }

    private static byte[] LegacyProtect(byte[] data)
    {
        using var aes = Aes.Create();
        var keyMaterial = Encoding.UTF8.GetBytes(
            $"{Environment.UserName}:{Environment.MachineName}:jdai-credential-store");
        aes.Key = SHA256.HashData(keyMaterial);
        aes.GenerateIV();

        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }

        return ms.ToArray();
    }

    private sealed class FakeVaultHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var method = request.Method.Method.ToUpperInvariant();

            if (segments.Length < 5)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var endpointType = segments[2];
            var encodedKey = segments.Length > 5
                ? segments[^1]
                : string.Empty;

            if (string.Equals(method, "LIST", StringComparison.Ordinal))
            {
                return Json(new
                {
                    data = new
                    {
                        keys = _values.Keys.ToArray(),
                    },
                });
            }

            if (string.Equals(method, "GET", StringComparison.Ordinal) &&
                string.Equals(endpointType, "data", StringComparison.Ordinal))
            {
                if (!_values.TryGetValue(encodedKey, out var value))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return Json(new
                {
                    data = new
                    {
                        data = new
                        {
                            value,
                        },
                    },
                });
            }

            if (string.Equals(method, "POST", StringComparison.Ordinal) &&
                string.Equals(endpointType, "data", StringComparison.Ordinal))
            {
                var json = await request.Content!
                    .ReadFromJsonAsync<JsonElement>(cancellationToken)
                    .ConfigureAwait(false);
                var value = json.GetProperty("data").GetProperty("value").GetString() ?? string.Empty;
                _values[encodedKey] = value;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            if (string.Equals(method, "DELETE", StringComparison.Ordinal) &&
                string.Equals(endpointType, "metadata", StringComparison.Ordinal))
            {
                _values.Remove(encodedKey);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(object payload)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload),
            };
        }
    }
}
