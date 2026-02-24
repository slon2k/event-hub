# Infrastructure

This directory contains all infrastructure-as-code (IaC) for the EventHub platform, written in [Bicep](https://learn.microsoft.com/azure/azure-resource-manager/bicep/overview).

## Structure

```
infra/
├── bicep/
│   ├── main.bicep              # Root template — orchestrates all modules
│   ├── environments/
│   │   ├── dev/
│   │   │   └── main.bicepparam # Parameter values for the dev environment
│   │   ├── test/
│   │   │   └── main.bicepparam # Parameter values for the test environment
│   │   └── prod/
│   │       └── main.bicepparam # Parameter values for the prod environment
│   └── modules/
│       ├── appServicePlan.bicep # App Service plan resource
│       └── appService.bicep     # App Service (Web App) resource
└── scripts/                    # Ad-hoc operational scripts
```

## Resources Deployed

| Resource | Naming convention | Notes |
|---|---|---|
| App Service Plan | `{baseName}-{env}-plan` | Linux, SKU varies per environment |
| App Service (API) | `{baseName}-{env}-api` | .NET 10, zip deploy, run-from-package |

## Environments

| Environment | SKU | Capacity | Always On | Purpose |
|---|---|---|---|---|
| dev | F1 (Free) | 1 | No | Active development |
| test | F1 (Free) | 1 | No | Integration testing |
| prod | B1 (Basic) | 1 | Yes | Production workload |

## Deployment

See [docs/operations/deployment.md](../../docs/operations/deployment.md) for full deployment instructions.

### Quick deploy (manual)

Prerequisites: [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) 2.47+, logged in with `az login`.

```bash
# Deploy to dev
az deployment group create \
  --resource-group eventhub-dev-rg \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev/main.bicepparam

# Preview changes (what-if)
az deployment group what-if \
  --resource-group eventhub-dev-rg \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev/main.bicepparam
```

### CI/CD

Infrastructure is deployed automatically via GitHub Actions ([.github/workflows/deploy-infra.yml](../../.github/workflows/deploy-infra.yml)):

| Trigger | Target |
|---|---|
| Push to `development` | dev |
| Push to `master` | test |
| Manual dispatch | prod |
| Pull request | what-if preview only |

## Parameters Reference

| Parameter | Type | Default | Description |
|---|---|---|---|
| `baseName` | string | — | Base workload name (e.g. `eventhub`) |
| `environment` | string | — | Environment moniker: `dev`, `test`, or `prod` |
| `location` | string | resource group location | Azure region |
| `skuName` | string | `B1` | App Service plan SKU |
| `skuCapacity` | int | `1` | Number of plan workers |
| `linuxFxVersion` | string | `DOTNETCORE\|10.0` | Linux runtime stack |
| `appSettings` | array | `[]` | Extra app settings `{name, value}` objects |
| `extraTags` | object | `{}` | Additional Azure resource tags |

## Tagging Strategy

All resources receive the following base tags automatically:

| Tag | Value | Source |
|---|---|---|
| `environment` | `dev` / `test` / `prod` | Bicep module |
| `workload` | `webapi` | Bicep module |

Additional tags can be injected per environment via the `extraTags` parameter in `.bicepparam`.
