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

### Automatic deployment (recommended)

API deploys automatically via [deploy-api.yml](../../.github/workflows/deploy-api.yml):

- Push to `development` → builds, tests, deploys to **dev**
- Push to `master` → builds, tests, deploys to **test**
- Manual trigger (GitHub Actions UI, `master` branch only) → deploys to **prod**

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

Or pass them via the Bicep environment parameter files in `infra/bicep/environments/<env>/`.

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
| `RoleDefinitionDoesNotExist` | Wrong built-in role GUID | Run `az role definition list --name "Key Vault Secrets User" --query "[0].name" -o tsv` to get the correct GUID for your tenant |
| App Service connection string unresolved | KV reference not working | Check App Service identity is enabled, Key Vault RBAC role assignment exists, and secret name matches `sql-connection-string` |
