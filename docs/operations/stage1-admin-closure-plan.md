# Stage 1 Admin Closure Plan

| Field | Value |
| --- | --- |
| Status | Proposed |
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

- **Stage 1 must-have**: Admin endpoints and behaviors listed above.
- **Stage 1 hardening (optional)**: filtering, paging, search tuning, and advanced idempotency semantics.

## 3. Functional Scope

### 3.1 List Users (Admin)

- Admin can retrieve a list of Entra users.
- Response includes user identity data and whether Organizer role is assigned.
- Paging/filtering can be added now or as Stage 1 hardening.

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

- `GET /api/admin/users`
- `PUT /api/admin/users/{userId}/roles/organizer`
- `DELETE /api/admin/users/{userId}/roles/organizer`
- `GET /api/admin/events`

Optional hardening (non-blocking for Stage 1 closure):

- `GET /api/admin/users?page=1&pageSize=50&search=`
- `GET /api/admin/events?page=1&pageSize=50&status=&organizerId=`

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

Choose one strategy and keep consistent:

- Idempotent:
  - Assign existing role => `204`
  - Remove missing role => `204`
- Strict:
  - Assign existing role => `409`
  - Remove missing role => `409`

Recommendation: idempotent for operational simplicity.

### 7.2 Paging defaults and limits (optional hardening)

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

### Step 3: Graph integration

- Implement `EntraIdentityAdminService` in Infrastructure.
- Wire configuration and DI.

### Step 4: Admin user endpoints

- Add `GET /api/admin/users`.
- Add assign/remove organizer endpoints.

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

## 11. Acceptance Criteria (Stage 1 Closure)

Stage 1 Admin scope is complete when:

- [ ] Admin events endpoint returns cross-organizer event list.
- [ ] Admin users endpoint returns users + role state.
- [ ] Organizer role assign/remove endpoints are implemented and policy-protected.
- [ ] Self role modification is blocked.
- [ ] Chosen idempotency behavior is implemented and documented.
- [ ] Unit and functional tests are green in CI.
- [ ] Requirements and operations docs are updated to match behavior.

Optional hardening completion:

- [ ] Paging/filtering/search implemented for admin list endpoints.

## 12. Out of Scope for this Closure

- Full user lifecycle management beyond Organizer role toggle.
- Fine-grained RBAC beyond Admin/Organizer role assignment.
- Bulk role operations.
- UI for admin operations (API only).
