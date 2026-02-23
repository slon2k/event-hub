# ADR 0003 — Outbox Pattern for Reliable Messaging

| | |
|---|---|
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

We implement the **Transactional Outbox Pattern**:

1. When a domain event is raised (e.g., `InvitationSent`), the Application layer handler writes an `OutboxMessage` row to the **same SQL database** as the domain change, inside the **same EF Core transaction**.
2. A separate process (`ProcessOutboxFunction`, Azure Functions Timer trigger) periodically reads unpublished rows, publishes them to Service Bus, and marks them as published.

The database is the single source of truth. The message cannot be lost as long as the database transaction succeeds.

```
BEGIN TRANSACTION
  INSERT INTO Invitations (...)
  INSERT INTO OutboxMessages (Type='InvitationSent', Payload='{...}', PublishedAt=NULL)
COMMIT

-- later, independently --
SELECT * FROM OutboxMessages WHERE PublishedAt IS NULL
→ Publish to Service Bus
→ UPDATE OutboxMessages SET PublishedAt = NOW()
```

## Alternatives Considered

| Option | Reason not chosen |
|---|---|
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
- **Eventual consistency**: email delivery is delayed by up to one Timer interval (10s by default) after the API response.
- **At-least-once delivery**: if `ProcessOutboxFunction` crashes after publishing but before marking the row, the message will be published again on the next run. Consumers (`SendEmailFunction`) must tolerate duplicate messages.
- **Polling overhead**: the Timer query runs every 10 seconds regardless of load — negligible at this scale but worth noting.
- **Schema coupling**: the `OutboxMessage` table resides in the application database, coupling the outbox lifecycle to the application schema.
