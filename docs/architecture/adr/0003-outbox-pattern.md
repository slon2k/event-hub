# ADR 0003 — Outbox Pattern for Reliable Messaging

| | |
| --- | --- |
| **Status** | Accepted |
| **Date** | 2026-02-23 |
| **Deciders** | Engineering team |

## Context

When an invitation is created, two things must happen:

1. The `Invitation` row is saved to the database.
2. A notification message is published to Azure Service Bus so that an email can be sent.

If these two operations are performed independently, a **dual-write** problem exists:

- If the DB write succeeds but the Service Bus publish fails → invitation exists but no email is ever sent.
- If the Service Bus publish succeeds but the DB write fails → email is sent for an invitation that does not exist.

Neither scenario is acceptable. Distributed transactions (two-phase commit) across SQL and Service Bus are not supported and would be prohibitively complex.

## Decision

We implement the **Transactional Outbox Pattern with a wake-up queue**:

1. When a domain event is raised (e.g., `InvitationSent`), the Application layer handler writes an `OutboxMessage` row to the **same SQL database** as the domain change, inside the **same EF Core transaction**.
2. After the transaction commits, `EventHubDbContext` calls `IOutboxNotifier.NotifyAsync()`, which sends a lightweight ping to the `outbox-trigger` Service Bus queue. This wakes the `ProcessOutboxOnDemand` Azure Function immediately.
3. `ProcessOutboxOnDemand` (ServiceBusTrigger) reads unpublished rows, publishes them to the `notifications` topic, and marks them published.
4. A `ProcessOutboxFallback` timer function (every 2 hours) acts as a safety net for any pings lost due to API restarts or transient failures.
5. The `IOutboxNotifier` is optional — if `ServiceBusConnectionString` is not configured, the notifier is not registered and the DB context skips the ping gracefully.

The database remains the single source of truth. The wake-up ping is best-effort; if it is lost, the fallback timer guarantees eventual processing.

```text
BEGIN TRANSACTION
  INSERT INTO Invitations (...)
  INSERT INTO OutboxMessages (Type='InvitationSent', Payload='{...}', PublishedAt=NULL)
COMMIT

-- immediately after commit --
ServiceBus.Send(queue: 'outbox-trigger', message: 'ping')  ← best-effort, swallows failures

-- on ping received (or every 2h fallback) --
SELECT * FROM OutboxMessages WHERE PublishedAt IS NULL
→ Publish to Service Bus topic 'notifications'
→ UPDATE OutboxMessages SET PublishedAt = NOW()
```

## Alternatives Considered

| Option | Reason not chosen |
| --- | --- |
| Publish directly in command handler | Dual-write problem — no atomicity guarantee across SQL and Service Bus |
| Sagas / distributed transactions | Extreme complexity for the problem size; no supported infrastructure path |
| Change Data Capture (CDC) | Requires SQL Server CDC or Debezium; unnecessary operational overhead for this scale |
| Event Sourcing | Fundamental model change; significant complexity that is not justified by the requirements |

## Consequences

### Positive

- **Atomicity**: domain change and message intent are committed together — no lost messages, no phantom messages.
- **Resilience**: if Service Bus is temporarily unavailable, the outbox acts as a buffer; the Timer retries on the next poll.
- **Simplicity**: the mechanism is plain SQL — no additional infrastructure components.
- **Observability**: unpublished or failed outbox rows are immediately visible in the database.

### Negative / Trade-offs

- **Eventual consistency**: email delivery is near-instantaneous in the happy path (ping → on-demand trigger) but can be delayed up to 2 hours if the ping is lost and the fallback timer fires.
- **At-least-once delivery**: if `ProcessOutboxOnDemand` crashes after publishing but before marking the row, the message will be published again on the next run (ping or fallback timer). Consumers (`SendEmailFunction`) must tolerate duplicate messages. Service Bus duplicate detection on the `notifications` topic mitigates this via `MessageId = outbox.Id`.
- **Minimal polling overhead**: the fallback timer query runs every 2 hours and only touches the DB once if there is nothing to process. Constant polling is eliminated.
- **Schema coupling**: the `OutboxMessage` table resides in the application database, coupling the outbox lifecycle to the application schema.
