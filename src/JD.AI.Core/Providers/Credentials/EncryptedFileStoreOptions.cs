namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Configuration options for <see cref="EncryptedFileStore"/>.
/// </summary>
public sealed record EncryptedFileStoreOptions
{
    public const string VaultAddressEnvVar = "JDAI_VAULT_ADDR";
    public const string VaultTokenEnvVar = "JDAI_VAULT_TOKEN";
    public const string VaultMountEnvVar = "JDAI_VAULT_MOUNT";
    public const string VaultPrefixEnvVar = "JDAI_VAULT_PREFIX";
    public const string MountedSecretsPathEnvVar = "JDAI_CREDENTIALS_MOUNT_PATH";

    public string? VaultAddress { get; init; }
    public string? VaultToken { get; init; }
    public string VaultMount { get; init; } = "secret";
    public string VaultPrefix { get; init; } = "jdai/credentials";
    public string? MountedSecretsPath { get; init; }

    internal bool VaultConfigured =>
        !string.IsNullOrWhiteSpace(VaultAddress) &&
        !string.IsNullOrWhiteSpace(VaultToken);

    public static EncryptedFileStoreOptions FromEnvironment()
    {
        return new EncryptedFileStoreOptions
        {
            VaultAddress = Environment.GetEnvironmentVariable(VaultAddressEnvVar),
            VaultToken = Environment.GetEnvironmentVariable(VaultTokenEnvVar),
            VaultMount = Environment.GetEnvironmentVariable(VaultMountEnvVar) is { Length: > 0 } mount
                ? mount
                : "secret",
            VaultPrefix = Environment.GetEnvironmentVariable(VaultPrefixEnvVar) is { Length: > 0 } prefix
                ? prefix
                : "jdai/credentials",
            MountedSecretsPath = Environment.GetEnvironmentVariable(MountedSecretsPathEnvVar),
        };
    }
}
