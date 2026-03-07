---
title: Authoring UPSS Specifications
description: "How to create, validate, and maintain UPSS specifications — common structure, lifecycle, validation, and best practices."
---

# Authoring UPSS Specifications

This guide covers how to create and maintain UPSS specifications. All 18 spec types share a common structure and lifecycle.

## Common spec structure

Every UPSS spec follows this YAML shape:

```yaml
apiVersion: jdai.upss/v1
kind: <SpecType>                    # Vision, Behavior, Architecture, etc.
id: <type>.<hyphenated-name>        # e.g., behavior.validate-pull-request
version: 1                          # Integer, incremented on revision
status: draft                       # draft | active | deprecated | retired
metadata:
  owners:
    - GitHubUsername
  reviewers:
    - upss-agent-role
  lastReviewed: 2026-03-07
  changeReason: Initial specification.

# ... type-specific fields ...

trace:
  upstream:
    - specs/vision/examples/vision.example.yaml
  downstream:
    testing:
      - tests/path/to/test.cs
    code:
      - src/path/to/implementation.cs
```

### Required fields (all types)

| Field | Type | Description |
|-------|------|-------------|
| `apiVersion` | string | Always `jdai.upss/v1` |
| `kind` | string | The spec type (e.g., `Behavior`, `Architecture`) |
| `id` | string | Unique identifier matching `<type>.<name>` pattern |
| `version` | integer | Revision number, must be >= 1 |
| `status` | string | Lifecycle status (see below) |
| `metadata` | object | Ownership and review tracking |

### Metadata fields

| Field | Type | Description |
|-------|------|-------------|
| `owners` | string[] | GitHub usernames responsible for this spec |
| `reviewers` | string[] | Usernames or agent roles that review changes |
| `lastReviewed` | string | ISO date of last review (YYYY-MM-DD) |
| `changeReason` | string | Why this version exists |

### ID conventions

IDs follow the pattern `<type>.<hyphenated-lowercase-name>`:

```
vision.jdai-product
capability.spec-validation
usecase.validate-pull-request
behavior.validate-pull-request
architecture.core-system
domain.specification-entities
security.oauth2-integration
policy.data-classification
```

Each spec type enforces its own ID prefix via regex (e.g., `^behavior\.[a-z0-9]+(?:[.-][a-z0-9]+)*$`).

## Spec lifecycle

```
draft  ──►  active  ──►  deprecated  ──►  retired
```

| Status | Meaning |
|--------|---------|
| `draft` | Work in progress. May be incomplete or under review. |
| `active` | Approved and in use. Implementation should conform to this spec. |
| `deprecated` | Superseded or no longer recommended. Still referenced but should not be used for new work. |
| `retired` | Completely obsolete. May be removed in a future cleanup. |

## Creating a new spec

### Step 1: Choose the spec type

Identify which of the [18 spec types](catalog.md) fits your need. If you're describing how a feature works, that's a behavior spec. If you're defining API contracts, that's an interface spec.

### Step 2: Create the YAML file

Create a new YAML file under the appropriate spec directory:

```
specs/<type>/examples/<type>.<name>.yaml
```

Use the existing `<type>.example.yaml` in that directory as a template.

### Step 3: Update the index

Add an entry to `specs/<type>/index.yaml`:

```yaml
entries:
  - id: <type>.<name>
    title: Human-Readable Title
    path: specs/<type>/examples/<type>.<name>.yaml
    status: draft
```

### Step 4: Set up traceability

Link your spec to upstream dependencies and downstream artifacts:

```yaml
trace:
  upstream:
    - specs/vision/examples/vision.example.yaml    # What drives this spec
  downstream:
    testing:
      - tests/JD.AI.Tests/path/to/test.cs           # Tests that verify it
    code:
      - src/JD.AI.Core/path/to/implementation.cs     # Code that implements it
```

All referenced files must exist in the repository. The validator checks this.

### Step 5: Validate

Run the repository validation tests:

```bash
dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~SpecificationRepository"
```

Fix any errors before committing.

## Validation rules

### Document-level validation

Each validator checks rules specific to its spec type. Common rules include:

- **ID format** — Must match `<type>.<name>` with lowercase alphanumeric segments
- **Version** — Must be >= 1
- **Status** — Must be one of: `draft`, `active`, `deprecated`, `retired`
- **Non-empty required fields** — Titles, descriptions, and arrays must not be blank or empty
- **Enumeration constraints** — Values like `architectureStyle`, `authnModel`, or `severity` must be from allowed sets
- **Referential integrity** — Cross-references (e.g., `useCaseRef` in behavior specs) must exist in their respective indexes

### Repository-level validation

The `ValidateRepository(repoRoot)` method checks:

- Every entry in `index.yaml` points to a file that exists
- Each file parses without error
- The parsed spec's `id` and `status` match the index entry
- All `trace.upstream` and `trace.downstream.*` file paths exist on disk
- Cross-references to other spec types resolve

### Running validation

```bash
# All specification tests
dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~Specification"

# Specific spec type
dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~BehaviorSpecification"

# Just repository validation (cross-references and file links)
dotnet test tests/JD.AI.Tests --filter "FullyQualifiedName~RepositoryTests"
```

## Traceability best practices

### Link to stable artifacts

Only reference files that exist and are committed. Don't link to planned files.

### Keep upstream minimal

Link to the spec that directly motivated this one, not every transitive ancestor.

### Keep downstream accurate

Update downstream links when you move or rename implementation files.

### Use relative paths

All trace paths are relative to the repository root:

```yaml
trace:
  upstream:
    - specs/usecases/examples/usecases.example.yaml    # Relative to repo root
  downstream:
    code:
      - src/JD.AI.Core/Specifications/BehaviorSpecification.cs
```

## Extending UPSS

To add a new spec type:

1. Create the directory structure: `specs/<type>/README.md`, `schema/<type>.schema.json`, `examples/<type>.example.yaml`, `index.yaml`
2. Define the JSON Schema with `"additionalProperties": false` on all objects
3. Create the C# model, parser, and validator in `src/JD.AI.Core/Specifications/<Type>Specification.cs`
4. Add repository tests in `tests/JD.AI.Tests/Specifications/<Type>SpecificationRepositoryTests.cs`
5. Add validator tests in `tests/JD.AI.Tests/Specifications/<Type>SpecificationValidatorTests.cs`

Follow the existing `BehaviorSpecification` as the canonical template.

## Next steps

- [UPSS Overview](index.md) — What UPSS is and why it exists
- [Specification Catalog](catalog.md) — Complete reference for all 18 spec types
