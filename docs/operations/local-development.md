# Local Development Guide

| | |
|---|---|
| **Updated** | 2026-02-23 |

---

## Prerequisites

| Tool | Minimum Version | Install |
|---|---|---|
| .NET SDK | 10.0 | [dot.net](https://dot.net) |
| Docker Desktop | Any recent | [docker.com](https://www.docker.com/products/docker-desktop) — required for Testcontainers and SQL Server |
| Azure Functions Core Tools | 4.x | `npm i -g azure-functions-core-tools@4 --unsafe-perm true` |
| Azure CLI | 2.47+ | [Install guide](https://learn.microsoft.com/cli/azure/install-azure-cli) |
| Git | Any | — |

Optional but recommended:

| Tool | Purpose |
|---|---|
| Service Bus Explorer | Inspect Service Bus messages and dead-letter queue |
| Azure Storage Explorer | Browse storage accounts / Azurite |
| EF Core CLI | `dotnet tool install --global dotnet-ef` |

---

## 1. Clone and Restore

```bash
git clone https://github.com/<org>/eventhub.git
cd eventhub
dotnet restore
```

---

## 2. Database — SQL Server via Docker

The API and integration tests use SQL Server. Run a local instance via Docker:

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name eventhub-sql -d \
  mcr.microsoft.com/mssql/server:2022-latest
```

Apply EF Core migrations to create the schema:

```bash
dotnet ef database update \
  --project src/backend/EventHub.Infrastructure \
  --startup-project src/backend/EventHub.Api
```

The connection string in `appsettings.Development.json` defaults to:

```
Server=localhost,1433;Database=EventHub;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
```

Override it via `dotnet user-secrets` if needed:

```bash
cd src/backend/EventHub.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-connection-string>"
```

---

## 3. Authentication — Azure Entra ID

The API validates JWT tokens issued by an Entra ID App Registration. For local development you have two options:

### Option A: Use a dev Entra ID tenant (recommended)

1. Ask the team for the **dev tenant App Registration** details (Client ID, Tenant ID).
2. Set the following in `appsettings.Development.json` or user secrets:

```json
{
  "AzureAd": {
    "TenantId": "<dev-tenant-id>",
    "ClientId": "<dev-app-client-id>",
    "Audience": "api://<dev-app-client-id>"
  }
}
```

3. Obtain a token using the Azure CLI:

```bash
az login --tenant <dev-tenant-id>
az account get-access-token --resource api://<dev-app-client-id> --query accessToken -o tsv
```

4. Use the token as a Bearer header in Postman / `.http` files.

### Option B: Bypass auth locally (dev-only flag)

> ⚠️ Never commit this setting or use it outside of local development.

In `appsettings.Development.json`:

```json
{
  "Auth": {
    "DisableForLocalDevelopment": true
  }
}
```

When this flag is set, the API accepts requests without a token and injects a fake user identity. The implementation is gated behind `#if DEBUG` / environment checks.

---

## 4. Service Bus — Local Development

Azure Service Bus has no complete local emulator. For local development:

### Option A: Use a shared dev Service Bus namespace (recommended)

Create a `dev` namespace in Azure (provisioned by Bicep) and share the connection string with the team via Key Vault or a shared `.env` file (not committed to git).

```bash
# Get the connection string from Azure
az servicebus namespace authorization-rule keys list \
  --resource-group eventhub-dev-rg \
  --namespace-name eventhub-dev-sb \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv
```

Set via user secrets (API):

```bash
dotnet user-secrets set "ServiceBus:ConnectionString" "<connection-string>"
```

Set in `src/notifications/EventHub.Notifications/local.settings.json` (not committed):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnectionString": "<connection-string>",
    "ServiceBus__TopicName": "notifications",
    "ServiceBus__SubscriptionName": "email"
  }
}
```

### Option B: Skip Service Bus locally

Comment out the `ProcessOutboxFunction` timer trigger and test the email function directly by publishing a test message manually via Service Bus Explorer.

---

## 5. Azure Communication Services Email

For local development, use **one of the following**:

| Approach | How |
|---|---|
| Real ACS dev resource | Use the connection string from the dev ACS resource in Key Vault |
| Stub / mock | Set `AcsEmail:UseStub=true` in `local.settings.json` — logs email content to the console instead of sending |

---

## 6. Run the API

```bash
cd src/backend/EventHub.Api
dotnet run
```

Swagger UI is available at: `https://localhost:5001/swagger`

Sample HTTP requests are in [src/backend/EventHub.Api/EventHub.Api.http](../../src/backend/EventHub.Api/EventHub.Api.http).

---

## 7. Run the Azure Functions

```bash
cd src/notifications/EventHub.Notifications
func start
```

Ensure `local.settings.json` is present (see §4 above).

---

## 8. Run Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/EventHub.Domain.UnitTests
dotnet test tests/EventHub.Application.UnitTests
dotnet test tests/EventHub.Infrastructure.IntegrationTests   # requires Docker
dotnet test tests/EventHub.Api.FunctionalTests               # requires Docker
```

Integration and functional tests use **Testcontainers** to spin up a SQL Server container automatically — Docker Desktop must be running.

---

## 9. Seeding Test Data

A development seed script is available to populate the database with sample users, events, and invitations:

```bash
dotnet run --project src/backend/EventHub.Api -- --seed
```

> This flag is only active when `ASPNETCORE_ENVIRONMENT=Development`.

---

## 10. Common Issues

| Problem | Solution |
|---|---|
| `Cannot open server requested by the login` | SQL Server container is not running — check `docker ps` |
| `401 Unauthorized` on all requests | Check Entra ID config or enable the local dev auth bypass flag |
| `OutboxMessages` not being published | Ensure `ProcessOutboxFunction` is running (`func start` in notifications project) |
| `Failed to connect to Service Bus` | Check `ServiceBusConnectionString` in `local.settings.json` or user secrets |
| EF migrations out of date | Run `dotnet ef database update` after pulling changes that include new migrations |
