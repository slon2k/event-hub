# Stage 2 Readiness Recommendations

| Field | Value |
| --- | --- |
| Status | Draft recommendation |
| Date | 2026-03-07 |
| Scope | End-of-Stage-1 readiness review |

## Executive Summary

The codebase is technically stable for transition planning: all tests pass locally (`214/214`).

Before moving to Stage 2, the main work is alignment and completeness:

- Align requirements with implemented API contracts.
- Complete basic Admin API scope as Stage 1 closure.
- Close invitation functional test gaps.
- Improve CI trigger coverage for notifications test-only changes.
- Clean markdown lint issues if docs lint is enforced in your quality gate.

## Current Health Snapshot

| Area | Status | Notes |
| --- | --- | --- |
| Solution tests | Good | `dotnet test EventHub.slnx` passed: 214 tests |
| Core event + invitation flows | Good | End-to-end paths implemented and tested |
| Notifications pipeline | Good with caveat | Tests run in workflow, but trigger paths are narrow |
| Requirements-code alignment | Partial | Some contract and scope drift remains |
| Documentation lint | Needs cleanup | Markdown style violations in multiple docs |

## Priority Recommendations

### P1 - Complete and document Stage 1 Admin baseline

Requirements currently include Admin use cases:

- List users
- Assign/remove roles
- View all events

No corresponding Admin endpoints/handlers are implemented yet.

Recommendation:

- Execute minimum Admin APIs before Stage 2 implementation starts.
- Keep Stage 2 focused on post-closure technical features.
- Record idempotency and self-modification behavior in requirements and operations docs.

## P1 - Align API contract with requirements

Current mismatch examples:

- Requirements mention `PUT /invitations/respond` and `400 Bad Request` for invalid/expired/used tokens.
- Implementation uses `POST /api/invitations/respond` with domain failures mapped via global exception handling.

Recommendation:

- Pick source of truth (recommended: implementation + tests), then update requirements to match.
- If product intent is the requirements behavior, update code and tests instead.

## P2 - Complete invitation endpoint functional coverage

Missing or weakly covered scenarios in API functional tests:

- Reissue invitation endpoint success and authorization/not-found cases.
- RSVP success path using a valid token.
- RSVP conflict path after token is consumed.

Recommendation:

- Add these tests before Stage 2 branch cut so behavior is locked by regression tests.

## P2 - Expand notifications workflow trigger paths

Current notifications workflow trigger path is source-only:

- `src/notifications/**`

Risk:

- Test-only changes under notifications test projects do not trigger the notifications pipeline automatically.

Recommendation:

- Include test path filters:
  - `tests/EventHub.Notifications.UnitTests/**`
  - `tests/EventHub.Notifications.IntegrationTests/**`

## P3 - Resolve markdown lint debt (if docs lint is gated)

Observed markdown issues include table style, ordered list style, and fenced code block language tags.

Recommendation:

- Run a focused docs formatting pass on:
  - `README.md`
  - `docs/operations/local-development.md`
  - `docs/architecture/adr/0003-outbox-pattern.md`
- Add a markdownlint task/check in CI if consistent docs quality is desired.

## Suggested Execution Order

1. Complete Stage 1 Admin baseline and document boundary.
2. Requirements-contract alignment update.
3. Invitation functional test completion.
4. Workflow trigger path expansion for notifications tests.
5. Docs lint cleanup.

## Stage 2 Go/No-Go Checklist

Use this list before opening Stage 2 workstream:

- [ ] Stage 1 Admin baseline completed and documented.
- [ ] Requirements and API contracts are consistent.
- [ ] Invitation API functional tests cover reissue + RSVP happy/conflict paths.
- [ ] Notifications workflow triggers include notifications test project changes.
- [ ] Docs lint issues resolved (or explicitly accepted as non-blocking).
- [ ] Full suite remains green after changes.

## Notes

This recommendation is intentionally conservative: it focuses on reducing ambiguity and regression risk before introducing Stage 2 complexity.

Related planning artifact:

- `docs/operations/stage2-scope-and-demo-plan.md`
