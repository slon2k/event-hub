# Notification Flow

| | |
| --- |
| **Status** | Draft |
| **Date** | 2026-02-23 |
| **Updated** | 2026-03-04 |
| **Version** | 0.2 |

---

## 1. Overview

Notifications are delivered **asynchronously** using the Outbox pattern combined with Azure Service Bus and Azure Functions. This decouples email delivery from the API request lifecycle and ensures **at-least-once, reliable delivery** without risking dual-write inconsistencies.

---

## 2. Components

| Component | Technology | Role |
| --- | --- | --- |
| API Command Handler | MediatR, EF Core | Writes domain change + OutboxMessage in one transaction, then sends a wake-up ping |
| `IOutboxNotifier` / `ServiceBusOutboxNotifier` | `EventHub.Infrastructure` | Sends a lightweight ping to `outbox-trigger` queue after each successful domain save |
| `OutboxMessage` table | Azure SQL | Durable staging area for unpublished events |
| `outbox-trigger` queue | Azure Service Bus | Wake-up channel; receives ping from API; triggers `ProcessOutboxOnDemand` |
| `ProcessOutboxOnDemand` | Azure Functions — ServiceBusTrigger | Primary: fires instantly when a ping arrives, processes outbox on demand |
| `ProcessOutboxFallback` | Azure Functions — TimerTrigger (every 2h) | Safety net: catches any pings lost during API restarts or transient errors |
| Service Bus Topic `notifications` | Azure Service Bus | Fan-out pub/sub hub |
| Subscription `email` | Azure Service Bus | Filter for email delivery |
| `SendEmailFunction` | Azure Functions — ServiceBusTrigger | Reads from subscription, delivers via ACS Email |
| Azure Communication Services Email | ACS | Actual email delivery to recipient |

---

## 3. Sequence — Invitation Sent

```text
 Client          API Handler         DB (SQL)         OutboxFunction    Service Bus      EmailFunction     ACS Email
   │                 │                  │                   │                │                 │               │
   │  POST           │                  │                   │                │                 │               │
   │ /invitations ──>│                  │                   │                │                 │               │
   │                 │ BEGIN TRANSACTION│                   │                │                 │               │
   │                 │──────────────────>                   │                │                 │               │
   │                 │ INSERT Invitation│                   │                │                 │               │
   │                 │──────────────────>                   │                │                 │               │
   │                 │ INSERT OutboxMsg │                   │                │                 │               │
   │                 │ (InvitationSent) │                   │                │                 │               │
   │                 │──────────────────>                   │                │                 │               │
   │                 │ COMMIT           │                   │                │                 │               │
   │                 │──────────────────>                   │                │                 │               │
   │  201 Created <──│                  │                   │                │                 │               │
   │                 │                  │                   │                │                 │               │
   │                 │                  │   Timer fires     │                │                 │               │
   │                 │                  │  (every 10s)      │                │                 │               │
   │                 │                  │<──────────────────│                │                 │               │
   │                 │                  │ SELECT unpublished│                │                 │               │
   │                 │                  │──────────────────>│                │                 │               │
   │                 │                  │                   │ Publish msg    │                 │               │
   │                 │                  │                   │───────────────>│                 │               │
   │                 │                  │ UPDATE PublishedAt│                │                 │               │
   │                 │                  │<──────────────────│                │                 │               │
   │                 │                  │                   │  ServiceBus    │                 │               │
   │                 │                  │                   │  triggers ─────┼────────────────>│               │
   │                 │                  │                   │                │                 │ Send email    │
   │                 │                  │                   │                │                 │──────────────>│
   │                 │                  │                   │                │                 │  Delivered <──│
   │                 │                  │                   │                │  Complete msg <─│               │
```

---

## 4. Sequence — Event Cancelled

The flow is identical structurally, but:

- The domain event is `EventCancelled`, raised in `Event.Cancel()`.
- The `OutboxMessage` payload contains all affected participant emails.
- The `SendEmailFunction` sends one email per recipient (fan-out inside the function).

