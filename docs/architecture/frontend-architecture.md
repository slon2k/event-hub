# Frontend Architecture

| | |
| --- |
| **Status** | Draft |
| **Date** | 2026-03-15 |
| **Version** | 0.1 |

---

## 1. Overview

The EventHub frontend is a **Blazor WebAssembly (WASM) standalone application** that runs entirely in the browser and communicates with the existing REST API over HTTPS. It is deployed as static files independently of the API and serves two distinct audiences:

- **Authenticated users** (Organizer, Admin) — protected by Azure Entra ID via MSAL.
- **Public participants** — access only the RSVP page, which requires no account; authorization is provided by the magic-link token in the URL.

---

## 2. Solution Structure

Two new projects are added:

```text
src/
  shared/
    EventHub.Contracts/       ← Shared DTO class library (no framework deps)
  frontend/
    EventHub.Web/             ← Blazor WASM standalone application
```

`EventHub.Contracts` is referenced by both `EventHub.Application` and `EventHub.Web`, ensuring DTO types are shared at the C# level and stay in sync at compile time with no code generation step required.

---

## 3. Project: EventHub.Contracts

A slim, `net10.0` class library with no framework or infrastructure dependencies.

**Types extracted from `EventHub.Application`:**

| Type | Description |
| --- | --- |
| `EventSummaryDto` | Event list row: Id, Title, DateTime, Location, Capacity, Status, invitation counts |
| `EventDetailDto` | Full event + nested `IReadOnlyList<InvitationDto>` |
| `InvitationDto` | Invitation row: Id, ParticipantEmail, Status, SentAt, RespondedAt |
| `AdminEventSummaryDto` | Event row for Admin view |
| `AdminUserDto` | User row: UserId, DisplayName, Email, IsOrganizer, IsAdmin |
| `PagedResult<T>` | Pagination envelope: Items, Page, PageSize, TotalCount |

---

## 4. Project: EventHub.Web

### 4.1 Technology

| Concern | Choice |
| --- | --- |
| Framework | Blazor WebAssembly (.NET 10, standalone) |
| Authentication | `Microsoft.Authentication.WebAssembly.Msal` |
| Component library | MudBlazor |
| DTO sharing | `EventHub.Contracts` project reference |

### 4.2 Folder Structure

```text
EventHub.Web/
├── Program.cs                   ← DI, MSAL, typed HttpClients
├── App.razor                    ← Router + MudThemeProvider + MudSnackbarProvider
├── _Imports.razor
├── wwwroot/
│   └── appsettings.json         ← ApiBaseUrl, AzureAd config (ClientId, TenantId, Scopes)
├── HttpClients/
│   ├── EventApiClient.cs        ← /api/events/** (authenticated)
│   ├── AdminApiClient.cs        ← /api/admin/** (authenticated, Admin role)
│   └── InvitationApiClient.cs   ← organizer endpoints (auth) + public RSVP (anonymous)
├── Layout/
│   ├── MainLayout.razor         ← MudLayout with side nav drawer
│   └── NavMenu.razor            ← AuthorizeView for role-conditional links
├── Pages/
│   ├── Admin/
│   │   ├── AdminUsersPage.razor    ← /admin/users  [Authorize(Roles="Admin")]
│   │   └── AdminEventsPage.razor   ← /admin/events [Authorize(Roles="Admin")]
│   ├── Organizer/
│   │   ├── EventsDashboard.razor   ← /events       [Authorize]
│   │   └── EventDetail.razor       ← /events/{id}  [Authorize]
│   └── Public/
│       └── RsvpPage.razor          ← /rsvp         (anonymous)
└── Shared/
    ├── StatusChip.razor         ← Maps status strings to MudChip colours
    ├── PaginationBar.razor      ← Wraps MudPagination, emits page-changed event
    └── ErrorMessage.razor       ← Maps HTTP status codes to user-friendly MudAlert text
```

### 4.3 Routes

| Path | Component | Auth |
| --- | --- | --- |
| `/events` | `EventsDashboard` | `[Authorize]` (Organizer or Admin) |
| `/events/{id:guid}` | `EventDetail` | `[Authorize]` (Organizer or Admin) |
| `/admin/users` | `AdminUsersPage` | `[Authorize(Roles="Admin")]` |
| `/admin/events` | `AdminEventsPage` | `[Authorize(Roles="Admin")]` |
| `/rsvp` | `RsvpPage` | Anonymous |

---

## 5. Authentication

### 5.1 MSAL Setup

