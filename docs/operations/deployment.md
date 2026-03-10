# Deployment Runbook

This document describes how to deploy the EventHub platform infrastructure and API to Azure.

## Prerequisites

| Tool | Minimum version | Install |
| --- | --- | --- |
| Azure CLI | 2.47 | [Install guide](https://learn.microsoft.com/cli/azure/install-azure-cli) |
| Bicep CLI | 0.18 (bundled with Azure CLI) | `az bicep install` |
| Git | Any | — |

Log in to Azure before running any commands:

```bash
az login
az account set --subscription "<subscription-id-or-name>"
```

## Environments

| Environment | Resource Group | Branch |
| --- | --- | --- |
| dev | `eventhub-dev-rg` | `development` |
| test | `eventhub-test-rg` | `master` |
| prod | `eventhub-prod-rg` | `master` (manual only) |

---

## Infrastructure Deployment

### Automatic (recommended)

Infrastructure deploys automatically via [deploy-infra.yml](../../.github/workflows/deploy-infra.yml):

- Push to `development` → deploys to **dev**
- Push to `master` → deploys to **test**
- Manual trigger (GitHub Actions UI) → deploys to **prod**

### Manual infrastructure deployment

`SQL_ADMIN_PASSWORD` must be set as an environment variable before running — never hard-code it.

```powershell
# PowerShell — replace <env> with dev | test | prod
$env:SQL_ADMIN_PASSWORD = '<your-password>'

az deployment group create `
  --resource-group eventhub-<env>-rg `
  --template-file infra/bicep/main.bicep `
  --parameters infra/bicep/environments/<env>/main.bicepparam
```

### Preview changes before deploying

```powershell
$env:SQL_ADMIN_PASSWORD = '<your-password>'

az deployment group what-if `
  --resource-group eventhub-<env>-rg `
  --template-file infra/bicep/main.bicep `
  --parameters infra/bicep/environments/<env>/main.bicepparam
```

---

## API Deployment

### Automatic API deployment (recommended)

API deploys automatically via [deploy-api.yml](../../.github/workflows/deploy-api.yml):

- Push to `development` → builds, tests, runs DB migrations, deploys to **dev**
- Push to `master` → builds, tests, runs DB migrations, deploys to **test**
- Manual trigger (GitHub Actions UI, `master` branch only) → builds, tests, runs DB migrations, deploys to **prod**

### Manual API deployment

```bash
# Build and publish
dotnet publish src/backend/EventHub.Api/EventHub.Api.csproj \
  --configuration Release \
  --output publish/api

# Deploy (replace <env> with dev | test | prod)
az webapp deploy \
  --resource-group eventhub-<env>-rg \
  --name eventhub-<env>-api \
  --src-path publish/api \
  --type zip
```

---

## Functions Deployment

### Automatic Functions deployment (recommended)

Functions deploy automatically via [deploy-notifications.yml](../../.github/workflows/deploy-notifications.yml):

- Push to `development` → deploys to **dev**
- Push to `master` → deploys to **test**
- Manual trigger (GitHub Actions UI, `master` branch only) → deploys to **prod**

### Manual Functions deployment

```powershell
# Build and publish
dotnet publish src/notifications/EventHub.Notifications/EventHub.Notifications.csproj `
  --configuration Release `
  --output publish/notifications

# Package (must include hidden .azurefunctions/ directory)
Compress-Archive -Path publish\notifications\* -DestinationPath publish\notifications.zip -Force

# Deploy (replace <env> with dev | test | prod)
az functionapp deployment source config-zip `
  --resource-group eventhub-<env>-rg `
  --name eventhub-<env>-func `
  --src publish\notifications.zip `
  --build-remote false
```

> **Important:** Use `Compress-Archive` (or `zip -r`) — not `az webapp deploy`. The `az webapp deploy` action strips hidden directories including `.azurefunctions/`, which prevents function registration.

---

## First-Time Setup

If deploying to a brand-new subscription or resource group:

```bash
# Create resource groups
az group create --name eventhub-dev-rg  --location eastus
az group create --name eventhub-test-rg --location eastus
az group create --name eventhub-prod-rg --location eastus
```

Resource groups are also created automatically by the GitHub Actions workflows if they don't exist.

### Entra ID App Registration (per environment)

Create one app registration per environment (**dev**, **test**, **prod**). These are stable — create once, never recreate.

#### 1. Register the application

1. Go to **Azure Portal → Entra ID → App registrations → New registration**
2. Name: `EventHub-Api-<env>` (e.g. `EventHub-Api-dev`)
3. Supported account types: **Accounts in this organizational directory only (Single tenant)**
4. Redirect URI: leave blank — this is an API, not a web app
5. Click **Register**
6. Note the **Application (client) ID** and **Directory (tenant) ID** from the Overview page

#### 2. Expose an API (set Application ID URI)

1. In the app registration → **Expose an API**
2. Click **Add** next to Application ID URI
3. Accept the default: `api://<CLIENT_ID>`
4. Click **Save**

#### 3. Define App Roles

In **App roles → Create app role** — create two roles:

| Display name | Allowed member types | Value | Description |
| --- | --- | --- | --- |
| Organizer | Users/Groups | `Organizer` | Can create and manage events |
| Admin | Users/Groups | `Admin` | Full administrative access |

#### 4. Assign users to roles

1. Go to **Enterprise applications → EventHub-Api-\<env> → Users and groups → Add user/group**
2. Assign users or groups to `Organizer` or `Admin` as needed

#### 5. Configure the App Service

In **App Service → Configuration → Application settings**, add:

| Name | Value |
| --- | --- |
| `Authentication__Mode` | `AzureAd` |
| `AzureAd__Authority` | `https://login.microsoftonline.com/<TENANT_ID>/v2.0` |
| `AzureAd__Audience` | `api://<CLIENT_ID>` |
| `AzureAd__TenantId` | `<TENANT_ID>` |
| `AzureAd__ApiAppClientId` | `<CLIENT_ID>` |

Or pass them via the Bicep environment parameter files in `infra/bicep/environments/<env>/`. `AzureAd__TenantId` and `AzureAd__ApiAppClientId` are already set in the dev and test parameter files.

#### 6. Create a Graph client app registration and grant Graph permissions

> **Multi-directory note:** This project uses two separate Entra ID tenants — one for Azure infrastructure (subscription, App Service, Key Vault) and one for user identities (app registrations, users, groups). The App Service managed identity lives in the infrastructure tenant and cannot call Graph in a different tenant directly. A dedicated **Graph client app registration** in the identity tenant is used instead, with its credentials stored in Key Vault.

**Steps (done once per environment by a Global Administrator of the identity tenant):**

1. In the **identity tenant** → **Entra ID → App registrations → New registration**
   - Name: `EventHub-GraphClient-<env>` (e.g. `EventHub-GraphClient-dev`)
   - Supported account types: **Single tenant**
   - No redirect URI needed
2. Note the **Application (client) ID** from the Overview page
3. Go to **Certificates & secrets → New client secret** → create a secret and note the value
4. Grant the three Graph app-only permissions (requires Global Admin of the identity tenant):

   ```bash
   # Log in to the IDENTITY tenant (not the infrastructure tenant)
   az login --tenant <identity-tenant-id>

   bash infra/scripts/grant-graph-permissions.sh <graph-client-app-id>
   ```

   The script is idempotent — safe to run multiple times. After it completes, the following permissions appear under the app registration's **API permissions** with status **Granted for \<tenant\>**:

   | Permission | Purpose |
   | --- | --- |
   | `User.Read.All` | Query Entra users for `GET /api/admin/users` |
   | `Application.Read.All` | Resolve the API service principal and Organizer app role ID |
   | `AppRoleAssignment.ReadWrite.All` | Assign and remove the Organizer app role |

5. Add the three values as **GitHub Actions environment secrets** for each environment (`dev`, `test`, `prod`):

   | Secret name | Value |
   | --- | --- |
   | `GRAPH_TENANT_ID` | Identity tenant ID (e.g. `8dd52aee-...`) |
   | `GRAPH_CLIENT_ID` | Application (client) ID of `EventHub-GraphClient-<env>` |
   | `GRAPH_CLIENT_SECRET` | Client secret value from step 3 |

   The next `deploy-infra` run will write these into Key Vault automatically (same mechanism as `SQL_ADMIN_PASSWORD`). The App Service app settings `Graph__TenantId`, `Graph__ClientId`, and `Graph__ClientSecret` are wired to Key Vault references by Bicep — no manual portal steps needed.

#### Environment reference

| Environment | App Registration name | Notes |
| --- | --- | --- |
| dev | `EventHub-Api-dev` | Add tenant/client IDs here after creation |
| test | `EventHub-Api-test` | — |
| prod | `EventHub-Api-prod` | — |

---

### GitHub Actions OIDC Setup

Required once per repository. See [Azure OIDC documentation](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect) for details.

1. Create an app registration in Entra ID
2. Create a service principal: `az ad sp create --id <appId>`
3. Add federated credentials for each GitHub environment (`dev`, `test`, `prod`) and pull requests
4. Assign the **Contributor** role to the service principal on each resource group
5. Assign the **User Access Administrator** role to the service principal on each resource group (required to create Key Vault RBAC role assignments during deployment)

   ```powershell
   az role assignment create `
     --assignee-object-id <sp-object-id> `
     --assignee-principal-type ServicePrincipal `
     --role "User Access Administrator" `
     --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group>"
   ```

6. Add repository secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
7. Add the following secret to **each GitHub environment** (`dev`, `test`, `prod`):

| Secret | Description |
| --- | --- |
| `SQL_ADMIN_PASSWORD` | SQL Server administrator password. Must match the password used when the SQL Server was first created. |

---

## Rollback

### Infrastructure

Bicep deployments are incremental by default. To roll back, redeploy a previous commit:

```bash
git checkout <previous-commit>
az deployment group create \
  --resource-group eventhub-<env>-rg \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/<env>/main.bicepparam
```

### API

Swap back to a previous deployment slot or redeploy a previous build artifact from GitHub Actions run history.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| `Failed to parse .bicepparam` | Azure CLI < 2.47 | `az upgrade` |
| `No matching federated identity record` | OIDC subject mismatch | Check federated credential subject matches the GitHub environment name |
| `No subscriptions found` | Service principal missing role assignment | Assign Contributor role on the resource group |
| `always_on cannot be set for Free tier` | `alwaysOn: true` on F1 plan | Bicep sets `alwaysOn` automatically based on SKU — ensure `skuName = 'F1'` |
| `Authorization failed for roleAssignments/write` | Service principal missing User Access Administrator | Assign User Access Administrator on each resource group (see OIDC setup step 5) |
| Graph calls return `403 Forbidden` from `EntraIdentityAdminService` | Missing Graph app role assignments on the Graph client app | Run `az login --tenant <identity-tenant-id>` then `bash infra/scripts/grant-graph-permissions.sh <graph-client-app-id>` as a Global Administrator of the identity tenant (see First-Time Setup §6) |
| `EntraIdentityAdminService` returns `401` / token acquisition fails | `Graph__TenantId`, `Graph__ClientId`, or `Graph__ClientSecret` missing or wrong | Check Key Vault secrets exist and App Service KV references resolve under Configuration → Application settings |
| `RoleDefinitionDoesNotExist` | Wrong built-in role GUID | Run `az role definition list --name "Key Vault Secrets User" --query "[0].name" -o tsv` to get the correct GUID for your tenant |
| App Service connection string unresolved | KV reference not working | Check App Service identity is enabled, Key Vault RBAC role assignment exists, and secret name matches `sql-connection-string` |
| Function timer never fires, zero invocations | Storage RBAC not applied to managed identity | Verify the function app identity has `Storage Blob Data Owner`, `Storage Queue Data Contributor`, and `Storage Table Data Contributor` on the storage account. Bicep assigns these automatically — if applying to a pre-existing function app, redeploy infra or grant manually. |
| `'%...%' does not resolve to a value` (any binding expression) | App setting name uses double-underscore (`__`) | Azure Functions binding expressions do a **flat key lookup** — `__` is not treated as a section separator here (unlike `IConfiguration` in C# code). All settings referenced in `%...%` binding expressions must use simple names without `__`: `OutboxTimerCronExpression`, `ServiceBusTopicName`, `ServiceBusSubscriptionName`. |
| `SendEmailFunction` never fires, messages accumulate in Service Bus subscription | Service Bus trigger binding can't resolve topic/subscription name | Check that `ServiceBusTopicName` and `ServiceBusSubscriptionName` app settings exist (not `ServiceBus__TopicName` / `ServiceBus__SubscriptionName`). |
