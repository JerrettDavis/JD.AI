# ADR-010: Enterprise Connector SDK

**Status**: Accepted  
**Date**: 2025-07  
**Deciders**: Core Team  
**Issue**: #210

---

## Context

JD.AI agents frequently need to interact with external enterprise systems: issue trackers, project management tools, CI/CD platforms, and ITSM systems. Without a standard integration model, each connector becomes a bespoke plugin with no consistent discovery, configuration, or tool-scoping mechanism.

The requirements are:

1. Third-party developers should be able to author connectors in separate assemblies.
2. The runtime should discover connectors via attribute decoration — not manual registration.
3. Each connector declares its own authentication strategy and tool plugins.
4. Tool loadouts allow scoping which tools an agent can use (e.g., read-only vs full access).
5. A reference implementation should validate the SDK design end-to-end.

---

## Decision

### SDK Package (`JD.AI.Connectors.Sdk`)

A standalone SDK package with minimal dependencies houses the connector contract:

- `[JdAiConnector]` attribute — marks a class as a connector; carries `name`, `displayName`, `version`, and optional `Description` / `Homepage`.
- `IConnector` — single-method interface (`Configure(IConnectorBuilder)`).
- `IConnectorBuilder` — fluent API for registering auth providers, tool plugins, and loadouts.
- `IConnectorAuthProvider` — async interface for retrieving authorization headers.
- `ConnectorDescriptor` — descriptor record produced at registration time.

### Runtime Support (`ConnectorRegistry`)

`ConnectorRegistry` (in the SDK) provides:

- `Register(IConnector, IServiceCollection)` — validates the attribute, runs `Configure`, returns a `ConnectorDescriptor`.
- `ScanAndRegister(assemblies, services)` — scans exported types for `[JdAiConnector]` and registers all found connectors.
- `Get(name)` — case-insensitive lookup.
- `SetEnabled(name, bool)` — toggle connector activation at runtime.

### Authentication

`IConnectorAuthProvider` returns a raw `Authorization` header value. This is intentionally simple: it does not prescribe OAuth token refresh, secret rotation, or credential scoping. Those concerns belong to the credential and security layers already present in `JD.AI.Core`.

### Tool Loadouts

Loadouts are named `Func<string, bool>` predicates registered via `IConnectorBuilder.AddLoadout`. A loadout filters available tool names to a permitted subset. Example: a `jira-readonly` loadout permits only `get_*` and `search_*` functions.

Loadouts allow operators to give agents scoped access without authoring separate connectors.

### Jira Reference Implementation (`JD.AI.Connectors.Jira`)

`JiraConnector` demonstrates the complete pattern:

- Decorated with `[JdAiConnector("jira", "Atlassian Jira", "1.0.0")]`.
- Registers `JiraApiKeyAuthProvider` (Basic auth with email + API token).
- Registers `JiraIssueTool` with three `[KernelFunction]` operations: `jira_get_issue`, `jira_search_issues`, `jira_create_issue`.
- Registers a `jira-readonly` loadout that permits only get/search operations.

---

## Alternatives Considered

### Embed connector types directly in `JD.AI.Core`

Rejected. Mixing enterprise connector implementations into the core library creates coupling, inflates the core package size, and prevents connectors from being shipped independently.

### Use MEF or System.Composition for discovery

Rejected. MEF adds a significant dependency and export/import ceremony. The `[JdAiConnector]` attribute with a simple assembly scanner achieves the same goal with no additional framework dependency.

### Define loadouts as permission sets (enum flags)

Rejected. String-predicate loadouts are more flexible — a connector author can express any filtering logic without being constrained to a fixed permission model.

---

## Consequences

- **Positive**: Any team can author a connector in an independent NuGet package and integrate with JD.AI by implementing one interface.
- **Positive**: Tool loadouts allow fine-grained agent scoping without custom code.
- **Positive**: `ConnectorRegistry.ScanAndRegister` enables zero-touch startup registration.
- **Neutral**: Each connector package carries its own auth implementation; shared OAuth helpers may be extracted later into `JD.AI.Connectors.OAuth`.
- **Negative**: The first-class `HttpClient` dependency in `JiraIssueTool` is not managed by the SDK. Connector authors are responsible for HttpClient lifetime and pooling.
