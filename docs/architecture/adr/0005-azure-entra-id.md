# ADR 0005 — Azure Entra ID for Authentication and Authorization

| | |
|---|---|
| **Status** | Accepted (updated 2026-02-23) |
| **Date** | 2026-02-23 |
| **Deciders** | Engineering team |

## Context

The application requires authentication (who is the user?) and role-based authorization (what are they allowed to do?). The privileged roles are `Admin` and `Organizer`. Participants are **external guests** who should be able to RSVP without requiring an enterprise account. An identity provider must be chosen that is consistent with the Azure-first infrastructure strategy and realistic for production enterprise scenarios.

> Participant authentication is handled separately via a magic link (signed HMAC token) — see **ADR 0006**.

## Decision

We use **Azure Entra ID (formerly Azure Active Directory)** as the identity provider for **Admin and Organizer roles only**, with **JWT Bearer token authentication**.

- The application is registered in an **Entra ID App Registration**.
- **App Roles** (`Admin`, `Organizer`) are defined on the App Registration.
- Users and groups are assigned app roles in the Entra ID portal.
- The API validates the Bearer JWT issued by Entra ID on protected requests.
- Role claims from the token are used with `[Authorize(Roles = "...")]` on endpoints.
- On first authenticated request, the API upserts a local `ApplicationUser` record from the JWT claims (`oid`, `email`, `name`).
- Participant RSVP endpoints are **public** (no Bearer token required) and are secured by a short-lived signed magic link token instead.

### JWT Claims Used

| Claim | Purpose |
|---|---|
| `oid` (Object ID) | Stable unique user identifier — used as `ApplicationUser.Id` |
| `email` | Display and invitation lookup |
| `name` | Display name |
| `roles` | App role assignments (`Admin`, `Organizer`) — used for authorization |

### API Authorization Summary

| Endpoint | Auth Method | Required Role |
|---|---|---|
| `POST /events` | Entra ID JWT | `Organizer` |
| `PUT /events/{id}` | Entra ID JWT | `Organizer` (own events only) |
| `DELETE /events/{id}` | Entra ID JWT | `Organizer` (own events only) |
| `POST /events/{id}/invitations` | Entra ID JWT | `Organizer` (own events only) |
| `GET /events` (all) | Entra ID JWT | `Admin` |
| `GET /users` | Entra ID JWT | `Admin` |
| `PUT /users/{id}/roles` | Entra ID JWT | `Admin` |
| `PUT /invitations/respond` | Magic Link Token | _(guest — no account)_ |

## Alternatives Considered

| Option | Reason not chosen |
|---|---|
| Entra ID for all roles including Participant | Requires external/casual participants to have an enterprise account; creates unnecessary friction for RSVP |
| ASP.NET Core Identity for Participants | Second user store, custom token issuance, password reset, email confirmation — significant boilerplate that distracts from the core patterns being taught; see ADR 0006 |
| Auth0 / Okta | Third-party SaaS; adds external dependency and cost; less relevant for Azure-focused training |
| Entra External Identities (B2C) | Separate tenant resource, significant setup overhead; better suited as a standalone training topic |
| API Keys | No user identity — cannot enforce per-user resource ownership or role-based access |
| No authentication | Acceptable for pure domain demos but undermines the "prod-like" training goal |

## Consequences

### Positive
- No custom auth code — token validation is handled by `Microsoft.Identity.Web` / `Microsoft.AspNetCore.Authentication.JwtBearer`.
- App Roles in Entra ID are the authoritative source of role assignments — no role sync needed.
- Consistent with enterprise Azure patterns — relevant and transferable knowledge for attendees.
- Works seamlessly with Managed Identity for service-to-service calls in the future.
- Participants as guests avoids the need for a second identity system entirely.

### Negative / Trade-offs
- Requires an Azure Entra ID tenant — not runnable offline without mocking or test tokens.
- Local development requires either a real dev tenant or a configured test token (documented in `docs/operations/local-development.md`).
- Token expiry (default 1 hour access token) requires clients to handle refresh — not a concern for an API-only v1 but relevant when the React frontend is added.
- App Role assignments in Entra ID are not version-controlled — must be documented and scripted separately (future: `infra/scripts/assign-roles.sh`).
- The split auth model (JWT for organizers, magic link for participants) requires two distinct auth middleware registrations and clear endpoint segregation.
