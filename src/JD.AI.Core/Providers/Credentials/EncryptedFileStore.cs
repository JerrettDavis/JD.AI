using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Cross-platform credential store using DPAPI (Windows) or AES-GCM with
/// locally rotated key material (Linux/macOS).
/// Credentials are stored in ~/.jdai/credentials/ by default.
/// </summary>
public sealed class EncryptedFileStore : ICredentialStore
{
    private static readonly byte[] EnvelopeMagic = Encoding.ASCII.GetBytes("JDAI2");
    private const byte EnvelopeVersion = 1;
    private const int NonceLength = 12;
    private const int TagLength = 16;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly System.Threading.Lock Lock = new();

    private readonly string _storePath;
    private readonly string _keyMapPath;
    private readonly string _keyRingPath;
    private readonly string _auditLogPath;
    private readonly string? _mountedSecretsPath;
    private readonly VaultCredentialBackend? _vaultBackend;

    public EncryptedFileStore(
        string? basePath = null,
        HttpClient? httpClient = null,
        EncryptedFileStoreOptions? options = null)
    {
        _storePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jdai", "credentials");

        Directory.CreateDirectory(_storePath);

        _keyMapPath = Path.Combine(_storePath, "keymap.json");
        _keyRingPath = Path.Combine(_storePath, "keyring.json");
        _auditLogPath = Path.Combine(_storePath, "access.audit.log");

        var resolved = options ?? EncryptedFileStoreOptions.FromEnvironment();
        _mountedSecretsPath = resolved.MountedSecretsPath;

        if (resolved.VaultConfigured)
        {
            _vaultBackend = new VaultCredentialBackend(httpClient ?? new HttpClient(), resolved);
        }
    }

    public bool IsAvailable => true;

