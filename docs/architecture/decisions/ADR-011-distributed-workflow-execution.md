# ADR-011: Distributed Workflow Execution

**Status**: Accepted  
**Date**: 2025-07  
**Deciders**: Core Team  
**Issue**: #208

---

## Context

JD.AI workflow execution is currently in-process — each workflow runs inside the agent's request cycle. This works well for short, latency-sensitive workflows but becomes a bottleneck for long-running, CPU-intensive, or parallel agent workloads.

Distributing workflow execution across multiple worker processes enables:

- Horizontal scaling of compute-intensive workflows
- Isolation of long-running workflows from the interactive agent loop
- Resilience through message broker durability and retry semantics
- Graceful degradation with dead-letter handling and observability

---

## Decision

### Core Abstractions

A dedicated library (`JD.AI.Workflows.Distributed`) defines the distribution contract:

- `WorkflowWorkItem` — serializable work item with identity, context, correlation ID, priority, and delivery tracking
- `IWorkflowDispatcher` — single-method interface for enqueuing work items
- `IWorkflowWorker` — single-method interface for processing work items
- `IDeadLetterSink` — records items that cannot be processed after max delivery attempts

### Transport Implementations

Three transports are provided, all implementing `IWorkflowDispatcher` + paired `IHostedService` worker:

**In-Memory** (`JD.AI.Workflows.Distributed.InMemory`)
- Backed by `System.Threading.Channels.Channel<T>` (bounded)
- `InMemoryDeadLetterSink` retains items in-process for inspection
- Suitable for single-process deployments, local development, and testing

**Redis Streams** (`JD.AI.Workflows.Distributed.Redis`)
- Uses StackExchange.Redis consumer groups (`XREADGROUP`) for competing consumer semantics
- Stream: `jdai:workflows`; DLQ: `jdai:workflows:dlq` (Redis List)
- Polling-based read with configurable timeout
- Failed items exceeding `MaxDeliveryCount` are dead-lettered and ACKed

**Azure Service Bus** (`JD.AI.Workflows.Distributed.AzureServiceBus`)
- Uses `ServiceBusProcessor` with `AutoCompleteMessages = false`
- Complete on success, abandon on transient, dead-letter on permanent/unhandled
- Configurable max concurrent calls for throughput control

### Dead-Letter Strategy

Items are dead-lettered when:
1. `DeliveryCount` exceeds `MaxDeliveryCount` (default 5)
2. The worker returns `WorkItemResult.Permanent`
3. An unhandled exception escapes the worker

Dead-lettered items are always ACKed from the main queue to prevent poison-message loops.

### Correlation ID and Tracing

`WorkflowWorkItem.CorrelationId` carries a distributed trace identifier. Future OpenTelemetry integration can extract this via Activity enrichment in the worker layer. The field is propagated through all transports as a first-class property (e.g., `CorrelationId` on Azure Service Bus messages).

---

## Alternatives Considered

### Use WorkflowFramework's existing scheduling extensions

`WorkflowFramework.Extensions.Scheduling` provides cron and delayed execution within a single process. It does not address cross-process message-passing or competing consumer semantics needed for multi-replica deployments.

### Use MassTransit as the abstraction layer

MassTransit provides a mature message broker abstraction. However, it introduces a significant additional dependency and learning surface. The JD.AI implementation surface is narrow (`IWorkflowDispatcher` / `IWorkflowWorker`) and can be mapped to MassTransit or NServiceBus in the future if needed.

### NATS transport

NATS JetStream is an excellent fit for this pattern. Deferred to a follow-up issue once the Redis and ASB implementations are validated in production.

---

## Consequences

- **Positive**: Stateless worker design — multiple replicas consume the same queue without coordination
- **Positive**: In-memory transport makes tests fast and deterministic with no external dependencies
- **Positive**: Redis and ASB implementations follow the same contract — switching is a DI registration change
- **Neutral**: `WorkflowWorkItem.InitialContext` is `IDictionary<string, string>` — structured context (nested objects) requires callers to serialize values to strings
- **Negative**: Redis polling (vs blocking `XREAD`) introduces up to `ReadBlockTimeout` latency between dispatch and processing
- **Negative**: Dead-lettered items in Redis are stored as JSON in a List; no built-in TTL or rotation
