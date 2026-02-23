# Architecture Overview

| | |
|---|---|
| **Status** | Draft |
| **Date** | 2026-02-23 |
| **Version** | 0.1 |

---

## 1. System Context

EventHub is a small-scale, invite-only event management platform built as a training application demonstrating production-grade architectural patterns. It exposes a REST API consumed initially by HTTP clients, with a React frontend planned for v2.

```
┌──────────────────────────────────────────────────────────────────┐
│                         External                                  │
│                                                                   │
│  HTTP Client          Azure Entra ID         ACS Email           │
│  (Postman / React)    (Auth / Roles)         (Delivery)          │
└───────┬──────────────────────┬──────────────────────┬────────────┘
        │ HTTPS                │ JWT validation        │ REST
        ▼                      ▼                       ▼
┌───────────────────┐   ┌──────────────┐   ┌─────────────────────┐
│  EventHub.Api     │   │  Entra ID    │   │  EventHub.          │
│  (App Service)    │   │  Tenant      │   │  Notifications      │
│                   │   └──────────────┘   │  (Azure Functions)  │
└───────┬───────────┘                      └──────────┬──────────┘
        │ MediatR                                     │ ServiceBusTrigger
        ▼                                             │ TimerTrigger
┌───────────────────┐                      ┌──────────▼──────────┐
│  Application      │                      │  Azure Service Bus  │
│  (CQRS handlers,  │ ─── publishes ──────▶│  Topic:             │
│   domain events,  │   (via Outbox)       │  "notifications"    │
│   outbox write)   │                      └─────────────────────┘
└───────┬───────────┘
        │
        ▼
┌───────────────────┐
│  Domain           │
│  (Entities,       │
│   Domain Events,  │
│   Value Objects)  │
└───────┬───────────┘
        │
        ▼
┌───────────────────┐
│  Infrastructure   │
│  (EF Core,        │
│   SQL Server,     │
│   Repositories,   │
│   Outbox)         │
└───────────────────┘
        │
        ▼
┌───────────────────┐
│  Azure SQL        │
│  (App data +      │
│   Outbox table)   │
└───────────────────┘
```

---

## 2. Technology Stack

| Layer | Technology | Notes |
|---|---|---|
| API | ASP.NET Core 10, Minimal APIs / Controllers | Hosted on Azure App Service |
| Auth (Admin/Organizer) | Azure Entra ID, JWT Bearer | App Roles: `Admin`, `Organizer` |
| Auth (Participant) | HMAC-SHA256 Magic Link token | Single-use, 72h expiry, public endpoint — see ADR 0006 |
| Application | MediatR, FluentValidation | CQRS, pipeline behaviours |
| Domain | Plain C# — no framework dependencies | Rich domain model, domain events |
| ORM | Entity Framework Core 10 | Code-first migrations |
| Database | Azure SQL (SQL Server) | One database, multiple schemas |
| Messaging | Azure Service Bus (Topics + Subscriptions) | Topic: `notifications` |
| Functions | Azure Functions v4 (.NET Isolated) | Timer + ServiceBus triggers |
| Email | Azure Communication Services Email | Transactional email delivery |
| IaC | Azure Bicep | `infra/bicep/` |
| CI/CD | GitHub Actions | `.github/workflows/` |
| Testing | xUnit, FluentAssertions, Testcontainers | Unit, Integration, Functional |

---

## 3. Solution Structure

```
src/
  backend/
    EventHub.Api/              ← HTTP entry point, DI composition root
    EventHub.Application/      ← Commands, Queries, Handlers, DTOs
    EventHub.Domain/           ← Entities, Value Objects, Domain Events
    EventHub.Infrastructure/   ← EF Core, Repositories, Outbox, Email stub
  notifications/
    EventHub.Notifications/    ← Azure Functions (Timer + ServiceBus triggers)
  frontend/                    ← React app (v2, placeholder)

tests/
  EventHub.Domain.UnitTests/
  EventHub.Application.UnitTests/
  EventHub.Infrastructure.IntegrationTests/
  EventHub.Api.FunctionalTests/

infra/
  bicep/
    main.bicep
    modules/
    environments/

docs/
  requirements/
  architecture/
  operations/
```

---

## 4. Layer Responsibilities

### EventHub.Domain
- **Entities and Aggregates**: `Event`, `Invitation`, `ApplicationUser`
- **Value Objects**: `EventStatus`, `InvitationStatus`
- **Domain Events**: `EventCancelled`, `InvitationSent`, `InvitationResponded`
- **Outbox Entity**: `OutboxMessage`
- No dependency on any infrastructure or application framework.

### EventHub.Application
- **Commands** and **Queries** (MediatR `IRequest<T>`)
- **Handlers**: one handler per command/query, one responsibility
- **Domain Event Handlers**: react to domain events — e.g., write `OutboxMessage` on `InvitationSent`
- **Interfaces**: `IEventRepository`, `IInvitationRepository`, `IOutboxRepository`, `IUnitOfWork`
- **Validation**: FluentValidation validators, registered as MediatR pipeline behaviours
- No dependency on EF Core, Azure SDKs, or HTTP.

### EventHub.Infrastructure
- **DbContext** (`EventHubDbContext`) and all EF Core entity configurations
- **Concrete repositories** implementing application interfaces
- **Unit of Work** wrapping `DbContext.SaveChangesAsync()`
- **EF Core Migrations**
- No business logic.

### EventHub.Api
- **Controllers / Endpoint definitions**: map HTTP verbs to MediatR commands/queries
- **DI registration**: wires all layers together
- **Middleware**: exception handling, authentication, authorisation
- **appsettings**: connection strings, Service Bus config (env-specific via App Service config)

### EventHub.Notifications (Azure Functions)
- **`ProcessOutboxFunction`** (Timer trigger, every 10s): reads unpublished `OutboxMessage` rows, publishes to Service Bus topic, marks as published.
- **`SendEmailFunction`** (ServiceBus trigger, subscription `email`): receives message, calls ACS Email.
- Has its own DI setup and `host.json`; shares domain message contracts via a shared models assembly or NuGet package (v2).

---

## 5. Key Architectural Patterns

| Pattern | Where Applied | Why |
|---|---|---|
| Clean Architecture / Layered | Entire solution | Separation of concerns, testability |
| CQRS | Application layer | Separate read/write models, scalability |
| MediatR | Application layer | Decoupled handlers, pipeline behaviours |
| Domain Events | Domain → Application | Behaviour encapsulated in domain, side-effects in handlers |
| Outbox Pattern | Infrastructure + Functions | Reliable exactly-once message publish with no dual-write |
| Repository + Unit of Work | Infrastructure | Testable data access, transaction control |
| Pub/Sub (Topics) | Service Bus | Extensible notification fan-out without coupling |
| Magic Link / Signed Token | API — public RSVP endpoint | Guest access without account registration; HMAC-SHA256 integrity |

---

## 6. Cross-Cutting Concerns

| Concern | Approach |
|---|---|
| Validation | FluentValidation + MediatR `ValidationBehaviour` pipeline |
| Error handling | Global exception middleware, Problem Details (RFC 7807) |
| Logging | `ILogger<T>`, structured logging to Application Insights |
| Configuration | `appsettings.json` + environment overrides + Azure App Config (future) |
| Secrets | Azure Key Vault referenced from App Service / Functions |
| Testing | Unit (Domain/Application), Integration (Infrastructure, Testcontainers), Functional (API) |
