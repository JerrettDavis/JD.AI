using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Cross-platform credential store using DPAPI (Windows) or AES with
/// a machine-specific key derived from the user profile path.
/// Credentials stored in ~/.jdai/credentials/.
/// </summary>
public sealed class EncryptedFileStore : ICredentialStore
{
    private readonly string _storePath;
    private static readonly System.Threading.Lock Lock = new();

    public EncryptedFileStore(string? basePath = null)
    {
        _storePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jdai", "credentials");
        Directory.CreateDirectory(_storePath);
    }

    public bool IsAvailable => true;

    public string StoreName => OperatingSystem.IsWindows()
        ? "DPAPI Encrypted File Store"
        : "AES Encrypted File Store";

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var filePath = GetFilePath(key);
        if (!File.Exists(filePath))
            return Task.FromResult<string?>(null);

        try
        {
            var encrypted = File.ReadAllBytes(filePath);
            var decrypted = Unprotect(encrypted);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(decrypted));
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var filePath = GetFilePath(key);
        var encrypted = Protect(Encoding.UTF8.GetBytes(value));
        lock (Lock)
        {
            File.WriteAllBytes(filePath, encrypted);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        var filePath = GetFilePath(key);
        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
    {
        var mapPath = Path.Combine(_storePath, "keymap.json");
        if (!File.Exists(mapPath))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(mapPath)) ?? [];
        var result = map.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    private string GetFilePath(string key)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];

        var mapPath = Path.Combine(_storePath, "keymap.json");
        Dictionary<string, string> map;
        lock (Lock)
        {
            map = File.Exists(mapPath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(mapPath)) ?? []
                : [];
            if (!map.ContainsKey(key))
            {
                map[key] = hash;
                File.WriteAllText(mapPath, JsonSerializer.Serialize(map));
            }
        }

        return Path.Combine(_storePath, $"{hash}.enc");
    }

    private static byte[] Protect(byte[] data)
    {
#pragma warning disable CA1416 // Platform compatibility — guarded by OperatingSystem.IsWindows()
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
#pragma warning restore CA1416

        // Non-Windows: AES with a deterministic key derived from username + machine
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

    private static byte[] Unprotect(byte[] data)
    {
#pragma warning disable CA1416 // Platform compatibility — guarded by OperatingSystem.IsWindows()
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }
#pragma warning restore CA1416

        // Non-Windows: AES decrypt
        using var aes = Aes.Create();
        var keyMaterial = Encoding.UTF8.GetBytes(
            $"{Environment.UserName}:{Environment.MachineName}:jdai-credential-store");
        aes.Key = SHA256.HashData(keyMaterial);

        var iv = new byte[16];
        Array.Copy(data, 0, iv, 0, 16);
        aes.IV = iv;

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
        {
            cs.Write(data, 16, data.Length - 16);
        }

        return ms.ToArray();
    }
}
