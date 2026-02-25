# Domain Model

| | |
|---|---|
| **Status** | Draft |
| **Date** | 2026-02-23 |
| **Version** | 0.1 |

---

## 1. Aggregates and Entities

### 1.1 `Event` (Aggregate Root)

Represents a scheduled occurrence that an Organizer creates and manages.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Title` | `string` | Required, max 200 chars |
| `Description` | `string?` | Optional, max 2000 chars |
| `DateTime` | `DateTimeOffset` | Must be in the future at creation |
| `Location` | `string?` | Optional, max 500 chars |
| `Capacity` | `int?` | `null` = unlimited |
| `Status` | `EventStatus` | See §3 |
| `OrganizerId` | `string` | Entra ID Object ID of the organizer |
| `CreatedAt` | `DateTimeOffset` | Set on creation, immutable |
| `UpdatedAt` | `DateTimeOffset` | Updated on any change |
| `Invitations` | `IReadOnlyCollection<Invitation>` | Navigation — owned by this aggregate |

**Invariants:**
- `DateTime` must be in the future when creating.
- Invitations can only be added when status is `Published`.
- Transitioning to `Cancelled` raises an `EventCancelled` domain event.
- Cannot transition from `Cancelled` to any other status.

**Domain behaviour methods:**
- `Publish()` — Draft → Published
- `Cancel()` — Draft|Published → Cancelled, raises `EventCancelled`
- `AddInvitation(email)` — creates Invitation with generated token, raises `InvitationSent`
- `CancelInvitation(invitationId)` — cancels a Pending invitation, clears its token

---

### 1.2 `Invitation` (Entity, owned by `Event`)

Represents a single invitation sent to a participant email address. Participants are guests — no account is required. RSVP is authorized via a signed magic link token (see ADR 0006).

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `EventId` | `Guid` | FK to `Event` |
| `ParticipantEmail` | `string` | Email address of the invitee; unique constraint per event |
| `Status` | `InvitationStatus` | See §3 |
| `SentAt` | `DateTimeOffset` | Set when invitation is created |
| `RespondedAt` | `DateTimeOffset?` | Set when participant responds |
| `RsvpTokenHash` | `string?` | HMAC-SHA256 hash of the raw token (raw token sent in email only) |
| `RsvpTokenExpiresAt` | `DateTimeOffset?` | UTC expiry; `null` once token is used or invitation cancelled |

**Invariants:**
- Only one active invitation (non-Cancelled) per email address per event.
- Can only respond (Accept/Decline) if status is `Pending`.
- Token must be valid (non-null, non-expired, hash matches) to respond.
- Token is cleared (single-use) after a successful response.
- Accepting when event is at capacity raises a domain exception.

**Domain behaviour methods:**
- `Accept(tokenHash)` — validates token, Pending → Accepted, clears token, raises `InvitationResponded`
- `Decline(tokenHash)` — validates token, Pending → Declined, clears token, raises `InvitationResponded`
- `Cancel()` — Pending → Cancelled, clears token

---

### 1.3 `ApplicationUser` (Entity — identity mirror for Admin/Organizer)

A local representation of an Entra ID user (Admin or Organizer), created or updated on first authenticated request. **Participants are guests and do not have `ApplicationUser` records** — they are identified solely by `ParticipantEmail` on the `Invitation`.

| Property | Type | Notes |
|---|---|---|
| `Id` | `string` | Entra ID Object ID (OID from JWT `oid` claim) |
| `Email` | `string` | From JWT `email` claim |
| `DisplayName` | `string` | From JWT `name` claim |
| `Roles` | `IEnumerable<string>` | From Entra ID App Role claims — not persisted locally |

> Roles are authoritative in Entra ID and are not stored in the application database. The `ApplicationUser` table is used for Organizer ownership checks and Admin user management.

---

### 1.4 `OutboxMessage` (Entity)

Represents a pending domain event payload to be published to the messaging infrastructure. Written in the same database transaction as the domain change.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Type` | `string` | Fully qualified event type name (e.g., `InvitationSent`) |
| `Payload` | `string` | JSON-serialized event data |
| `CreatedAt` | `DateTimeOffset` | When the message was written |
| `PublishedAt` | `DateTimeOffset?` | `null` = not yet published; set by `ProcessOutboxFunction` |
| `Error` | `string?` | Last error if publishing failed |
| `RetryCount` | `int` | Number of publish attempts |

