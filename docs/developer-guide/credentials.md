---
title: "Credential Management"
description: "ICredentialStore interface, built-in implementations (EncryptedFileStore, EnvironmentCredentialStore, VaultCredentialBackend, ChainedCredentialStore, AuditingCredentialStore), provider add workflow, and security best practices."
---

# Credential Management

JD.AI uses a layered credential store system to securely store and retrieve API keys, tokens, and other secrets. All credential access goes through the `ICredentialStore` abstraction, allowing different backends to be composed without changing application code.

## Overview and security model

Credentials are referenced by a **named key** (e.g., `openai-api-key`, `anthropic-api-key`). The application never embeds secrets in configuration files or source code — only the key name is referenced at call sites.

The default production setup is a `ChainedCredentialStore` that tries:

1. `EncryptedFileStore` — encrypted local file storage
2. `EnvironmentCredentialStore` — environment variable fallback

For enterprise deployments, `VaultCredentialBackend` can be prepended to the chain to centralise secret management in HashiCorp Vault.

All access can be wrapped with `AuditingCredentialStore` to produce a tamper-evident audit trail.

## ICredentialStore interface

All implementations live under `src/JD.AI.Core/Credentials/` and implement:

```csharp
public interface ICredentialStore
{
    /// <summary>Retrieves a credential by name. Returns null if not found.</summary>
    Task<string?> GetAsync(string name, CancellationToken ct = default);

    /// <summary>Stores or overwrites a credential.</summary>
    Task SetAsync(string name, string value, CancellationToken ct = default);

    /// <summary>Deletes a credential. No-op if it does not exist.</summary>
    Task DeleteAsync(string name, CancellationToken ct = default);

    /// <summary>Returns true if the credential exists.</summary>
    Task<bool> ExistsAsync(string name, CancellationToken ct = default);
}
```

| Method | Behaviour |
|--------|-----------|
| `GetAsync` | Returns the plaintext credential, or `null` if absent |
| `SetAsync` | Creates or overwrites; encrypts at rest where applicable |
| `DeleteAsync` | Idempotent removal |
| `ExistsAsync` | Presence check without returning the value |

## Implementations

### EncryptedFileStore

Stores credentials as individual encrypted files in `~/.jdai/credentials/`. Each file is named after the credential key.

**Encryption**

| Platform | Algorithm |
|----------|-----------|
| Windows  | DPAPI (`ProtectedData.Protect`) — key tied to Windows user account |
| Linux / macOS | AES-256-GCM — key derived from machine identity + user-supplied entropy |

The AES key derivation uses HKDF over a combination of a machine-unique identifier (e.g., `/etc/machine-id` or `IOPlatformUUID`) and an optional passphrase from `JDAI_KEY_ENTROPY`. Without `JDAI_KEY_ENTROPY` set, the key is derived from machine identity alone.

**Registration**

```csharp
services.AddSingleton<ICredentialStore, EncryptedFileStore>();
```

**Configuration** (`appsettings.json`)

```json
{
  "CredentialStore": {
    "EncryptedFile": {
      "Directory": "~/.jdai/credentials"
    }
  }
}
```

---

### EnvironmentCredentialStore

Reads credentials from environment variables. The variable name is derived from the credential key:

```
Credential key:   openai-api-key
Environment var:  JDAI_CRED_OPENAI_API_KEY
```

Transformation: upper-case, replace `-` and `.` with `_`, prepend `JDAI_CRED_`.

`SetAsync` and `DeleteAsync` are no-ops — environment variables are read-only from the perspective of the store.

```csharp
services.AddSingleton<ICredentialStore, EnvironmentCredentialStore>();
```

**Use cases**

- CI/CD pipelines where secrets are injected as environment variables
- Container deployments with secrets mounted as env vars
- Overriding a specific credential without modifying the encrypted store

---

### VaultCredentialBackend

