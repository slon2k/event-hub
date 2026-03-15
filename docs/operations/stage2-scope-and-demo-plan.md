# Stage 2 Scope and Demo Plan

| Field | Value |
| --- | --- |
| Status | Proposed plan |
| Date | 2026-03-08 |
| Scope | Stage 2 implementation and demo planning |

## 1. Objective

Deliver a Stage 2 increment that is both product-meaningful and demo-ready:

- Expose key organizer workflows in UI.
- Demonstrate RSVP and notification lifecycle end-to-end.
- Add operational visibility needed for confidence during demo and rollout.

## 2. Planning Principles

- Prioritize end-to-end user journeys over isolated endpoints.
- Keep scope focused on technical capabilities required for a credible live demo.
- Favor simple, reliable UX over broad but shallow UI surface area.
- Defer non-critical polish if it does not improve demo narrative or reliability.

Stage boundary decision:

- Stage 2 includes technical delivery and demo-enabling assets only.
- Business packaging and portfolio commercialization artifacts move to Stage 3.

Prerequisite from Stage 1:

- Basic Admin API functionality is implemented as Stage 1 closure.

## 3. Recommended Stage 2 Scope

## 3.1 Must Have (Demo-Critical)

### A. Organizer dashboard with pagination/filtering (backend + UI)

- Event list supports pagination and filters:
  - `status`
  - `dateFrom`, `dateTo`
  - text search by title
- UI table includes:
  - Status chip
  - Event date/time
  - Capacity summary
  - Quick actions (`Publish`, `Cancel`, `Open`)

Acceptance criteria:

- Organizer sees only own events in organizer views.
- Pagination metadata and total count are returned and rendered.
- Filters combine correctly and are reflected in query parameters.

### B. Invitation lifecycle panel in Event Details (UI)

- Participant invitation table with:
  - Email
  - Status (`Pending`, `Accepted`, `Declined`, `Cancelled`)
  - Sent/responded timestamps
- Reissue token action for pending invitations

Acceptance criteria:

- Reissue succeeds for pending invitation and returns `204`.
- Reissue for accepted invitation returns `409` and UI shows clear message.
- Reissue by non-organizer returns `403` and UI shows authorization feedback.

### C. Public RSVP page (UI)

- Token-based RSVP screen with event summary.
- `Accept` / `Decline` response actions.
- Friendly handled states:
  - invalid token (`400`)
  - expired token (`400`)
  - already used token (`409`)

Acceptance criteria:

- Valid token can accept/decline and transitions to success state.
- Already-used token shows non-retriable conflict message.
- Invalid/expired token shows recoverable guidance message.

### D. Reminder notification baseline

- Configurable reminder policy per event (minimal: enabled + 24h before event).
- Outbox/notification processing supports reminder dispatch.
- Basic UI readout of reminder delivery activity (queued/sent/failed counts).

Acceptance criteria:

- Reminder message is generated for eligible accepted participants.
- Reminder events are visible in activity counters.
- Failed sends are retried according to existing retry policy and failures remain observable.

## 3.2 Should Have (High Value, Can Slip)

- Notification activity list by event (not only counters).
- RSVP endpoint abuse guardrails (basic rate limiting and telemetry).
- UI skeleton/loading/empty/error states across Admin and Organizer screens.
- Contract tests for critical external dependencies (Microsoft Graph and notification messaging boundary).

## 3.3 Could Have (Backlog for Stage 2.1)

- Admin role-change audit trail view (who, target user, action, timestamp).
- Event clone workflow.
- Bulk invitation upload/import.
- Advanced reminder windows (e.g., 72h and 1h presets).
- Full-text user search across additional fields.

## 4. API/Contract Changes to Plan

- Extend organizer events query with pagination and filtering parameters.
- Add reminder configuration contract to event details/update flow.
- Keep RSVP token semantics aligned with Stage 1 decision:
  - `400` invalid/expired/not-found token
  - `409` already used token

## 5. UI Surface Plan

### 5.1 Screens

- Admin Users screen
- Admin Events screen
- Organizer Events dashboard
- Event Details with Invitations tab
- Public RSVP page

### 5.2 UX Minimum Bar

- Consistent empty/loading/error states.
- Server error messages mapped to user-friendly text.
- Key actions provide deterministic success/failure feedback.

## 6. Delivery Phases

## Phase 1: Foundation and contracts

- Finalize Stage 2 requirements and DTOs.
- Implement paginated/filtered event queries.
- Add test coverage for new contracts.

Definition of done:

- Unit + functional tests pass for all new endpoints.
- OpenAPI/API documentation reflects final contracts.

## Phase 2: Core UI and end-to-end flow

- Build Organizer dashboard pagination/filtering UI.
- Build Event Details invitation panel + reissue action.
- Build Public RSVP page.

Definition of done:

- Complete demo flow works against local seeded data.
- No blocker defects in must-have screens.

## Phase 3: Notifications and observability

- Implement reminder scheduling/dispatch path.
- Add activity counters and failure visibility.
- Add baseline alerts/log queries for failed reminder sends.

Definition of done:

- Reminder flow is demonstrable end-to-end in test environment.
- Operational visibility exists for successful and failed reminders.

## 7. Test Strategy for Stage 2

- Unit tests for admin handlers, query filtering/pagination logic, reminder rules.
- API functional tests for pagination/filter combinations and reminder behavior.
- UI integration/e2e smoke tests for core demo journey:
  - Organizer event listing/filtering
  - Invitation reissue
  - Participant RSVP

## 8. Demo Script (Recommended)

1. Organizer signs in and sees dashboard with pagination/filtering.
2. Organizer opens event, sends/reissues invitation.
3. Participant opens RSVP page from token and accepts invitation.
4. Organizer reviews notification and reminder activity.

## 9. Risks and Mitigations

- Risk: Graph/role assignment integration complexity.
  - Mitigation: isolate through `IIdentityAdminService` and test with mocks plus one integration path.
- Risk: UI scope creep from advanced design requests.
  - Mitigation: lock Must/Should/Could boundaries at sprint start.
- Risk: Reminder scheduling edge cases (time zones, late updates).
  - Mitigation: define one canonical timezone strategy and test deterministic scenarios.

## 10. Out of Scope for Stage 2

- Multi-tenant administration model.
- Rich analytics/reporting suite.
- Advanced bulk import workflows.
- Complex reminder orchestration rules.
- Portfolio commercialization artifacts (service offers, proposal snippets, CTA packs).

## 11. Go/No-Go Checklist for Stage 2 Demo Readiness

- [ ] Stage 1 Admin API baseline is complete and documented.
- [ ] Organizer dashboard supports pagination and filter combinations.
- [ ] Invitation reissue and RSVP flows are stable and user-friendly.
- [ ] Reminder notifications are sent and observable.
- [ ] Must-have UI screens are complete with loading/error/empty states.
- [ ] Full automated test suite is green.
