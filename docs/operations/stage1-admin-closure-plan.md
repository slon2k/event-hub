# Stage 1 Admin Closure Plan

| Field | Value |
| --- | --- |
| Status | Approved for implementation |
| Date | 2026-03-07 |
| Scope | Remaining required Admin capabilities for Stage 1 |

## 1. Goals

Close the remaining Stage 1 requirements with minimal disruption to existing EventHub architecture:

- List users and their roles.
- Assign Organizer role.
- Remove Organizer role.
- View all events (read-only).

## 2. Requirement Boundary

Admin scope is part of current requirements completion, not a Stage 2 feature.

- **Stage 1 must-have**: Admin endpoints and behaviors listed above, including basic pagination for `GET /api/admin/users` (required per §5.1 of functional requirements).
- **Stage 1 hardening (optional)**: filtering, search tuning, paging for events endpoint, and advanced idempotency semantics.

## 3. Functional Scope

### 3.1 List Users (Admin)

- Admin can retrieve a paginated list of Entra users (required per functional requirements §5.1).
- Response includes user identity data and whether Organizer role is assigned.
- Pagination is required (`page`, `pageSize`); search/filtering is optional hardening.

### 3.2 Assign Organizer Role (Admin)

- Admin can assign Organizer role to a target user.
- Admin cannot modify own roles.

### 3.3 Remove Organizer Role (Admin)

- Admin can remove Organizer role from a target user.
- Admin cannot modify own roles.

### 3.4 View All Events (Admin)

- Admin can list events across all organizers.
- Endpoint is read-only.

## 4. API Contract Proposal

All endpoints require `AdminPolicy`.

- `GET /api/admin/users?page=1&pageSize=50` — pagination required; returns `PagedResult<AdminUserDto>`
- `PUT /api/admin/users/{userId}/roles/organizer`
- `DELETE /api/admin/users/{userId}/roles/organizer`
- `GET /api/admin/events`

Optional hardening (non-blocking for Stage 1 closure):

- `GET /api/admin/users?page=1&pageSize=50&search=` — search filter
- `GET /api/admin/events?page=1&pageSize=50&status=&organizerId=` — pagination + filtering for events

### 4.1 Suggested Response Models

```csharp
public sealed record AdminUserDto(
    string UserId,
    string? DisplayName,
    string? Email,
    bool IsOrganizer,
    bool IsAdmin);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminEventSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset DateTime,
    string? Location,
    string Status,
    string OrganizerId,
    DateTimeOffset CreatedAt);
```

### 4.2 Status Codes

- `200 OK`: successful query/list.
- `204 No Content`: role assignment/removal succeeded (or idempotent no-op, if chosen).
- `400 Bad Request`: invalid request payload/parameters.
- `403 Forbidden`: caller is not admin or tries self role change.
- `404 Not Found`: target user not found.
- `409 Conflict`: optional for already assigned/not assigned role if non-idempotent behavior is selected.

## 5. Architecture Plan

### 5.1 Application Layer

Add `Features/Admin` and keep Graph details out of handlers.

- Queries:
  - `GetUsersQuery`
  - `GetAllEventsQuery`
- Commands:
  - `AssignOrganizerRoleCommand`
  - `RemoveOrganizerRoleCommand`

Add abstraction:

```csharp
public interface IIdentityAdminService
{
    Task<PagedResult<AdminUserDto>> GetUsersAsync(int page, int pageSize, string? search, CancellationToken ct);
    Task AssignOrganizerRoleAsync(string targetUserId, string actingAdminUserId, CancellationToken ct);
    Task RemoveOrganizerRoleAsync(string targetUserId, string actingAdminUserId, CancellationToken ct);
}
```

### 5.2 Infrastructure Layer

Implement `IIdentityAdminService` with Microsoft Graph:

- Class: `EntraIdentityAdminService`
- Responsibilities:
  - Resolve API service principal.
  - Resolve Organizer app role id.
  - Query users.
  - Read and mutate app role assignments.

Register in DI via `AddInfrastructure(...)`.

### 5.3 API Layer

Add `AdminEndpoints.cs` under `src/backend/EventHub.Api/Endpoints`.

- Map `/api/admin/*` routes.
- Require `AdminPolicy` at group level.
- Enforce "admin cannot self-modify roles" before dispatching role commands.
- Extract `actingAdminUserId` from JWT claims at the endpoint layer (`HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)`) and pass it into the command — do not rely on the handler to resolve the caller identity.

## 6. Microsoft Entra / Graph Prerequisites

Backend identity must have app-only Graph permissions with admin consent:

- `User.Read.All`
- `Application.Read.All`
- `AppRoleAssignment.ReadWrite.All`

Configuration needed:

- Tenant id
- Backend client id (managed identity or app registration)
- API app id/client id for app role assignment target
- Optional: cached Organizer app role id

## 7. Behavior Decisions

### 7.1 Role operation idempotency

Selected strategy for Stage 1 closure: idempotent.

- Idempotent:
  - Assign existing role => `204`
  - Remove missing role => `204`

