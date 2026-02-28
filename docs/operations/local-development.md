# Local Development Guide

| | |
|---|---|
| **Updated** | 2026-02-28 |

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

## 3. Authentication

The API supports two authentication modes via `Authentication:Mode`:

- `DevJwt` (local development; default in `appsettings.Development.json`)
- `AzureAd` (real Entra ID integration)

### Option A: Local development with `dotnet user-jwts`

This mode does not require Azure app registration and is recommended while developing endpoints.

1. Ensure `Authentication:Mode` is `DevJwt` in `appsettings.Development.json`.
2. Generate a token with role + user id claims:

```bash
dotnet user-jwts create --project src/backend/EventHub.Api --role Organizer --claim "oid=dev-user-1" --output token
```

3. Use the token as a Bearer token for protected endpoints:

```bash
curl -H "Authorization: Bearer <PASTE_TOKEN_HERE>" http://localhost:5165/api/events
```

Useful commands:

```bash
dotnet user-jwts list --project src/backend/EventHub.Api
dotnet user-jwts remove --project src/backend/EventHub.Api --all
```

### Option B: Real Azure Entra ID

Set `Authentication:Mode` to `AzureAd` and configure:

```json
{
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0",
    "Audience": "api://<app-client-id>"
  }
}
```

Then request an access token for your API and use it as a Bearer token.

> **Note:** The API accepts both v1.0 (`sts.windows.net`) and v2.0 (`login.microsoftonline.com`) tokens. No manifest change is required.

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

### API request files (`.http`)

Use VS Code REST Client support to run requests directly from:

- [src/backend/EventHub.Api/EventHub.Api.http](../../src/backend/EventHub.Api/EventHub.Api.http) — full endpoint catalog (including negative checks)
- [src/backend/EventHub.Api/EventHub.Api.happy-path.http](../../src/backend/EventHub.Api/EventHub.Api.happy-path.http) — step-by-step end-to-end flow

Before running requests:

1. Ensure the API is running (`dotnet run` in `src/backend/EventHub.Api`).
2. Generate a local JWT (`dotnet user-jwts create ...`) and set `@bearerToken`.
3. For RSVP requests, set `@rawToken` from your invitation delivery/log source.
4. Run create/send requests first so `eventId` and `invitationId` variables are auto-populated.

### Targeting the dev Azure environment

Use [src/backend/EventHub.Api/EventHub.Api.dev.http](../../src/backend/EventHub.Api/EventHub.Api.dev.http) which points to `https://eventhub-dev-api.azurewebsites.net`.

Get a token via Postman (recommended) or by signing in with a dev user account that has an app role assigned.

**Postman OAuth2 setup** (collection → Authorization tab):

| Field | Value |
|---|---|
| Type | OAuth 2.0 |
| Grant Type | Authorization Code (with PKCE) |
| Auth URL | `https://login.microsoftonline.com/8dd52aee-fd49-4e5c-ace3-0a0e907b0529/oauth2/v2.0/authorize` |
| Access Token URL | `https://login.microsoftonline.com/8dd52aee-fd49-4e5c-ace3-0a0e907b0529/oauth2/v2.0/token` |
| Client ID | `60e56d3c-0d3d-4262-8fe3-4edb3307dc8e` (`eventhub-dev-client`) |
| Scope | `api://09af58ae-9706-469f-8dfe-c913428505fd/access_as_user` |
| Callback URL | `https://oauth.pstmn.io/v1/callback` |
| Client authentication | Send client credentials in body |

Click **Get New Access Token**, sign in as a dev user, then paste the token into `@bearerToken` in `EventHub.Api.dev.http`.

> **Important:** Never commit real tokens. `@bearerToken` in `EventHub.Api.dev.http` reads from `{{$dotenv EVENTHUB_BEARER_TOKEN}}`. Copy `.env.example` to `.env` (gitignored) and paste your token there — it will never be committed.

**Dev test users** (app roles assigned in Entra ID → Enterprise applications → EventHub API → Users and groups):

| User | Role | UPN |
|---|---|---|
| EventHub Organizer Dev | Organizer | `eh-organizer-dev@globaltradedemo.onmicrosoft.com` |

To test with multiple roles, sign in with the corresponding account in Postman's token dialog (use a private browser window to switch accounts).

**Generating tokens for multiple local personas:**

```bash
# Organizer
dotnet user-jwts create --project src/backend/EventHub.Api --role Organizer --claim "oid=user-organizer-1" --output token

# Admin
dotnet user-jwts create --project src/backend/EventHub.Api --role Admin --claim "oid=user-admin-1" --output token

# No role (expect 403 on protected endpoints)
dotnet user-jwts create --project src/backend/EventHub.Api --claim "oid=user-guest-1" --output token
```

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
| `401 Unauthorized` on all requests | Check `Authentication:Mode` in `appsettings.Development.json`; for DevJwt verify token hasn't expired (`dotnet user-jwts` tokens last 1 hour by default) |
| `401` against dev Azure environment | Token expired — get a fresh token via Postman OAuth2 flow |
| `403 Forbidden` against dev Azure environment | User does not have an app role assigned — add via Entra ID → Enterprise applications → EventHub API → Users and groups |
| `Health check sql Unhealthy` | SQL Server container is not running locally, or (Azure) the Key Vault reference for `DefaultConnection` failed to resolve — check App Service Configuration |
| `OutboxMessages` not being published | Ensure `ProcessOutboxFunction` is running (`func start` in notifications project) |
| `Failed to connect to Service Bus` | Check `ServiceBusConnectionString` in `local.settings.json` or user secrets |
| EF migrations out of date | Run `dotnet ef database update` after pulling changes that include new migrations |
