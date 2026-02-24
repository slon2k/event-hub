# ADR 0001 — Infrastructure as Code with Azure Bicep

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-02-23 |
| **Deciders** | Engineering team |

## Context

The EventHub platform requires a repeatable, version-controlled way to provision and manage Azure resources across multiple environments (dev, test, prod). Manual portal-based provisioning is error-prone and cannot be audited or peer-reviewed.

## Decision

We use **Azure Bicep** as the IaC language, structured as a root template (`main.bicep`) composed of reusable modules under `infra/bicep/modules/`. Environment-specific values are provided via `.bicepparam` parameter files, one per environment.

## Alternatives Considered

| Option | Reason not chosen |
|---|---|
| ARM JSON templates | Verbose syntax, poor readability, no type safety |
| Terraform | Additional toolchain complexity; Bicep is first-class on Azure with no state file management required |
| Pulumi | Too heavyweight for the current team size and project scope |

## Consequences

### Positive
- Full audit trail — infrastructure changes are reviewed in PRs like application code
- Environment parity — all environments use identical modules with different parameter values
- What-if previews — `az deployment group what-if` surfaces changes before they are applied
- Native Azure integration — Bicep compiles to ARM, no third-party state backend required

### Negative / Trade-offs
- Bicep is Azure-specific — not portable to other cloud providers
- Azure CLI 2.47+ required to support `.bicepparam` files natively
