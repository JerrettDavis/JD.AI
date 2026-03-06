# ADR-008: Secret Vault Backend Packages

**Date**: 2025-07  
**Status**: Accepted  
**Deciders**: JD  
**Issue**: [#204](https://github.com/JerrettDavis/JD.AI/issues/204)

---

## Context

JD.AI already ships `ICredentialStore` with three built-in implementations:
`EnvironmentCredentialStore`, `EncryptedFileStore`, and `ChainedCredentialStore`.
A HashiCorp Vault backend (`VaultCredentialBackend`) also exists inside `JD.AI.Core`.

Enterprise deployments typically use cloud-native secret managers (Azure Key Vault,
AWS Secrets Manager) rather than Vault or local file stores. Adding these backends
directly to `JD.AI.Core` would pull in the Azure SDK and AWS SDK for every consumer,
even those that never connect to a cloud provider.

## Decision

Introduce two new library packages:

| Package | Backend |
|---|---|
| `JD.AI.Credentials.Azure` | Azure Key Vault (`Azure.Security.KeyVault.Secrets`) |
| `JD.AI.Credentials.Aws` | AWS Secrets Manager (`AWSSDK.SecretsManager`) |

Both implement the existing `ICredentialStore` interface. Neither is referenced by
`JD.AI.Core` or the CLI — they are opt-in via `AddAzureKeyVaultCredentialStore()` /
`AddAwsSecretsManagerCredentialStore()` extension methods.

## Consequences

### Positive

- **Zero mandatory dependency bloat**: consumers only pull in what they use.
- **Interface compatibility**: drop-in for any existing `ICredentialStore` consumer.
- **Independently versioned**: each package can ship a patch without forcing a Core bump.
- **Testable by design**: `SecretClient` and `IAmazonSecretsManager` are abstractions
  that can be substituted (NSubstitute used in unit tests).

### Negative / Trade-offs

- Users must add an extra NuGet reference per backend they want. Acceptable given most
  deployments use exactly one store.
- `AzureKeyVaultCredentialStore` maps key slashes to hyphens because Azure KV names
  must match `^[0-9a-zA-Z-]+$`. This loses fidelity if keys differ only by
  slash-vs-hyphen — callers should use consistent naming conventions.

## Alternatives Considered

**Keep all backends in JD.AI.Core** — rejected; imports ~30 MB of transitive dependencies
for users that only run against Ollama or Copilot.

**Single `JD.AI.Credentials` package for all backends** — rejected; AWS SDK alone is
~15 MB. Shipping both SDKs together forces every Azure-only user to carry the AWS SDK.
