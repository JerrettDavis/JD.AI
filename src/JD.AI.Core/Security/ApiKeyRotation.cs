using System.Security.Cryptography;

namespace JD.AI.Core.Security;

/// <summary>
/// Manages API key lifecycle: generation, rotation, and expiry tracking.
/// </summary>
public sealed class ApiKeyRotation
{
    private readonly Dictionary<string, ApiKeyRecord> _keys = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>Number of active (non-expired, non-revoked) keys.</summary>
    public int ActiveKeyCount
    {
        get
        {
            lock (_lock)
                return _keys.Values.Count(k => !k.IsRevoked && !k.IsExpired);
        }
    }

    /// <summary>
    /// Generates a new API key with optional expiry.
    /// </summary>
    /// <returns>The generated API key string.</returns>
    public string GenerateKey(string name, GatewayRole role = GatewayRole.User, TimeSpan? expiry = null)
    {
        var key = GenerateSecureKey();
        var record = new ApiKeyRecord
        {
            Key = key,
            Name = name,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiry.HasValue ? DateTimeOffset.UtcNow + expiry.Value : null,
        };

        lock (_lock)
        {
            _keys[key] = record;
        }

        return key;
    }

    /// <summary>
    /// Rotates a key: generates a new key and revokes the old one.
    /// Returns the new key, or null if the old key was not found.
    /// </summary>
    public string? RotateKey(string oldKey, TimeSpan? newExpiry = null)
    {
        lock (_lock)
        {
            if (!_keys.TryGetValue(oldKey, out var oldRecord))
                return null;

            // Revoke old key
            oldRecord.IsRevoked = true;
            oldRecord.RevokedAt = DateTimeOffset.UtcNow;

            // Generate replacement
            var newKey = GenerateSecureKey();
            _keys[newKey] = new ApiKeyRecord
            {
                Key = newKey,
                Name = oldRecord.Name,
                Role = oldRecord.Role,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = newExpiry.HasValue ? DateTimeOffset.UtcNow + newExpiry.Value : null,
                PreviousKey = oldKey,
            };

            return newKey;
        }
    }

    /// <summary>
    /// Validates and retrieves a key record. Returns null if key is invalid,
    /// expired, or revoked.
    /// </summary>
    public ApiKeyRecord? Validate(string key)
    {
        lock (_lock)
        {
            if (!_keys.TryGetValue(key, out var record))
                return null;

            if (record.IsRevoked || record.IsExpired)
                return null;

            return record;
        }
    }

    /// <summary>
    /// Explicitly revokes a key.
    /// </summary>
    public bool RevokeKey(string key)
    {
        lock (_lock)
        {
            if (!_keys.TryGetValue(key, out var record))
                return false;

            record.IsRevoked = true;
            record.RevokedAt = DateTimeOffset.UtcNow;
            return true;
        }
    }

    /// <summary>Records a usage touch on the specified key.</summary>
    public bool TouchKey(string key)
    {
        lock (_lock)
        {
            if (!_keys.TryGetValue(key, out var record))
                return false;

            record.LastUsedAt = DateTimeOffset.UtcNow;
            record.UsageCount++;
            return true;
        }
    }

    /// <summary>Gets all key records (for admin/audit purposes).</summary>
    public IReadOnlyList<ApiKeyRecord> GetAllKeys()
    {
        lock (_lock)
        {
            return _keys.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Records usage of a key (last used timestamp and increment counter).
    /// Returns true if the key was found and updated, false otherwise.
    /// </summary>
    public bool TouchKey(string key)
    {
        lock (_lock)
        {
            if (!_keys.TryGetValue(key, out var record))
                return false;

            if (record.IsRevoked || record.IsExpired)
                return false;

            record.LastUsedAt = DateTimeOffset.UtcNow;
            record.UsageCount++;
            return true;
        }
    }

    private static string GenerateSecureKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"jdai_{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}

/// <summary>
/// Metadata for an API key.
/// </summary>
public sealed class ApiKeyRecord
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public GatewayRole Role { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? PreviousKey { get; init; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public long UsageCount { get; set; }

    /// <summary>When the key was last used for authentication.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Number of times this key has been used for authentication.</summary>
    public long UsageCount { get; set; }

    /// <summary>Whether this key has passed its expiry date.</summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;
}
