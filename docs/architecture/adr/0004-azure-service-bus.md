# ADR 0004 — Azure Service Bus over Azure Storage Queues

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-02-23 |
| **Deciders** | Engineering team |

## Context

The Outbox pattern requires a reliable message broker to fan-out notification events from the application to one or more consumers (initially email; in-app notifications in v2). Two Azure-native options were considered: **Azure Storage Queues** and **Azure Service Bus**.

## Decision

We use **Azure Service Bus** with a **Topic and Subscription** model.

The `notifications` topic has a subscription `email` consumed by `SendEmailFunction`. Adding future notification channels (e.g., `in-app`, `push`) requires only creating a new subscription and deploying a new function — the publisher is unchanged.

## Comparison

| Feature | Azure Storage Queues | Azure Service Bus |
|---|---|---|
| Max message size | 64 KB | 256 KB (Standard), 100 MB (Premium) |
| Message ordering | No guarantee | Sessions support (FIFO) |
| Dead-letter queue | Not built-in (manual) | **Built-in per subscription** |
| Topics / fan-out | ❌ No | **✅ Yes** |
| Duplicate detection | ❌ No | ✅ Yes (configurable window) |
| Max delivery count / retry | Limited (5 dequeues visible) | **Configurable (e.g., 10)** |
| Message lock / competing consumers | Basic | Full |
| At-least-once guarantee | Yes | Yes |
| Cost | Lower | Slightly higher (Standard tier) |
| Demo / training value | Low | **High** |

## Alternatives Considered

| Option | Reason not chosen |
|---|---|
| Azure Storage Queues | No Topics (cannot fan-out to multiple subscribers without duplicating publish logic); no built-in DLQ |
| Azure Event Hubs | Designed for high-throughput streaming / telemetry, not transactional messaging; complex consumer model for this use case |
| Azure Event Grid | Push-based, not pull-based; requires public HTTPS endpoints for subscribers; poor fit for Azure Functions outbox polling |

## Consequences

### Positive
- Topics + Subscriptions provide a natural pub/sub model: publish once, deliver to N subscribers.
- Built-in dead-letter queue enables operational observability for failed deliveries without custom infrastructure.
- Configurable `MaxDeliveryCount` per subscription controls retry behaviour before dead-lettering.
- Native Azure Functions `ServiceBusTrigger` binding eliminates polling boilerplate.
- Adding a second notification channel in v2 requires zero changes to the publisher.

### Negative / Trade-offs
- Azure Service Bus Standard tier has a cost (approximately $0.05 per million operations); negligible at training scale.
- Slightly more complex local development setup than Storage Queues (no full local emulator; use connection string to a shared dev namespace or the `ServiceBusClient` with a local emulator in Docker).
- Requires provisioning a Service Bus namespace in Bicep (added to `infra/bicep/modules/`).