---

## 2. Entity Relationship Diagram

```
┌─────────────────────────┐           ┌──────────────────────────┐
│         Event           │           │       Invitation         │
├─────────────────────────┤  1     *  ├──────────────────────────┤
│ Id             (PK)     │◀────────▶│ Id              (PK)     │
│ Title                   │           │ EventId          (FK)    │
│ Description             │           │ ParticipantEmail  (UQ*)  │
│ DateTime                │           │ Status                   │
│ Location                │           │ SentAt                   │
│ Capacity                │           │ RespondedAt              │
│ Status                  │           │ RsvpTokenHash            │
│ OrganizerId             │           │ RsvpTokenExpiresAt       │
│ CreatedAt               │           └──────────────────────────┘
│ UpdatedAt               │           * unique per (EventId, ParticipantEmail)
└─────────────────────────┘

┌─────────────────────────┐
│     ApplicationUser     │
│  (Admin / Organizer)    │
├─────────────────────────┤
│ Id  (Entra OID)  (PK)   │
│ Email                   │
│ DisplayName             │
└─────────────────────────┘

┌─────────────────────────┐
│      OutboxMessage      │
├─────────────────────────┤
│ Id             (PK)     │
│ Type                    │
│ Payload                 │
│ CreatedAt               │
│ PublishedAt             │
│ Error                   │
│ RetryCount              │
└─────────────────────────┘
```

---

## 3. Enumerations

### `EventStatus`

| Value | Description |
|---|---|
| `Draft` | Created but not yet visible to participants; cannot be invited to |
| `Published` | Active; invitations can be sent |
| `Cancelled` | Permanently deactivated; all pending invitations are invalidated |

### `InvitationStatus`

| Value | Description |
|---|---|
| `Pending` | Sent, awaiting participant response |
| `Accepted` | Participant confirmed attendance |
| `Declined` | Participant declined |
| `Cancelled` | Cancelled by the organizer before a response was given |

---

## 4. Domain Events

Domain events are raised inside aggregate methods and dispatched by the Application layer after `SaveChanges()`.

| Event | Raised By | Triggered When |
|---|---|---|
| `EventCancelled` | `Event.Cancel()` | An event's status changes to `Cancelled` |
| `InvitationSent` | `Event.AddInvitation()` | A new invitation is created |
| `InvitationResponded` | `Invitation.Accept()` / `Invitation.Decline()` | A participant submits an RSVP |

### `InvitationSent`

```csharp
public record InvitationSent(
    Guid EventId,
    string EventTitle,
    DateTimeOffset EventDateTime,
    string? EventLocation,
    Guid InvitationId,
    string ParticipantEmail,
    string RsvpToken,          // raw token — included in email link; NOT stored in DB
    DateTimeOffset TokenExpiresAt
) : IDomainEvent;
```

### `EventCancelled`

```csharp
public record EventCancelled(
    Guid EventId,
    string EventTitle,
    DateTimeOffset EventDateTime,
    IReadOnlyList<string> AffectedParticipantEmails
) : IDomainEvent;
```

---

## 5. Domain Services

| Service | Responsibility |
|---|---|
| `IRsvpTokenService` | Generates a raw HMAC-SHA256 token + its hash; validates a raw token against a stored hash and expiry. Defined in Domain, implemented in Infrastructure. |

---

## 6. Validation Rules Summary

| Rule | Where Enforced |
|---|---|
| Event date must be in the future | Domain (`Event` constructor) + FluentValidation |
| Event capacity must be positive if set | Domain (`Event` constructor) + FluentValidation |
| Duplicate invitation per email per event | Domain (`Event.AddInvitation()`) |
| Invitation accept when at capacity | Domain (`Invitation.Accept()`) |
| RSVP token must be valid, non-expired, hash-matching | Domain (`Invitation.Accept()` / `Decline()`) |
| Token is cleared after use (single-use) | Domain (`Invitation.Accept()` / `Decline()`) |
| State transition guards | Domain methods (throw `DomainException`) |
| Input format / required fields | FluentValidation in Application layer |
