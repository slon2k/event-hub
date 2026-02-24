# Functional Requirements

| | |
|---|---|
| **Status** | Draft |
| **Date** | 2026-02-23 |
| **Version** | 0.1 |

---

## 1. Actors and Roles

| Role | Account Required | Description |
|---|---|---|
| **Admin** | Yes — Entra ID | Platform administrator. Manages user role assignments and has read-only visibility into all events. |
| **Organizer** | Yes — Entra ID | Authenticated user who creates and manages events and sends invitations to participants. |
| **Participant** | **No — guest** | External person invited by email. RSVPs via a signed magic link. No account or registration required. |

> Admin and Organizer are Entra ID accounts with App Roles assigned. A single user can hold both roles.
> Participants are guests — they are identified only by their email address and a short-lived signed token in their invitation email.

---

## 2. Authentication and Authorization

### Admin and Organizer — Azure Entra ID (JWT Bearer)
- Admin and Organizer endpoints require a valid **Entra ID JWT Bearer token**.
- Roles (`Admin`, `Organizer`) are defined as **Entra ID App Roles** and conveyed in the token.
- Unauthenticated requests to protected endpoints return `401 Unauthorized`.
- Requests by authenticated users to endpoints outside their role return `403 Forbidden`.

### Participant — Magic Link Token
- Participants **do not require an account**.
- When an invitation is sent, a short-lived **HMAC-SHA256 signed token** is generated and embedded in the invitation email as a URL.
- The RSVP endpoint (`PUT /invitations/respond`) is **public** (no Bearer token) and validates the magic link token.
- Tokens are **single-use**: invalidated immediately after the participant responds.
- Tokens expire after **72 hours**. An expired token returns `400 Bad Request`.
- See **ADR 0006** for full token design.

---

## 3. Event Management

### 3.1 Create Event (Organizer)

- An Organizer can create an event providing:
  - **Title** (required, max 200 chars)
  - **Description** (optional, max 2000 chars)
  - **Date and Time** (required, must be in the future)
  - **Location** (optional, max 500 chars)
  - **Capacity** (optional; if set, must be a positive integer)
- A newly created event is in **Draft** status.
- The system records the creating user as the **Organizer** of the event.

### 3.2 Publish Event (Organizer)

- An Organizer can publish a **Draft** event, changing its status to **Published**.
- Only **Published** events can have invitations sent.

### 3.3 Update Event (Organizer)

- An Organizer can update their own **Draft** or **Published** event's details.
- Updating a **Published** event does not change its status.
- Date/time changes on a Published event do not automatically re-notify participants (v1 scope).

### 3.4 Cancel Event (Organizer)

- An Organizer can cancel their own **Draft** or **Published** event, changing its status to **Cancelled**.
- Cancelling a **Published** event triggers a cancellation notification to all invited participants (see §6).
- A **Cancelled** event cannot be reactivated.

### 3.5 View Own Events (Organizer)

- An Organizer can list all events they have created, with filtering by status.
- An Organizer can view the full detail of an event including the RSVP summary (total invited / accepted / declined / pending).

### 3.6 View Event Participants (Organizer)

- An Organizer can view the list of participants for their event, including each participant's RSVP status.

---

## 4. Invitation Management

### 4.1 Send Invitation (Organizer)

- An Organizer can invite **any person by email address** to their **Published** event. The recipient does not need to have an account.
- The same email address cannot be invited to the same event twice (duplicate prevention). Returns `409 Conflict` on duplicate.
- If the event has a capacity limit and the number of accepted RSVPs has reached it, no further invitations can be sent.
- Sending an invitation:
  1. Generates a signed HMAC-SHA256 token; stores its hash and expiry on the `Invitation`.
  2. Creates an `Invitation` record in status **Pending**.
  3. Writes an `OutboxMessage` for the `InvitationSent` notification (same transaction — includes the raw token for the email link).

### 4.2 Cancel Invitation (Organizer)

- An Organizer can cancel a **Pending** invitation before the participant has responded.
- A **Cancelled** invitation cannot be re-sent; a new invitation must be issued.

### 4.3 Respond to Invitation (Participant — via Magic Link)

- The invitation email contains a **magic link** embedding the signed RSVP token.
- The participant clicks the link, which calls `PUT /invitations/respond` with the token and their chosen response.
- Valid responses:
  - **Accept** → status changes to `Accepted`
  - **Decline** → status changes to `Declined`
- The token is **single-use**: invalidated immediately after a successful response.
- If the event has reached capacity and the participant tries to Accept, the action is rejected with `409 Conflict`.
- An invalid, expired, already-used, or tampered token returns `400 Bad Request`.
- A participant **cannot change their response after the token is consumed**. To change RSVP, the organizer must cancel the invitation and re-send it.

### 4.4 Re-send Invitation (Organizer)

- An Organizer can re-send an invitation to regenerate a fresh token for a participant whose previous token has expired.
- Re-sending invalidates the old token and generates a new one with a fresh 72-hour expiry.

---

## 5. User Management

### 5.1 List Users (Admin)

- An Admin can retrieve a paginated list of all registered users including their assigned roles.

### 5.2 Assign / Remove Role (Admin)

- An Admin can assign or remove the `Organizer` role from any user (Entra ID accounts only).
- An Admin cannot modify their own roles.
- Participants are guests and do not have application roles; they are not managed through this feature.

### 5.3 View All Events (Admin)

- An Admin can list all events across all organizers (read-only).

---

## 6. Notifications

### 6.1 Invitation Sent

- When an invitation is created, the participant receives an **email notification** containing:
  - Event title, date/time, location
  - A **magic link** (signed RSVP URL) to accept or decline — no login required

### 6.2 Event Cancelled

- When an event is cancelled, all participants with a **Pending** or **Accepted** invitation receive an **email notification** informing them the event has been cancelled.

### 6.3 Delivery

- Notifications are delivered asynchronously via the Outbox → Azure Service Bus → Azure Functions → Azure Communication Services Email pipeline.
- The user-facing API response is not blocked by email delivery.
- Failed deliveries are retried automatically via Service Bus retry policy; messages that exhaust retries go to the **dead-letter queue** for manual inspection.

---

## 7. Out of Scope (v1)

| Feature | Notes |
|---|---|
| Attendance tracking | Tracking who physically attended vs. who RSVP'd |
| Waitlists | Auto-promoting participants if an accepted RSVP is withdrawn |
| Recurring events | Every event is a one-off |
| Public event registration | All events are invite-only |
| In-app notifications / real-time updates | Email only in v1 |
| Re-notifying participants on event update | Only cancellation triggers a notification in v1 |
| RSVP change after token consumed | Organizer must re-send the invitation; self-service change planned for v2 |
| Magic link re-issue by participant | Organizer re-sends in v1; participant self-service re-issue endpoint planned for v2 |
| Participant account / Entra External Identities | Participants are guests in v1; optional account upgrade planned for v2 |
| Frontend UI | API-only in v1; React frontend planned for v2 |