```text
 Client          API Handler         DB (SQL)         outbox-trigger   OutboxFunction    Service Bus      EmailFunction     ACS Email
   │  DELETE /events/{id} ──> │         │                  queue              │                │                 │               │
   │                 │ BEGIN TRANSACTION                    │                 │                │                 │               │
   │                 │ UPDATE Event → Cancelled             │                 │                │                 │               │
   │                 │ INSERT OutboxMsg (EventCancelled)    │                 │                │                 │               │
   │                 │ COMMIT                               │                 │                │                 │               │
   │                 │   Send ping ─────────────────────────>                 │                │                 │               │
   │  204 No Content <── │               │                  │  (same ServiceBus/outbox flow as above) ...       │               │
```

---

## 5. Outbox Message Schema

Messages are serialized as JSON and stored in the `OutboxMessages` table.

### `InvitationSent` payload example

```json
{
  "type": "InvitationSent",
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventTitle": "Team Kickoff 2026",
  "eventDateTime": "2026-03-15T09:00:00+00:00",
  "eventLocation": "Berlin, Germany",
  "invitationId": "7cb2e4a1-0011-4a2d-bcde-000000000001",
  "participantEmail": "alice@example.com",
  "rsvpToken": "<raw-signed-token>",
  "tokenExpiresAt": "2026-03-18T09:00:00+00:00"
}
```

> The `rsvpToken` is the **raw token** — it is included in the outbox message so that `SendEmailFunction` can embed it in the RSVP link. It is **never stored in the database** (only its hash is stored on the `Invitation` row).

### `EventCancelled` payload example

```json
{
  "type": "EventCancelled",
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventTitle": "Team Kickoff 2026",
  "eventDateTime": "2026-03-15T09:00:00+00:00",
  "affectedParticipantEmails": [
    "alice@example.com",
    "bob@example.com"
  ]
}
```

---

## 6. Service Bus Topology

```text
Topic: notifications
  │
  ├── Subscription: email
  │       Filter: all messages (no filter)
  │       → handled by SendEmailFunction
  │
  └── Subscription: in-app  (future v2)
          Filter: all messages
          → handled by InAppNotificationFunction
```

Adding a new notification channel in v2 requires only:

1. Creating a new subscription on the existing topic.
2. Deploying a new Azure Function triggered by that subscription.
The publisher (`ProcessOutboxFunction`) does not change.

---

## 7. Error Handling and Resilience

| Failure Scenario | Behaviour |
| --- | --- |
| API crashes after COMMIT but before ping | OutboxMessage remains unpublished; fallback timer (every 2h) picks it up |
| Ping send fails (transient Service Bus error) | `ServiceBusOutboxNotifier` swallows the exception and logs a warning; domain transaction is not rolled back; fallback timer covers the gap |
| `ProcessOutboxOnDemand` fails to publish | Row stays unpublished; fallback timer retries on next 2h tick |
| `SendEmailFunction` throws | Service Bus retries delivery up to configured `MaxDeliveryCount` |
| Retries exhausted | Message moved to **Dead-Letter Queue** (`notifications/subscriptions/email/$deadletterqueue`) |
| ACS Email rejects (invalid address) | `SendEmailFunction` catches the error, logs it, and completes the message (no DLQ for undeliverable addresses) |

---

## 8. Idempotency

- `ProcessOutboxFunction` checks `PublishedAt IS NULL` before publishing to avoid double-publishing on overlapping timer runs.
- `SendEmailFunction` is idempotent for invitations: the same `InvitationId` will result in a duplicate email if the message is redelivered, but Service Bus's `MaxDeliveryCount` limits this to the configured retry window. Full idempotency (deduplication by `InvitationId`) can be added in v2.

---

## 9. Configuration

| Setting | Location | Example Value |
| --- | --- | --- |
| `ServiceBusConnectionString` | API app settings / Key Vault | `Endpoint=sb://...` |
| `ServiceBus__OutboxTriggerQueueName` | API app settings | `outbox-trigger` |
| `ServiceBusConnectionString` | Functions config / Key Vault | `Endpoint=sb://...` |
| `ServiceBusTopicName` | Functions config | `notifications` |
| `ServiceBusSubscriptionName` | Functions config | `email` |
| `OutboxTriggerQueueName` | Functions config | `outbox-trigger` |
| `OutboxTimerCronExpression` | Functions config | `0 0 */2 * * *` (every 2 hours — fallback only) |
| `AcsEmail:ConnectionString` | Functions config / Key Vault | `endpoint=https://...` |
| `AcsEmail:SenderAddress` | Functions config | `noreply@eventhub.example.com` |
