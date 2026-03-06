using Amazon;
using Amazon.SecretsManager;
using JD.AI.Core.Providers.Credentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JD.AI.Credentials.Aws;

/// <summary>
/// Configuration options for <see cref="AwsSecretsManagerCredentialStore"/>.
/// </summary>
public sealed class AwsSecretsManagerCredentialStoreOptions
{
    /// <summary>
    /// The AWS region (e.g. <c>us-east-1</c>). If null, the SDK default region resolution is used.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Optional prefix applied to all secret names (e.g. <c>"jdai/"</c>).
    /// </summary>
    public string Prefix { get; set; } = string.Empty;
}

/// <summary>
/// Extension methods for registering the AWS Secrets Manager credential store.
/// </summary>
public static class AwsCredentialsServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="AwsSecretsManagerCredentialStore"/> as the <see cref="ICredentialStore"/> singleton,
    /// using the default AWS credential chain.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configure region and optional prefix.</param>
    public static IServiceCollection AddAwsSecretsManagerCredentialStore(
        this IServiceCollection services,
        Action<AwsSecretsManagerCredentialStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new AwsSecretsManagerCredentialStoreOptions();
        configure?.Invoke(options);

        services.AddSingleton<IAmazonSecretsManager>(_ =>
        {
            if (!string.IsNullOrWhiteSpace(options.Region))
            {
                return new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(options.Region));
            }

            return new AmazonSecretsManagerClient();
        });

        services.AddSingleton<ICredentialStore>(sp =>
            new AwsSecretsManagerCredentialStore(
                sp.GetRequiredService<IAmazonSecretsManager>(),
                sp.GetRequiredService<ILogger<AwsSecretsManagerCredentialStore>>(),
                options.Prefix));

        return services;
    }
}
