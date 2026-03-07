using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using JD.AI.Core.Providers.Credentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JD.AI.Credentials.Azure;

/// <summary>
/// Configuration options for <see cref="AzureKeyVaultCredentialStore"/>.
/// </summary>
public sealed class AzureKeyVaultCredentialStoreOptions
{
    /// <summary>The URI of the Azure Key Vault, e.g. <c>https://myvault.vault.azure.net/</c>.</summary>
    public Uri? VaultUri { get; set; }
}

/// <summary>
/// Extension methods for registering Azure Key Vault credential store.
/// </summary>
public static class AzureCredentialsServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="AzureKeyVaultCredentialStore"/> as the <see cref="ICredentialStore"/> singleton,
    /// using <see cref="DefaultAzureCredential"/> for authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configure the vault URI.</param>
    public static IServiceCollection AddAzureKeyVaultCredentialStore(
        this IServiceCollection services,
        Action<AzureKeyVaultCredentialStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AzureKeyVaultCredentialStoreOptions();
        configure(options);

        if (options.VaultUri is null)
        {
            throw new InvalidOperationException("VaultUri must be set when configuring Azure Key Vault credential store.");
        }

        services.AddSingleton(_ => new SecretClient(options.VaultUri, new DefaultAzureCredential()));
        services.AddSingleton<ICredentialStore>(sp =>
            new AzureKeyVaultCredentialStore(
                sp.GetRequiredService<SecretClient>(),
                sp.GetRequiredService<ILogger<AzureKeyVaultCredentialStore>>()));

        return services;
    }
}