Integrates with [HashiCorp Vault](https://www.vaultproject.io/) KV v2 secrets engine for enterprise secret management.

**Configuration**

Set these environment variables before starting JD.AI:

| Variable | Description |
|----------|-------------|
| `VAULT_ADDR` | Vault server address, e.g., `https://vault.example.com:8200` |
| `VAULT_TOKEN` | Vault token with read/write access to the configured path |

Optional `appsettings.json` settings:

```json
{
  "CredentialStore": {
    "Vault": {
      "MountPath": "secret",
      "BasePath": "jdai/credentials"
    }
  }
}
```

**Registration**

```csharp
services.AddSingleton<ICredentialStore, VaultCredentialBackend>();
```

Secrets are stored at `<MountPath>/data/<BasePath>/<name>` in Vault. The `VaultCredentialBackend` uses the Vault KV v2 API and respects Vault's own access control policies.

---

### ChainedCredentialStore

Tries a prioritised list of stores in order, returning the first non-null result from `GetAsync`. `SetAsync` writes to the **first** store in the chain.

```csharp
services.AddSingleton<ICredentialStore>(sp => new ChainedCredentialStore(
    sp.GetRequiredService<VaultCredentialBackend>(),
    sp.GetRequiredService<EncryptedFileStore>(),
    sp.GetRequiredService<EnvironmentCredentialStore>()
));
```

**Priority order example**

| Priority | Store | Rationale |
|----------|-------|-----------|
| 1 | `VaultCredentialBackend` | Central enterprise secret — highest trust |
| 2 | `EncryptedFileStore` | Local encrypted store — developer workstation |
| 3 | `EnvironmentCredentialStore` | Last-resort env var override |

**Default production chain** (without Vault):

```csharp
new ChainedCredentialStore(
    new EncryptedFileStore(options),
    new EnvironmentCredentialStore()
)
```

---

### AuditingCredentialStore

Wraps any `ICredentialStore` and logs every `Get`, `Set`, `Delete`, and `Exists` call to the JD.AI audit log. Use this for compliance requirements or security monitoring.

```csharp
services.AddSingleton<ICredentialStore>(sp =>
{
    var inner = new ChainedCredentialStore(
        sp.GetRequiredService<EncryptedFileStore>(),
        sp.GetRequiredService<EnvironmentCredentialStore>());

    return new AuditingCredentialStore(inner, sp.GetRequiredService<IAuditLogger>());
});
```

Each audit entry records:

| Field | Value |
|-------|-------|
| `timestamp` | UTC time of access |
| `operation` | `Get`, `Set`, `Delete`, `Exists` |
| `credentialName` | The key name (never the value) |
| `succeeded` | Whether the operation returned a result |
| `caller` | Stack frame or component that initiated the call |

Credential values are **never** written to the audit log.

---

## ChainedCredentialStore composition pattern

Compose stores to match your deployment environment:

```csharp
// Development: encrypted local store with env var override
var devStore = new ChainedCredentialStore(
    new EncryptedFileStore(devOptions),
    new EnvironmentCredentialStore());

// CI: environment-only
var ciStore = new EnvironmentCredentialStore();

// Enterprise: Vault → encrypted → env, with auditing
var enterpriseStore = new AuditingCredentialStore(
    new ChainedCredentialStore(
        new VaultCredentialBackend(vaultOptions),
        new EncryptedFileStore(prodOptions),
        new EnvironmentCredentialStore()),
    auditLogger);
```

Register conditionally via configuration:

```csharp
if (config["CredentialStore:UseVault"] == "true")
    services.AddSingleton<ICredentialStore, VaultCredentialBackend>();
else
    services.AddSingleton<ICredentialStore, EncryptedFileStore>();
```

---

## /provider add workflow

The `/provider add` wizard prompts for API keys and stores them via `ICredentialStore.SetAsync`. The wizard:

1. Presents a list of supported providers
2. Prompts for the API key (masked input)
3. Calls `credentialStore.SetAsync("openai-api-key", enteredKey)`
4. Verifies the credential round-trips correctly via `GetAsync`

The provider configuration file (`~/.jdai/providers.json`) stores only the **credential key name**, not the value:

```json
{
  "providers": [
    {
      "name": "openai",
      "type": "OpenAI",
      "credentialKey": "openai-api-key"
    }
  ]
}
```

---

## Manual credential management

Use the `jdai credential` CLI commands:

```bash
# Store a credential
jdai credential set openai-api-key

# Check if a credential exists
jdai credential exists openai-api-key

# Delete a credential
jdai credential delete openai-api-key

# List stored credential names (values are never displayed)
jdai credential list
```

Or via the `ICredentialStore` API directly in code:

```csharp
await credentialStore.SetAsync("my-service-key", secretValue);
bool present = await credentialStore.ExistsAsync("my-service-key");
string? key = await credentialStore.GetAsync("my-service-key");
await credentialStore.DeleteAsync("my-service-key");
```

---

## Environment variable override

Any credential can be overridden at runtime without modifying the encrypted store by setting the corresponding `JDAI_CRED_*` variable:

```bash
# Override openai-api-key for a single session
export JDAI_CRED_OPENAI_API_KEY=sk-...
jdai chat "Hello"
```

`EnvironmentCredentialStore` is always the last store in the default chain, so environment variables serve as a last-resort override. To make environment variables take precedence, place `EnvironmentCredentialStore` first in a custom `ChainedCredentialStore`.

---

## Vault integration setup

1. **Install and start Vault** (or point to your existing cluster)
2. **Enable KV v2** on the desired mount path:
   ```bash
   vault secrets enable -path=secret kv-v2
   ```
3. **Create a policy** granting JD.AI access:
   ```hcl
   path "secret/data/jdai/credentials/*" {
     capabilities = ["create", "read", "update", "delete"]
   }
   ```
4. **Generate a token** with that policy:
   ```bash
   vault token create -policy=jdai-credentials
   ```
5. **Configure JD.AI**:
   ```bash
   export VAULT_ADDR=https://vault.example.com:8200
   export VAULT_TOKEN=hvs.XXXX
   ```
6. **Add VaultCredentialBackend** to the DI chain (see [ChainedCredentialStore](#chainedcredentialstore)).

Vault's own audit logs provide an independent record of all secret access, complementing `AuditingCredentialStore`.

---

## Security best practices

| Practice | Recommendation |
|----------|----------------|
| **Principle of least privilege** | Grant each component only the credential keys it needs |
| **Key rotation** | Rotate API keys regularly; use `SetAsync` to update in place |
| **Audit logging** | Enable `AuditingCredentialStore` in production |
| **Never log values** | Credential values must never appear in application logs |
| **Vault in production** | Prefer `VaultCredentialBackend` over local file storage for shared deployments |
| **Machine entropy** | Set `JDAI_KEY_ENTROPY` on Linux/macOS to add user-controlled entropy to AES key derivation |
| **File permissions** | `~/.jdai/credentials/` is created with mode `700`; do not relax permissions |
| **Short-lived tokens** | Use Vault's token TTL and renewal to limit exposure window |

---

## See also

- [Custom Providers](custom-providers.md) — how providers consume credentials
- [Architecture Overview](index.md) — system architecture
- [Gateway API](gateway-api.md) — credential handling for the HTTP gateway
