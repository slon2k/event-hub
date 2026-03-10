#!/usr/bin/env bash
# grant-graph-permissions.sh
#
# Grants the three Microsoft Graph application permissions required by
# EntraIdentityAdminService to a dedicated Graph client app registration.
#
# ── Multi-directory setup ────────────────────────────────────────────────────
# This project uses two separate Entra ID tenants:
#   - Infrastructure tenant: hosts the Azure subscription, App Service, Key Vault
#   - Identity tenant:       hosts users, app registrations, and Graph data
#
# The App Service managed identity lives in the infrastructure tenant and cannot
# obtain Graph tokens scoped to the identity tenant directly. Instead, a dedicated
# app registration ("GraphClient") is created in the identity tenant and its
# credentials are stored in Key Vault. EntraIdentityAdminService uses
# ClientSecretCredential targeting the identity tenant.
#
# This script must be run logged in to the IDENTITY tenant (not the infrastructure
# tenant) by a Global Administrator of that tenant.
#
# Permissions granted (app-only, require admin consent in the identity tenant):
#   - User.Read.All                    (df021288-bdef-4463-88db-98f22de89214)
#   - Application.Read.All             (9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30)
#   - AppRoleAssignment.ReadWrite.All  (06b708a9-e830-4db3-a914-8e69da51d44f)
#
# Usage:
#   az login --tenant <identity-tenant-id>
#   ./grant-graph-permissions.sh <graph-client-app-id>
#
# Where <graph-client-app-id> is the Application (client) ID of the dedicated
# app registration created in the identity tenant for Graph calls.
#
# Idempotent: existing assignments are detected and skipped.

set -euo pipefail

GRAPH_CLIENT_APP_ID="${1:?Usage: $0 <graph-client-app-id>}"

# Well-known Microsoft Graph application ID (same in every tenant)
GRAPH_APP_ID="00000003-0000-0000-c000-000000000000"

# App role IDs for the three required permissions (stable across all tenants)
ROLE_USER_READ_ALL="df021288-bdef-4463-88db-98f22de89214"
ROLE_APPLICATION_READ_ALL="9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30"
ROLE_APPROLEASSIGNMENT_READWRITE_ALL="06b708a9-e830-4db3-a914-8e69da51d44f"

echo "==> Resolving service principal for Graph client app '$GRAPH_CLIENT_APP_ID'..."
CLIENT_SP_ID=$(az ad sp show --id "$GRAPH_CLIENT_APP_ID" --query id -o tsv)

if [ -z "$CLIENT_SP_ID" ]; then
  echo "ERROR: No service principal found for app '$GRAPH_CLIENT_APP_ID' in the current tenant."
  echo "       Ensure you are logged in to the identity tenant: az login --tenant <identity-tenant-id>"
  exit 1
fi
echo "    servicePrincipalId: $CLIENT_SP_ID"

echo "==> Resolving Microsoft Graph service principal..."
GRAPH_SP_ID=$(az ad sp show --id "$GRAPH_APP_ID" --query id -o tsv)
echo "    graphSpId: $GRAPH_SP_ID"

grant_role() {
  local ROLE_ID="$1"
  local ROLE_NAME="$2"

  # Check for an existing assignment
  EXISTING=$(az rest --method GET \
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/$GRAPH_SP_ID/appRoleAssignedTo?\$filter=principalId eq '$CLIENT_SP_ID' and appRoleId eq '$ROLE_ID'" \
    --query "value[0].id" -o tsv 2>/dev/null || true)

  if [ -n "$EXISTING" ]; then
    echo "    [$ROLE_NAME] already assigned — skipping"
  else
    az rest --method POST \
      --url "https://graph.microsoft.com/v1.0/servicePrincipals/$GRAPH_SP_ID/appRoleAssignedTo" \
      --headers "Content-Type=application/json" \
      --body "{\"principalId\":\"$CLIENT_SP_ID\",\"resourceId\":\"$GRAPH_SP_ID\",\"appRoleId\":\"$ROLE_ID\"}" \
      --output none
    echo "    [$ROLE_NAME] granted"
  fi
}

echo "==> Granting Graph app role assignments..."
grant_role "$ROLE_USER_READ_ALL"                   "User.Read.All"
grant_role "$ROLE_APPLICATION_READ_ALL"            "Application.Read.All"
grant_role "$ROLE_APPROLEASSIGNMENT_READWRITE_ALL" "AppRoleAssignment.ReadWrite.All"

echo "==> Done. All three Graph permissions are assigned to the Graph client app."
echo ""
echo "Next steps:"
echo "  1. Create a client secret on app '$GRAPH_CLIENT_APP_ID' in the identity tenant portal"
echo "  2. Store TenantId, ClientId, and ClientSecret in Key Vault (infrastructure tenant):"
echo "       graph-tenant-id     = <identity-tenant-id>"
echo "       graph-client-id     = $GRAPH_CLIENT_APP_ID"
echo "       graph-client-secret = <secret-value>"
