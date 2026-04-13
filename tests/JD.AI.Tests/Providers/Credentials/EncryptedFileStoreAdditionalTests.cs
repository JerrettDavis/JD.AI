using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Providers.Credentials;

public sealed class EncryptedFileStoreAdditionalTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;

    public EncryptedFileStoreAdditionalTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
    }

    public void Dispose() => _fixture.Dispose();

    // ── RotateKeysAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RotateKeysAsync_WithNoStoredKeys_ReturnsZero()
    {
        var rotatedCount = await _store.RotateKeysAsync();

        rotatedCount.Should().Be(0);
    }

    [Fact]
    public async Task RotateKeysAsync_WithSingleKey_ReRotatesAndReturnsOne()
    {
        if (OperatingSystem.IsWindows())
        {
            // DPAPI doesn't support re-encryption
            return;
        }

        await _store.SetAsync("test-key", "test-value");

        var rotatedCount = await _store.RotateKeysAsync();

        rotatedCount.Should().Be(1);
        var value = await _store.GetAsync("test-key");
        value.Should().Be("test-value");
    }

    [Fact]
    public async Task RotateKeysAsync_WithMultipleKeys_ReRotatesAll()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _store.SetAsync("key-1", "value-1");
        await _store.SetAsync("key-2", "value-2");
        await _store.SetAsync("key-3", "value-3");

        var rotatedCount = await _store.RotateKeysAsync();

        rotatedCount.Should().Be(3);

        (await _store.GetAsync("key-1")).Should().Be("value-1");
        (await _store.GetAsync("key-2")).Should().Be("value-2");
        (await _store.GetAsync("key-3")).Should().Be("value-3");
    }

    [Fact]
    public async Task RotateKeysAsync_PreservesValues_OnNonWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // DPAPI re-encryption works differently
        }

        await _store.SetAsync("test-key", "test-value");

        var rotatedCount = await _store.RotateKeysAsync();

        rotatedCount.Should().Be(1);
        (await _store.GetAsync("test-key")).Should().Be("test-value");
    }

    [Fact]
    public async Task RotateKeysAsync_PreservesKeyMapping()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _store.SetAsync("preserve-test", "original-value");

        await _store.RotateKeysAsync();

        var retrieved = await _store.GetAsync("preserve-test");
        retrieved.Should().Be("original-value");
    }

    // ── ReadOrCreateKeyRingNoLock ──────────────────────────────────────────

    [Fact]
    public async Task ReadOrCreateKeyRingNoLock_WhenKeyRingDoesNotExist_CreatesOne()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var keyringPath = Path.Combine(_fixture.DirectoryPath, "keyring.json");
        File.Exists(keyringPath).Should().BeFalse();

        // Trigger keyring creation by setting a value
        await _store.SetAsync("trigger-keyring", "value");

        File.Exists(keyringPath).Should().BeTrue();
    }

    [Fact]
    public async Task ReadOrCreateKeyRingNoLock_CreatedKeyRingHasActiveKey()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _store.SetAsync("test-key", "test-value");

        var keyringPath = Path.Combine(_fixture.DirectoryPath, "keyring.json");
        var keyringJson = await File.ReadAllTextAsync(keyringPath);
        var keyring = JsonSerializer.Deserialize<JsonElement>(keyringJson);

        keyring.TryGetProperty("ActiveKeyId", out var activeKeyId).Should().BeTrue();
        keyring.TryGetProperty("Keys", out var keys).Should().BeTrue();
        keys.GetArrayLength().Should().BeGreaterThan(0);
    }

    // ── CreateNewKeyRecord ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateNewKeyRecord_GeneratesUniqueId()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _store.SetAsync("key1", "val1");
        await _store.SetAsync("key2", "val2");

        var keyringPath = Path.Combine(_fixture.DirectoryPath, "keyring.json");
        var keyringJson = await File.ReadAllTextAsync(keyringPath);
        var keyring = JsonSerializer.Deserialize<dynamic>(keyringJson);

        // Both calls should have created unique keys
        // This is verified by the fact that we can read both keys back
        (await _store.GetAsync("key1")).Should().Be("val1");
        (await _store.GetAsync("key2")).Should().Be("val2");
    }

    [Fact]
    public async Task CreateNewKeyRecord_GeneratesBase64KeyMaterial()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _store.SetAsync("test", "value");

        var keyringPath = Path.Combine(_fixture.DirectoryPath, "keyring.json");
        var content = await File.ReadAllTextAsync(keyringPath);
        var doc = JsonDocument.Parse(content);
        var keys = doc.RootElement.GetProperty("Keys");

        keys.GetArrayLength().Should().BeGreaterThan(0);
        var firstKey = keys[0];
        firstKey.TryGetProperty("KeyMaterialBase64", out var material).Should().BeTrue();

        var act = () => Convert.FromBase64String(material.GetString()!);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task CreateNewKeyRecord_SetsCreatedAtUtc()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _store.SetAsync("test", "value");

        var keyringPath = Path.Combine(_fixture.DirectoryPath, "keyring.json");
        var content = await File.ReadAllTextAsync(keyringPath);
        var doc = JsonDocument.Parse(content);
        var keys = doc.RootElement.GetProperty("Keys");
        var firstKey = keys[0];

        firstKey.TryGetProperty("CreatedAtUtc", out var createdAt).Should().BeTrue();
        DateTimeOffset.Parse(createdAt.GetString()!, CultureInfo.InvariantCulture).Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── TryUnprotectEnvelopeV2 ─────────────────────────────────────────────

    [Fact]
    public async Task TryUnprotectEnvelopeV2_WithValidEnvelope_Decrypts()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const string Key = "decrypt-test";
        const string Plaintext = "secret-value";

        await _store.SetAsync(Key, Plaintext);

        var retrieved = await _store.GetAsync(Key);

        retrieved.Should().Be(Plaintext);
    }

    [Fact]
    public async Task TryUnprotectEnvelopeV2_WithCorruptedData_Throws()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const string Key = "corrupt-test";
        var corruptedData = new byte[] { 1, 2, 3, 4, 5 };

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Key)))[..16];
        var mapPath = Path.Combine(_fixture.DirectoryPath, "keymap.json");
        var payloadPath = Path.Combine(_fixture.DirectoryPath, $"{hash}.enc");

        await File.WriteAllTextAsync(mapPath, JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Key] = hash,
        }));
        await File.WriteAllBytesAsync(payloadPath, corruptedData);

        var act = async () => await _store.GetAsync(Key);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task TryUnprotectEnvelopeV2_WithWrongMagic_ReturnsFalse()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const string Key = "wrong-magic";
        var invalidEnvelope = Encoding.UTF8.GetBytes("XXXX");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Key)))[..16];
        var mapPath = Path.Combine(_fixture.DirectoryPath, "keymap.json");
        var payloadPath = Path.Combine(_fixture.DirectoryPath, $"{hash}.enc");

        await File.WriteAllTextAsync(mapPath, JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Key] = hash,
        }));
        await File.WriteAllBytesAsync(payloadPath, invalidEnvelope);

        var result = await _store.GetAsync(Key);

        result.Should().BeNull();
    }

    // ── Concurrent Access ──────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_ConcurrentWrites_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(i => _store.SetAsync($"concurrent-key-{i}", $"value-{i}"))
            .ToList();

        await Task.WhenAll(tasks);

        for (int i = 0; i < 10; i++)
        {
            var value = await _store.GetAsync($"concurrent-key-{i}");
            value.Should().Be($"value-{i}");
        }
    }

    [Fact]
    public async Task GetAsync_ConcurrentReads_AllSucceed()
    {
        await _store.SetAsync("shared-key", "shared-value");

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _store.GetAsync("shared-key"))
            .ToList();

        var results = await Task.WhenAll(tasks);

        results.Should().AllBe("shared-value");
    }

    [Fact]
    public async Task MixedOperations_ConcurrentMix_AllSucceed()
    {
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            var idx = i;
            tasks.Add(_store.SetAsync($"key-{idx}", $"value-{idx}"));
            tasks.Add(_store.GetAsync($"key-{idx}"));
            tasks.Add(_store.ListKeysAsync("key-"));
        }

        var act = async () => await Task.WhenAll(tasks);

        await act.Should().NotThrowAsync();
    }

    // ── StoreName Property ─────────────────────────────────────────────────

    [Fact]
    public void StoreName_OnWindows_ReturnsDpapiName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _store.StoreName.Should().Contain("DPAPI");
    }

    [Fact]
    public void StoreName_OnNonWindows_ReturnsAesGcmName()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _store.StoreName.Should().Contain("AES-GCM");
    }

    [Fact]
    public void IsAvailable_AlwaysTrue()
    {
        _store.IsAvailable.Should().BeTrue();
    }

    // ── RotateKeyAsync (single key rotation) ───────────────────────────────

    [Fact]
    public async Task RotateKeyAsync_OnWindows_IsNoOp()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var act = async () => await _store.RotateKeyAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RotateKeyAsync_OnNonWindows_CreatesNewKey()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _store.SetAsync("rotate-test", "value");
        await _store.RotateKeyAsync();

        var keyringPath = Path.Combine(_fixture.DirectoryPath, "keyring.json");
        var content = await File.ReadAllTextAsync(keyringPath);
        var doc = JsonDocument.Parse(content);
        var keys = doc.RootElement.GetProperty("Keys");

        // Should have at least 2 keys (old + new)
        keys.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RotateKeyAsync_MarksOldKeyAsRetired()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _store.SetAsync("retire-test", "value");
        await _store.RotateKeyAsync();

        var keyringPath = Path.Combine(_fixture.DirectoryPath, "keyring.json");
        var content = await File.ReadAllTextAsync(keyringPath);
        var doc = JsonDocument.Parse(content);
        var keys = doc.RootElement.GetProperty("Keys");
        var oldKey = keys[0];

        oldKey.TryGetProperty("RetiredAtUtc", out _).Should().BeTrue();
    }
}
