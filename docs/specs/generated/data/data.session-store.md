# Session Store Data Model

- **Type:** `data`
- **Kind:** `DataIndex`
- **ID:** `data.session-store`
- **Status:** `draft`
- **Source:** `specs/data/examples/data.example.yaml`

## YAML

```yaml
apiVersion: jdai.upss/v1
kind: Data
id: data.session-store
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-data-spec-architect
  lastReviewed: 2026-03-07
  changeReason: Establish the first canonical data specification for session persistence.
modelType: relational
schemas:
  - name: Sessions
    description: Tracks active user sessions with expiration metadata.
    fields:
      - Id (uniqueidentifier, PK)
      - UserId (nvarchar(256), NOT NULL)
      - Token (nvarchar(512), NOT NULL)
      - CreatedAt (datetimeoffset, NOT NULL)
      - ExpiresAt (datetimeoffset, NOT NULL)
migrations:
  - version: "1.0.0"
    description: Create Sessions table with primary key and expiration columns.
    reversible: true
indexes:
  - name: IX_Sessions_UserId
    table: Sessions
    columns:
      - UserId
  - name: IX_Sessions_ExpiresAt
    table: Sessions
    columns:
      - ExpiresAt
constraints:
  - ExpiresAt must be greater than CreatedAt.
  - Token values must be unique across active sessions.
trace:
  upstream:
    - specs/behavior/examples/behavior.example.yaml
  downstream:
    deployment: []
    operations: []
    testing:
      - tests/JD.AI.Tests/Specifications/DataSpecificationRepositoryTests.cs
```