    public string StoreName
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "DPAPI Encrypted File Store";
            }

            return _vaultBackend is not null
                ? "Vault + AES-GCM Encrypted File Store"
                : "AES-GCM Encrypted File Store";
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        if (_vaultBackend is not null)
        {
            try
            {
                var fromVault = await _vaultBackend.GetAsync(key, ct).ConfigureAwait(false);
                if (fromVault is not null)
                {
                    AppendAudit("get", key, "vault", success: true);
                    return fromVault;
                }

                AppendAudit("get", key, "vault", success: false, detail: "miss");
            }
#pragma warning disable CA1031 // Vault failures should not block local fallback
            catch (Exception ex)
#pragma warning restore CA1031
            {
                AppendAudit("get", key, "vault", success: false, detail: ex.GetType().Name);
            }
        }

        var filePath = ResolveFilePath(key, createMapping: false);
        if (File.Exists(filePath))
        {
            try
            {
                var encrypted = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
                var decrypted = Unprotect(encrypted);
                var value = Encoding.UTF8.GetString(decrypted);
                AppendAudit("get", key, "local", success: true);
                return value;
            }
#pragma warning disable CA1031 // Return null on corrupted entries and continue fallback chain
            catch (Exception ex)
#pragma warning restore CA1031
            {
                AppendAudit("get", key, "local", success: false, detail: ex.GetType().Name);
            }
        }

        var mountedValue = TryReadMountedSecret(key);
        if (mountedValue is not null)
        {
            AppendAudit("get", key, "mounted", success: true);
            return mountedValue;
        }

        AppendAudit("get", key, "local", success: false, detail: "miss");
        return null;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ct.ThrowIfCancellationRequested();

        if (_vaultBackend is not null)
        {
            try
            {
                await _vaultBackend.SetAsync(key, value, ct).ConfigureAwait(false);
                AppendAudit("set", key, "vault", success: true);
            }
#pragma warning disable CA1031 // Continue with local storage fallback if vault write fails
            catch (Exception ex)
#pragma warning restore CA1031
            {
                AppendAudit("set", key, "vault", success: false, detail: ex.GetType().Name);
            }
        }

        var filePath = ResolveFilePath(key, createMapping: true);
        var encrypted = Protect(Encoding.UTF8.GetBytes(value));
        lock (Lock)
        {
            File.WriteAllBytes(filePath, encrypted);
            TrySetFilePermissions(filePath);
        }

        AppendAudit("set", key, "local", success: true);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        if (_vaultBackend is not null)
        {
            try
            {
                await _vaultBackend.RemoveAsync(key, ct).ConfigureAwait(false);
                AppendAudit("remove", key, "vault", success: true);
            }
#pragma warning disable CA1031 // Continue local delete even when vault delete fails
            catch (Exception ex)
#pragma warning restore CA1031
            {
                AppendAudit("remove", key, "vault", success: false, detail: ex.GetType().Name);
            }
        }

        var filePath = ResolveFilePath(key, createMapping: false);
        lock (Lock)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var map = ReadKeyMapNoLock();
            if (map.Remove(key))
            {
                WriteKeyMapNoLock(map);
            }
        }

        AppendAudit("remove", key, "local", success: true);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ct.ThrowIfCancellationRequested();

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        lock (Lock)
        {
            foreach (var key in ReadKeyMapNoLock().Keys.Where(k =>
                         k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(key);
            }
        }

        if (_vaultBackend is not null)
        {
            try
            {
                var vaultKeys = await _vaultBackend.ListKeysAsync(ct).ConfigureAwait(false);
                foreach (var key in vaultKeys.Where(k =>
                             k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(key);
                }

                AppendAudit("list", prefix, "vault", success: true);
            }
#pragma warning disable CA1031 // Non-fatal vault list failure
            catch (Exception ex)
#pragma warning restore CA1031
            {
                AppendAudit("list", prefix, "vault", success: false, detail: ex.GetType().Name);
            }
        }

        AppendAudit("list", prefix, "local", success: true);
        return [.. result.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Rotates the local non-Windows AES-GCM key and marks the previous key as retired.
    /// Existing secrets remain readable with historical keys.
    /// </summary>
    public Task RotateKeyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        lock (Lock)
        {
            var keyRing = ReadOrCreateKeyRingNoLock();
            var active = keyRing.Keys.FirstOrDefault(k =>
                string.Equals(k.Id, keyRing.ActiveKeyId, StringComparison.OrdinalIgnoreCase));
            if (active is not null)
            {
                active.RetiredAtUtc ??= DateTimeOffset.UtcNow;
            }

            var next = CreateNewKeyRecord();
            keyRing.ActiveKeyId = next.Id;
            keyRing.Keys.Add(next);
            WriteKeyRingNoLock(keyRing);
        }

        AppendAudit("rotate", "store", "local", success: true);
        return Task.CompletedTask;
    }

    private string ResolveFilePath(string key, bool createMapping)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];

        lock (Lock)
        {
            var map = ReadKeyMapNoLock();
            if (map.TryGetValue(key, out var existingHash))
            {
                return Path.Combine(_storePath, $"{existingHash}.enc");
            }

            if (createMapping)
            {
                map[key] = hash;
                WriteKeyMapNoLock(map);
            }
        }

        return Path.Combine(_storePath, $"{hash}.enc");
    }

    private Dictionary<string, string> ReadKeyMapNoLock()
    {
        if (!File.Exists(_keyMapPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(
                   File.ReadAllText(_keyMapPath), JsonOptions) ??
               new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void WriteKeyMapNoLock(Dictionary<string, string> map)
    {
        File.WriteAllText(_keyMapPath, JsonSerializer.Serialize(map, JsonOptions));
        TrySetFilePermissions(_keyMapPath);
    }

    private byte[] Protect(byte[] data)
    {
#pragma warning disable CA1416 // Platform compatibility — guarded by OperatingSystem.IsWindows()
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
#pragma warning restore CA1416

        string keyId;
        byte[] keyMaterial;
        lock (Lock)
        {
            var keyRing = ReadOrCreateKeyRingNoLock();
            var active = keyRing.Keys.FirstOrDefault(k =>
                             string.Equals(k.Id, keyRing.ActiveKeyId, StringComparison.OrdinalIgnoreCase))
                         ?? throw new CryptographicException("Active credential key not found.");
            keyId = active.Id;
            keyMaterial = Convert.FromBase64String(active.KeyMaterialBase64);
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[data.Length];
        var tag = new byte[TagLength];

        using (var aes = new AesGcm(keyMaterial, TagLength))
        {
            aes.Encrypt(nonce, data, ciphertext, tag);
        }

        var envelope = new byte[EnvelopeMagic.Length + 1 + 16 + NonceLength + TagLength + ciphertext.Length];
        var offset = 0;
        Buffer.BlockCopy(EnvelopeMagic, 0, envelope, offset, EnvelopeMagic.Length);
        offset += EnvelopeMagic.Length;

        envelope[offset++] = EnvelopeVersion;

        var keyIdBytes = Guid.Parse(keyId).ToByteArray();
        Buffer.BlockCopy(keyIdBytes, 0, envelope, offset, keyIdBytes.Length);
        offset += keyIdBytes.Length;

        Buffer.BlockCopy(nonce, 0, envelope, offset, nonce.Length);
        offset += nonce.Length;

        Buffer.BlockCopy(tag, 0, envelope, offset, tag.Length);
        offset += tag.Length;

        Buffer.BlockCopy(ciphertext, 0, envelope, offset, ciphertext.Length);
        return envelope;
    }

    private byte[] Unprotect(byte[] data)
    {
#pragma warning disable CA1416 // Platform compatibility — guarded by OperatingSystem.IsWindows()
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }
#pragma warning restore CA1416

        if (TryUnprotectEnvelopeV2(data, out var decrypted))
        {
            return decrypted;
        }

        return LegacyUnprotect(data);
    }

    private bool TryUnprotectEnvelopeV2(byte[] data, out byte[] decrypted)
    {
        decrypted = [];
        var minimum = EnvelopeMagic.Length + 1 + 16 + NonceLength + TagLength;
        if (data.Length <= minimum)
        {
            return false;
        }

        for (var i = 0; i < EnvelopeMagic.Length; i++)
        {
            if (data[i] != EnvelopeMagic[i])
            {
                return false;
            }
        }

        var offset = EnvelopeMagic.Length;
        var version = data[offset++];
        if (version != EnvelopeVersion)
        {
            throw new CryptographicException($"Unsupported credential envelope version '{version}'.");
        }

        var keyIdBytes = new byte[16];
        Buffer.BlockCopy(data, offset, keyIdBytes, 0, keyIdBytes.Length);
        offset += keyIdBytes.Length;
        var keyId = new Guid(keyIdBytes).ToString("D");

        var nonce = new byte[NonceLength];
        Buffer.BlockCopy(data, offset, nonce, 0, nonce.Length);
        offset += nonce.Length;

        var tag = new byte[TagLength];
        Buffer.BlockCopy(data, offset, tag, 0, tag.Length);
        offset += tag.Length;

        var ciphertext = new byte[data.Length - offset];
        Buffer.BlockCopy(data, offset, ciphertext, 0, ciphertext.Length);

        byte[] keyMaterial;
        lock (Lock)
        {
            var keyRing = ReadOrCreateKeyRingNoLock();
            var key = keyRing.Keys.FirstOrDefault(k =>
                string.Equals(k.Id, keyId, StringComparison.OrdinalIgnoreCase));
            if (key is null)
            {
                throw new CryptographicException(
                    $"Credential key '{keyId}' not found in keyring. " +
                    "Rotate keys only after migrating encrypted secrets.");
            }

            keyMaterial = Convert.FromBase64String(key.KeyMaterialBase64);
        }

        decrypted = new byte[ciphertext.Length];
        using (var aes = new AesGcm(keyMaterial, TagLength))
        {
            aes.Decrypt(nonce, ciphertext, tag, decrypted);
        }

        return true;
    }

    private static byte[] LegacyUnprotect(byte[] data)
    {
        using var aes = Aes.Create();
        var keyMaterial = Encoding.UTF8.GetBytes(
            $"{Environment.UserName}:{Environment.MachineName}:jdai-credential-store");
        aes.Key = SHA256.HashData(keyMaterial);

        var iv = new byte[16];
        Array.Copy(data, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
        {
            cs.Write(data, 16, data.Length - 16);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Re-encrypts all stored credentials in place. On Windows this is a no-op
    /// (DPAPI handles key rotation at the OS level). On Linux/macOS this reads
    /// each credential and re-writes it, picking up any changes in the derived key material.
    /// Returns the number of credentials rotated.
    /// </summary>
    public async Task<int> RotateKeysAsync(CancellationToken ct = default)
    {
        var allKeys = await ListKeysAsync("", ct).ConfigureAwait(false);
        var rotated = 0;

        foreach (var key in allKeys)
        {
            ct.ThrowIfCancellationRequested();
            var value = await GetAsync(key, ct).ConfigureAwait(false);
            if (value is not null)
            {
                await SetAsync(key, value, ct).ConfigureAwait(false);
                rotated++;
            }
        }

        return rotated;
    }

    private KeyRingDocument ReadOrCreateKeyRingNoLock()
    {
        if (File.Exists(_keyRingPath))
        {
            var existing = JsonSerializer.Deserialize<KeyRingDocument>(
                File.ReadAllText(_keyRingPath), JsonOptions);
            if (existing?.Keys.Count > 0 &&
                existing.Keys.Any(k => string.Equals(k.Id, existing.ActiveKeyId, StringComparison.OrdinalIgnoreCase)))
            {
                return existing;
            }
        }

        var key = CreateNewKeyRecord();
        var created = new KeyRingDocument
        {
            ActiveKeyId = key.Id,
            Keys = [key],
        };
        WriteKeyRingNoLock(created);
        return created;
    }

    private void WriteKeyRingNoLock(KeyRingDocument keyRing)
    {
        File.WriteAllText(_keyRingPath, JsonSerializer.Serialize(keyRing, JsonOptions));
        TrySetFilePermissions(_keyRingPath);
    }

    private static KeyRecord CreateNewKeyRecord()
    {
        return new KeyRecord
        {
            Id = Guid.NewGuid().ToString("D"),
            KeyMaterialBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private string? TryReadMountedSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(_mountedSecretsPath) ||
            !Directory.Exists(_mountedSecretsPath))
        {
            return null;
        }

        var candidates = new[]
        {
            key,
            key.Replace(':', '_'),
            key.Replace(":", "__", StringComparison.Ordinal),
        };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(_mountedSecretsPath, candidate);
            if (File.Exists(path))
            {
                return File.ReadAllText(path).Trim();
            }
        }

        return null;
    }

    private void AppendAudit(string action, string keyOrPrefix, string backend, bool success, string? detail = null)
    {
        var keyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(keyOrPrefix)))[..12];

        var line =
            $"{DateTimeOffset.UtcNow:O} action={action} keyHash={keyHash} backend={backend} success={success}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            line += $" detail={detail}";
        }

        lock (Lock)
        {
            File.AppendAllText(_auditLogPath, line + Environment.NewLine);
            TrySetFilePermissions(_auditLogPath);
        }
    }

    private static void TrySetFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

#pragma warning disable CA1416 // Guarded by OS check
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
#pragma warning disable CA1031 // Best-effort permissions hardening
        catch
#pragma warning restore CA1031
        {
            // Best effort permissions hardening.
        }
#pragma warning restore CA1416
    }

    private sealed record KeyRingDocument
    {
        public string ActiveKeyId { get; set; } = string.Empty;
        public List<KeyRecord> Keys { get; set; } = [];
    }

    private sealed record KeyRecord
    {
        public string Id { get; set; } = string.Empty;
        public string KeyMaterialBase64 { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? RetiredAtUtc { get; set; }
    }
}
