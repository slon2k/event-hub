# Deployment Runbook

This document describes how to deploy the EventHub platform infrastructure and API to Azure.

## Prerequisites

| Tool | Minimum version | Install |
|---|---|---|
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
|---|---|---|
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

### Manual

```bash
# Replace <env> with dev | test | prod
az deployment group create \
  --resource-group eventhub-<env>-rg \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/<env>/main.bicepparam
```

### Preview changes before deploying

```bash
az deployment group what-if \
  --resource-group eventhub-<env>-rg \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/<env>/main.bicepparam
```

---

## API Deployment

### Automatic (recommended)

API deploys automatically via [deploy-api.yml](../../.github/workflows/deploy-api.yml):

- Push to `development` → builds, tests, deploys to **dev**
- Push to `master` → builds, tests, deploys to **test**
- Manual trigger (GitHub Actions UI, `master` branch only) → deploys to **prod**

### Manual

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

### GitHub Actions OIDC Setup

Required once per repository. See [Azure OIDC documentation](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect) for details.

1. Create an app registration in Entra ID
2. Create a service principal: `az ad sp create --id <appId>`
3. Add federated credentials for each GitHub environment (`dev`, `test`, `prod`) and pull requests
4. Assign the Contributor role to the service principal on your subscription
5. Add repository secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`

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
|---|---|---|
| `Failed to parse .bicepparam` | Azure CLI < 2.47 | `az upgrade` |
| `No matching federated identity record` | OIDC subject mismatch | Check federated credential subject matches the GitHub environment name |
| `No subscriptions found` | Service principal missing role assignment | Assign Contributor role on the subscription |
| `always_on cannot be set for Free tier` | `alwaysOn: true` on F1 plan | Bicep sets `alwaysOn` automatically based on SKU — ensure `skuName = 'F1'` |
