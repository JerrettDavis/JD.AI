# Agent Chat API

- **Type:** `interfaces`
- **Kind:** `InterfaceIndex`
- **ID:** `interface.agent-chat-api`
- **Status:** `draft`
- **Source:** `specs/interfaces/examples/interfaces.example.yaml`

## YAML

```yaml
apiVersion: jdai.upss/v1
kind: Interface
id: interface.agent-chat-api
version: 1
status: draft
metadata:
  owners:
    - JerrettDavis
  reviewers:
    - upss-interface-contract-architect
  lastReviewed: 2026-03-07
  changeReason: Establish the first canonical interface specification for agent chat interactions.
interfaceType: rest
operations:
  - name: SendMessage
    method: POST
    path: /api/chat/messages
    description: Sends a user message to the agent and returns the assistant response.
  - name: ListConversations
    method: GET
    path: /api/chat/conversations
    description: Lists all active conversations for the authenticated user.
messageSchemas:
  - name: ChatMessageRequest
    format: application/json
    description: Payload containing the user message text, conversation id, and optional parameters.
  - name: ChatMessageResponse
    format: application/json
    description: Payload containing the assistant response text, message id, and token usage.
compatibilityRules:
  - Existing operation paths must not be removed or renamed without a deprecation period.
  - New required request fields must provide default values for backward compatibility.
trace:
  upstream:
    - specs/usecases/examples/usecases.example.yaml
  downstream:
    code:
      - src/JD.AI.Core/Specifications/InterfaceSpecification.cs
    testing:
      - tests/JD.AI.Tests/Specifications/InterfaceSpecificationRepositoryTests.cs
    deployment: []
```
