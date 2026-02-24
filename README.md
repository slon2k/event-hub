# EventHub

A training application demonstrating production-grade patterns at small scale: a simple invite-only event management system built with .NET 10, Clean Architecture, CQRS, the Outbox pattern, Azure Service Bus, and Azure Functions.

## What It Demonstrates

| Pattern / Technology | Where |
|---|---|
| Clean Architecture (Domain / Application / Infrastructure / API) | `src/backend/` |
| CQRS with MediatR | `EventHub.Application` |
| Rich Domain Model with Domain Events | `EventHub.Domain` |
| Outbox Pattern (reliable messaging, no dual-write) | `EventHub.Infrastructure` + `EventHub.Notifications` |
| Azure Service Bus (Topics + Subscriptions) | Notification pipeline |
| Azure Functions (Timer trigger + ServiceBus trigger) | `src/notifications/EventHub.Notifications` |
| Azure Communication Services Email | Transactional email delivery |
| Azure Entra ID (JWT Bearer + App Roles) | Authentication & Authorization |
| Infrastructure as Code (Azure Bicep) | `infra/bicep/` |
| CI/CD (GitHub Actions + OIDC) | `.github/workflows/` |

## Repository Structure

```
├── .github/
│   └── workflows/
│       ├── deploy-api.yml          # Build, test and deploy the API
│       └── deploy-infra.yml        # Deploy Azure infrastructure
├── docs/
│   ├── requirements/
│   │   └── functional-requirements.md
│   ├── architecture/
│   │   ├── overview.md             # System diagram and layer descriptions
│   │   ├── domain-model.md         # Entities, aggregates, domain events
│   │   ├── notification-flow.md    # Outbox → Service Bus → Functions → Email
│   │   └── adr/                    # Architecture Decision Records
│   └── operations/
│       ├── deployment.md           # Deployment runbook
│       └── local-development.md    # Local setup guide
├── infra/
│   └── bicep/                      # Azure Bicep templates
├── src/
│   ├── backend/
│   │   ├── EventHub.Api/           # ASP.NET Core Web API
│   │   ├── EventHub.Application/   # CQRS handlers, DTOs, interfaces
│   │   ├── EventHub.Domain/        # Entities, domain events, outbox entity
│   │   └── EventHub.Infrastructure/# EF Core, repositories, migrations
│   └── notifications/
│       └── EventHub.Notifications/ # Azure Functions (Timer + ServiceBus)
└── tests/
    ├── EventHub.Api.FunctionalTests/
    ├── EventHub.Application.UnitTests/
    ├── EventHub.Domain.UnitTests/
    └── EventHub.Infrastructure.IntegrationTests/
```

## Getting Started

See [docs/operations/local-development.md](docs/operations/local-development.md) for full setup instructions including Docker, Entra ID config, Service Bus, and test data seeding.

### Quick start (API only)

```bash
dotnet restore
# Start SQL Server in Docker first — see local-development.md §2
dotnet ef database update --project src/backend/EventHub.Infrastructure --startup-project src/backend/EventHub.Api
dotnet run --project src/backend/EventHub.Api
```

### Run Azure Functions locally

```bash
cd src/notifications/EventHub.Notifications
func start
```

### Deploy infrastructure

See [infra/README.md](infra/README.md) or the [deployment runbook](docs/operations/deployment.md).

## CI/CD

| Workflow | Trigger | Target |
|---|---|---|
| Deploy Infrastructure | Push to `development` | dev |
| Deploy Infrastructure | Push to `master` | test |
| Deploy Infrastructure | Manual | prod |
| Deploy API | Push to `development` | dev |
| Deploy API | Push to `master` | test |
| Deploy API | Manual (master only) | prod |

Authentication to Azure uses OIDC — no long-lived secrets stored in GitHub.

## Documentation

| Document | Description |
|---|---|
| [Functional Requirements](docs/requirements/functional-requirements.md) | Actors, features, out-of-scope items |
| [Architecture Overview](docs/architecture/overview.md) | System diagram, tech stack, layer responsibilities |
| [Domain Model](docs/architecture/domain-model.md) | Entities, aggregates, domain events, enumerations |
| [Notification Flow](docs/architecture/notification-flow.md) | Outbox → Service Bus → Functions → ACS Email |
| [Local Development](docs/operations/local-development.md) | Prerequisites, setup, running tests |
| [Deployment Runbook](docs/operations/deployment.md) | Manual and automated deployment steps |

## Architecture Decisions

| # | Decision |
|---|---|
| [ADR 0001](docs/architecture/adr/0001-infrastructure-as-code.md) | Infrastructure as Code with Azure Bicep |
| [ADR 0002](docs/architecture/adr/0002-cqrs-mediatr.md) | CQRS with MediatR |
| [ADR 0003](docs/architecture/adr/0003-outbox-pattern.md) | Outbox Pattern for Reliable Messaging |
| [ADR 0004](docs/architecture/adr/0004-azure-service-bus.md) | Azure Service Bus over Azure Storage Queues |
| [ADR 0005](docs/architecture/adr/0005-azure-entra-id.md) | Azure Entra ID for Authentication and Authorization |
| [ADR 0006](docs/architecture/adr/0006-magic-link-guest-participants.md) | Magic Link (Tokenized RSVP) for Guest Participants |
