# ADR 0007 — Blazor WebAssembly for the Web Frontend

| | |
| --- | --- |
| **Status** | Accepted |
| **Date** | 2026-03-15 |
| **Deciders** | Engineering team |

## Context

Stage 2 introduces a web frontend serving two distinct surfaces:

- An **authenticated dashboard** for Organizers and Admins (5 screens, Entra ID MSAL flow, CRUD tables, paginated lists).
- A **public RSVP page** for guest participants (no account required, magic-link token in the URL).

A frontend framework must be chosen. The application is .NET-first throughout: Clean Architecture, CQRS, EF Core, Azure. The frontend scope is focused: approximately 5 authenticated screens and 1 public page.

The primary candidates evaluated were **Blazor WebAssembly**, **React**, and **Angular**.

## Decision

We use **Blazor WebAssembly (standalone)** deployed as static files to Azure Static Web Apps.

Component library: **MudBlazor** (Material Design, covers all required table/pagination/chip/dialog patterns).

DTO types are shared via a new **`EventHub.Contracts`** class library, referenced by both `EventHub.Application` and `EventHub.Web` — no code generation required.

## Alternatives Considered

### React (Vite + TypeScript)

| | |
| --- | --- |
| **Ecosystem** | Largest; `@azure/msal-react`, React Query, broad community |
| **Type sharing** | Requires duplication or OpenAPI codegen tooling to mirror C# DTOs in TypeScript |
| **Fit for this scope** | The 5-screen scope does not exercise React's strengths (complex client state, real-time updates, performance-critical rendering); the context switch to TypeScript adds overhead without a proportionate benefit for this application |

React is a better fit for applications where rich client-side rendering patterns are the primary architectural concern. It is not chosen here because the overhead of maintaining TypeScript equivalents of C# types — via duplication or code generation — outweighs the benefit at this scope, and the team is exclusively .NET-focused.

### Angular

| | |
| --- | --- |
| **Ecosystem** | Mature; `@azure/msal-angular` provides route guards |
| **Boilerplate** | Module system, decorators, DI, and build configuration are justified at 20+ screens and large teams |
| **Fit for this scope** | Boilerplate-to-output ratio is poor at 5 screens; onboarding cost is highest of the three options |

Angular is not chosen. The application does not benefit from what Angular's opinionation protects against at this scale.

### Blazor Server

| | |
| --- | --- |
| **Deployment** | Requires a persistent server with SignalR connections |
| **Fit for this project** | Adds operational dependency (always-on server process, connection management); standalone WASM fits better with the existing static-first deployment model and aligns with the Azure Static Web Apps hosting target |

## Consequences

### Positive

- End-to-end C# — no context switch between the backend and frontend. A single developer can work across the full stack in one language and toolchain.
- DTO types shared at compile time via `EventHub.Contracts` — no drift between API response shapes and frontend consumption.
- `Microsoft.Authentication.WebAssembly.Msal` is the native .NET MSAL library for Blazor WASM — same library family and mental model as the backend's JWT Bearer configuration.
- MudBlazor covers all required UI primitives (data grids, pagination, chips, dialogs, alerts, skeletons) without requiring custom CSS work.
- Deployed as static files — no server-side hosting required for the frontend; integrates cleanly with Azure Static Web Apps.
- Architecturally cohesive: the entire stack uses a single language, toolchain, and library ecosystem, which reduces operational overhead and keeps focus on domain and infrastructure concerns.
- The public RSVP page requires zero special framework handling — anonymous routes with no auth wrapper work identically in all three frameworks.

### Negative / Trade-offs

- WASM initial download is larger than a comparable React bundle (~5–10 MB after .NET 8+ size improvements). Acceptable at this scale; would require evaluation in a high-traffic production scenario.
- Blazor ecosystem (component libraries, community Q&A, tutorials) is narrower than React's. Sufficient for this scope; may require more first-principles problem solving on edge cases.
- MSAL in WASM always communicates with a real Entra ID tenant. Local development requires either a dedicated dev tenant or a test-token bypass strategy. The API's existing `Authentication:Mode = DevJwt` does not eliminate this — see `docs/architecture/frontend-architecture.md §9`.
