# ADR 0006 — Magic Link (Tokenized RSVP) for Guest Participants

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-02-23 |
| **Deciders** | Engineering team |

## Context

The `Participant` role presents a fundamental design question: should participants be required to have an authenticated account, and if so, via which identity provider?

The original design required all participants to have an Entra ID account. This creates unnecessary friction: participants are often external to the organization, occasional users, or people who should not require an enterprise account just to RSVP to an event.

Three options were evaluated (see discussion in ADR 0005):

1. Require Entra ID for participants — too much friction for casual participants.
2. ASP.NET Core Identity for participants alongside Entra ID — dual identity stores, boilerplate auth infrastructure, no meaningful gain.
3. Magic link (tokenized RSVP) — participants receive a signed URL in their invitation email; one click accepts or declines. No account required.

## Decision

Participants are treated as **unauthenticated guests**. RSVP actions are authorized via a **short-lived, signed HMAC-SHA256 token** embedded in the invitation email.

### Token Structure

The token encodes:
- `InvitationId` — the specific invitation being responded to
- `ParticipantEmail` — binds the token to the intended recipient
- `ExpiresAt` — UTC timestamp; token is invalid after this point
- A **HMAC-SHA256 signature** over the above fields using a server-side secret key

```
Token = Base64Url( JSON({ invitationId, email, expiresAt }) ) + "." + Base64Url( HMAC-SHA256( payload, secret ) )
```

### RSVP Endpoint (public)

```
PUT /invitations/respond
Body: { "token": "<magic-token>", "response": "Accepted" | "Declined" }
```

- No `Authorization` header required.
- The API validates the token signature and expiry before processing.
- On success, the invitation status is updated and the token is **invalidated** (single-use).
- An invalid, expired, already-used, or tampered token returns `400 Bad Request`.

### Token Lifecycle

| Event | Action |
|---|---|
| Invitation created | Token generated and stored in `Invitation.RsvpToken` (hashed), `RsvpTokenExpiresAt` set |
| Email sent (ACS) | Token embedded in RSVP URL in the email body |
| Participant responds | Token validated, invitation updated, `RsvpToken` cleared (single-use) |
| Invitation cancelled | `RsvpToken` cleared; any future use of the token returns `400` |
| Token expires (72h default) | `GET /invitations/reissue?token=<old>` (future v2) re-sends a fresh token |

### Token Stored as Hash

The raw token is sent only in the email. The database stores only the **HMAC of the token** — the same strategy used for secure password reset tokens. This means a leaked database does not expose usable tokens.

## Alternatives Considered

| Option | Reason not chosen |
|---|---|
| Entra ID for participants | Requires enterprise account; too much friction for external/occasional participants |
| ASP.NET Core Identity | Builds a second user store and auth system; significant boilerplate with no architectural benefit for this demo; password reset / email confirmation flows distract from core patterns |
| Entra External Identities (B2C) | Separate tenant required; significant setup overhead; better suited as a standalone training topic |
| No authentication (open link) | Anyone who has the URL (e.g., forwarded email) can respond; no integrity guarantee |

## Consequences

### Positive
- **Zero friction for participants** — one click from the email, no registration required.
- **No second identity system** — Entra ID remains clean for Admin/Organizer; participant auth is entirely self-contained.
- **Teachable security pattern** — HMAC signing, single-use tokens, hash-only storage, and expiry are real-world techniques used in password reset, email verification, and unsubscribe flows.
- **Simplifies the domain** — `Invitation` becomes the sole identity anchor for participants; no `ApplicationUser` record needed for guests.
- **Consistent with the notification pipeline** — the magic link is simply included in the ACS Email payload; no new infrastructure required.

### Negative / Trade-offs
- **Token-in-URL** — the token is present in the email link. If a participant forwards their invitation email, the recipient could RSVP on their behalf. Mitigated by single-use invalidation and short expiry (72h).
- **No "view my invitations" for participants** — without an account, a participant cannot list past invitations. Mitigated in v2 by the React frontend and optional Entra External Identities.
- **Token re-issue flow not in v1** — if the token expires before responding, the participant cannot self-serve; the organizer must cancel and re-send the invitation. A `reissue` endpoint is planned for v2.
- **Secret key rotation** — the HMAC secret must be rotated periodically and stored in Key Vault; all active tokens become invalid on rotation (acceptable given 72h lifetime).