Alternative (not selected for Stage 1): strict.

- Assign existing role => `409`
- Remove missing role => `409`

### 7.2 Paging defaults and limits

Applies to `GET /api/admin/users` (required) and optionally to `GET /api/admin/events` (hardening).

- Default `page=1`, `pageSize=50`.
- Max `pageSize=200`.

### 7.3 Search semantics (optional hardening)

- Start with case-insensitive `displayName`/`mail` contains.

## 8. Implementation Sequence

### Step 1: Admin events read model (fastest)

- Add `GetAllEventsQuery` + handler.
- Add `GET /api/admin/events` endpoint.
- Add unit + functional tests.

### Step 2: Identity abstraction

- Add `IIdentityAdminService` + contracts.
- Add command/query handlers for users and roles.
- `GetUsersQuery` accepts `page` and `pageSize` parameters; handler returns `PagedResult<AdminUserDto>`.

### Step 3: Graph integration

- Implement `EntraIdentityAdminService` in Infrastructure.
- Wire configuration and DI.

### Step 4: Admin user endpoints

- Add `GET /api/admin/users`.
- Add assign/remove organizer endpoints.
- Register `AdminEndpoints` in `Program.cs`: call `app.MapAdminEndpoints()` alongside `MapEventEndpoints()` and `MapInvitationEndpoints()`.

### Step 5: Hardening (optional but recommended)

- Add paging/filtering/search.
- Improve error mapping for Graph/API failures.
- Add logging and telemetry around role changes.
- Update docs and operational runbook.

## 9. Testing Strategy

### 9.1 Unit tests

- Handlers for all admin commands/queries.
- Self-modification rule.
- Idempotency behavior.

### 9.2 Functional tests (API)

- `GET /api/admin/events` with admin token.
- `GET /api/admin/users` happy path and auth failures.
- Assign/remove organizer endpoints with:
  - success
  - self-modification forbidden
  - target not found
  - already assigned/not assigned behavior

### 9.3 Integration tests (Infrastructure)

- Mock Graph SDK client wrappers where possible.
- Optional live tenant smoke tests via manual pipeline only.

## 10. CI/CD Considerations

- Ensure admin-related tests run in API pipeline.
- Keep live-tenant Graph tests out of PR gating unless sandbox is reliable.
- Add secrets/config checks for Graph permission prerequisites in deployment runbook.

Tracked improvement — non-blocking for Stage 1 closure, required before Stage 2 kickoff:

- Introduce test-environment API E2E automation in a dedicated workflow (`e2e-test.yml`).
- Keep PR gating fast by excluding E2E from default pipeline runs (`--filter "Category!=E2E"`).
- Run E2E only after deployment to `test` (or manual dispatch), with environment-based configuration and secrets.
- Required GitHub Actions environment secrets: `EVENTHUB_TEST_HOST`, `EVENTHUB_TEST_ADMIN_TOKEN`, `EVENTHUB_TEST_ORGANIZER_TOKEN`.

## 11. Stage 1 Stabilization Release Gate

After Stage 1 Admin acceptance criteria are met, treat Stage 1 as a stabilization milestone:

- Merge `dev` into `master`.
- Deploy `master` to the `test` environment.
- Run post-deploy verification in `test` before starting Stage 2 implementation.

Recommended post-deploy verification in `test`:

- Smoke test core API endpoints (health, events, invitations).
- Verify Admin API baseline end-to-end:
  - list users
  - assign/remove Organizer role
  - list all events
- Verify role-security behavior:
  - non-admin blocked
  - self-role modification blocked
- Verify idempotent role behavior (`204` on no-op assign/remove).
- Verify notifications pipeline healthy for invitation and RSVP events.

Release decision for Stage 2 kickoff:

- Proceed only if `test` validation is green and no blocker defects remain.
- If blockers are found, fix on `dev`, re-run CI, and redeploy to `test`.

## 12. Acceptance Criteria (Stage 1 Closure)

Stage 1 Admin scope is complete when:

- [ ] Admin events endpoint returns cross-organizer event list.
- [ ] Admin users endpoint returns paginated users + role state (`PagedResult<AdminUserDto>`).
- [ ] Organizer role assign/remove endpoints are implemented and policy-protected.
- [ ] Self role modification is blocked.
- [ ] Chosen idempotency behavior is implemented and documented.
- [ ] Unit and functional tests are green in CI.
- [ ] Requirements and operations docs are updated to match behavior.
- [ ] `dev` is merged to `master` and deployed to `test` environment.
- [ ] Post-deploy validation in `test` passes with no blocker defects.
- [ ] Dedicated `test`-environment E2E workflow (`e2e-test.yml`) is in place and passes.

Optional hardening completion:

- [ ] Paging/filtering/search implemented for admin list endpoints.

## 13. Out of Scope for this Closure

- Full user lifecycle management beyond Organizer role toggle.
- Fine-grained RBAC beyond Admin/Organizer role assignment.
- Bulk role operations.
- UI for admin operations (API only).
