# EventHub

A .NET 10 web API platform deployed to Azure App Service, with full infrastructure-as-code and automated CI/CD via GitHub Actions.

## Repository Structure

```
├── .github/
│   └── workflows/
│       ├── deploy-api.yml        # Build, test and deploy the API
│       └── deploy-infra.yml      # Deploy Azure infrastructure
├── docs/
│   ├── architecture/
│   │   └── adr/                  # Architecture Decision Records
│   └── operations/
│       └── deployment.md         # Deployment runbook
├── infra/
│   └── README.md                 # Infrastructure documentation
├── src/
│   └── backend/
│       ├── EventHub.Api/         # ASP.NET Core Web API
│       ├── EventHub.Application/ # Application layer
│       ├── EventHub.Domain/      # Domain layer
│       └── EventHub.Infrastructure/ # Infrastructure layer
└── tests/
    ├── EventHub.Api.FunctionalTests/
    ├── EventHub.Application.UnitTests/
    ├── EventHub.Domain.UnitTests/
    └── EventHub.Infrastructure.IntegrationTests/
```

## Getting Started

### Run locally

```bash
dotnet restore
dotnet run --project src/backend/EventHub.Api
```

### Deploy infrastructure

See [infra/README.md](infra/README.md) for a full reference, or the [deployment runbook](docs/operations/deployment.md) for step-by-step instructions.

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

## Architecture Decisions

| # | Decision |
|---|---|
| [ADR 0001](docs/architecture/adr/0001-infrastructure-as-code.md) | Infrastructure as Code with Azure Bicep |