`Microsoft.Authentication.WebAssembly.Msal` is configured in `Program.cs` via `AddMsalAuthentication`. All pages except `RsvpPage` are protected.

```csharp
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add(/* api scope */);
});
```

`wwwroot/appsettings.json` holds the Entra ID SPA app registration coordinates:

```json
{
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/{tenantId}",
    "ClientId": "{spa-client-id}",
    "ValidateAuthority": true
  },
  "ApiBaseUrl": "https://<api-hostname>/api"
}
```

### 5.2 Entra ID SPA App Registration

A separate app registration is required for the frontend (SPA platform, no client secret):

- Platform: Single-page application
- Redirect URIs: `https://localhost:{port}/authentication/login-callback` (dev) + production URI
- API permissions: grant access to the scope exposed by the API app registration

### 5.3 Role-conditional navigation

`NavMenu.razor` uses `<AuthorizeView Roles="Admin">` to show admin links only to users with the Admin app role. Pages enforce roles independently via `[Authorize]` attributes.

### 5.4 HTTP clients

| Client | Handler | Purpose |
| --- | --- | --- |
| `EventApiClient` | `AuthorizationMessageHandler` | `/api/events/**` |
| `AdminApiClient` | `AuthorizationMessageHandler` | `/api/admin/**` |
| `InvitationApiClient` (organizer) | `AuthorizationMessageHandler` | `/api/events/{id}/invitations/**` |
| `InvitationApiClient` (RSVP) | None | `POST /api/invitations/respond` |

The anonymous RSVP client is registered separately to avoid attaching a Bearer token to a public endpoint.

---

## 6. Pages

### 6.1 Organizer Events Dashboard (`/events`)

- `MudDataGrid` with server-side pagination (`PagedResult<EventSummaryDto>`).
- Filter bar: status dropdown, date range pickers, text search by title.
- Columns: Title, Status chip, DateTime, Location, Capacity, Accepted/Pending/Total invited.
- Row actions: Publish, Cancel, Open (navigate to EventDetail).
- Empty state, loading skeleton, and error alert handled explicitly.

### 6.2 Event Detail (`/events/{id}`)

- Event header: Title, Status chip, DateTime, Location, Capacity.
- Edit form for Draft/Published events; inline validation.
- Action buttons: Publish (Draft only), Cancel (Draft or Published).
- **Invitations tab** (`MudTable`):
  - Columns: Email, Status chip, Sent At, Responded At.
  - Row action: Reissue (visible only for Pending status).
  - Send Invitation dialog: email input, submit, success/error feedback.

### 6.3 Admin Users (`/admin/users`)

- Paginated table with search.
- Columns: Display Name, Email, Organizer role toggle, Admin badge.
- Assign/Remove Organizer role with confirmation.

### 6.4 Admin Events (`/admin/events`)

- Read-only paginated table using `AdminEventSummaryDto`.
- No edit actions — Admin view only.

### 6.5 Public RSVP Page (`/rsvp`)

Token is read from the query string: `/rsvp?token=<value>`.

| State | Trigger | UI |
| --- | --- | --- |
| Loading | Initial | Spinner |
| Ready | Token + event summary loaded | Event summary, Accept / Decline buttons |
| Success | 204 response | Confirmation message |
| Conflict | 409 response | "Already responded" message; non-retriable |
| Invalid / Expired | 400 response | "Link is invalid or has expired; contact the organizer" |
| Network error | Exception | Generic error alert with retry option |

---

## 7. CORS

`EventHub.Api` adds a CORS policy `"WebFrontend"` allowing the frontend origin (configured via `Cors:AllowedOrigin`). Applied to all API routes; the origin value differs per environment.

---

## 8. Deployment

The Blazor WASM app compiles to static files (`dotnet publish` output). Hosted on **Azure Static Web Apps** — simplest zero-config hosting for static bundles with a free tier suitable for this project.

A new Bicep module (`infra/bicep/modules/static-web-app.bicep`) and GitHub Actions workflow (`deploy-web.yml`) are added alongside the existing API and infrastructure workflows.

---

## 9. Local Development

For local development, the API supports `Authentication:Mode = DevJwt`. The WASM app always goes through MSAL, so the recommended approach is:

- Use a real Azure Entra ID **development tenant** (free) for frontend authentication during local development.
- Configure `wwwroot/appsettings.Development.json` with local redirect URIs and the dev tenant coordinates.
- The API is pointed to `https://localhost:5165` via `ApiBaseUrl` in the local appsettings.

Full setup steps will be documented in `docs/operations/local-development.md` when the frontend project is scaffolded.
