namespace JD.AI.Core.Security;

/// <summary>
/// Chains multiple <see cref="IAuthProvider"/> implementations, trying each
/// in order until one succeeds. Enables API key + JWT + future auth methods
/// to coexist.
/// </summary>
public sealed class CompositeAuthProvider : IAuthProvider
{
    private readonly IReadOnlyList<IAuthProvider> _providers;

    public CompositeAuthProvider(params IAuthProvider[] providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    public CompositeAuthProvider(IEnumerable<IAuthProvider> providers)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>Number of registered auth providers.</summary>
    public int ProviderCount => _providers.Count;

    public async Task<GatewayIdentity?> AuthenticateAsync(string credential, CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            var identity = await provider.AuthenticateAsync(credential, ct).ConfigureAwait(false);
            if (identity is not null)
                return identity;
        }

        return null;
    }
}
