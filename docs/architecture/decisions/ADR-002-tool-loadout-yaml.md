# ADR-002 — Tool Loadout YAML Format and File-Based Registry

**Date:** 2025-01-01  
**Status:** Accepted  
**Issue:** [#214](https://github.com/jd-ai/jd-ai/issues/214)

---

## Context

JD.AI ships five built-in tool loadouts (`minimal`, `developer`, `research`, `devops`, `full`) hard-coded in `ToolLoadoutRegistry`. Users and teams need the ability to define their own loadouts for custom workflows without modifying application source code or recompiling.

Loadouts must support inheritance chains (a custom loadout extending a built-in one), category/plugin include/exclude rules, and discoverable-plugin patterns — features already present in the built-in registry.

## Decision

We adopt a **YAML file format** for user-defined loadouts and a **file-based registry** that discovers `*.loadout.yaml` files from configured search paths. The existing `ToolLoadoutRegistry` is wrapped in a `CompositeToolLoadoutRegistry` so that file-based loadouts overlay the built-ins without modifying any existing code.

### 1. YAML Schema

```yaml
name: my-custom-loadout        # required; unique identifier
parent: developer              # optional; inherits from this loadout
includeCategories:             # ToolCategory enum values (case-insensitive)
  - Git
  - Search
includePlugins:                # exact plugin names (case-insensitive)
  - myCustomPlugin
excludePlugins:                # explicitly disabled plugins
  - tailscale
discoverablePatterns:          # glob-style patterns for lazy discovery
  - docker*
```

Fields map to `ToolLoadout` properties via a `LoadoutYamlDto` intermediate object. An internal `ToolLoadoutYamlSerializer` handles serialisation and deserialisation using **YamlDotNet** (already a project dependency) with `CamelCaseNamingConvention`.

### 2. New Types

| Type | Responsibility |
|---|---|
| `ToolLoadoutYamlSerializer` | YAML ↔ `ToolLoadout` conversion via DTO |
| `LoadoutYamlDto` | Internal YAML data-transfer object |
| `FileToolLoadoutRegistry` | Scans directories for `*.loadout.yaml`; implements `IToolLoadoutRegistry` |
| `CompositeToolLoadoutRegistry` | Chains multiple registries; primary wins on name conflict |
| `LoadoutValidator` | Validates name, parent existence, include/exclude conflicts, circular inheritance |
| `LoadoutResolutionHelper` | Shared active/discoverable plugin resolution logic reused across registries |

### 3. DI Registration

`GovernanceInitializer` creates a `CompositeToolLoadoutRegistry` wrapping:
1. `FileToolLoadoutRegistry` scanning `~/.jdai/loadouts/` and `./loadouts/`
2. The existing `ToolLoadoutRegistry` (built-ins) as fallback

This means file-based loadouts can shadow built-in ones by name if desired.

### 4. Cross-Registry Inheritance

`CompositeToolLoadoutRegistry.ResolveActivePlugins` merges all loadouts from all registries into a single dictionary before resolving the inheritance chain. This allows a YAML loadout to inherit from a built-in loadout (e.g. `parent: developer`) even though the two live in separate registry instances.

## Consequences

**Positive:**
- Users can define custom loadouts without recompiling.
- Built-in loadouts are unchanged and continue working.
- Inheritance from built-ins works transparently.
- Validation catches common configuration mistakes (cycles, conflicts) early.
- YamlDotNet was already a dependency — no new packages required.

**Negative / Trade-offs:**
- File-based registry loads once at startup; hot-reload requires a process restart.
- Invalid YAML files are silently skipped (log line recommended in production).
- `excludeCategories` from the original spec is not supported by `ToolLoadout`; omitted.

## Alternatives Considered

- **JSON format** — rejected in favour of YAML for readability and comment support.
- **Modifying `ToolLoadoutRegistry`** — rejected to preserve the existing public surface and avoid breaking changes.
- **Database-backed registry** — out of scope for this iteration; can be added as an additional `IToolLoadoutRegistry` implementation later.
