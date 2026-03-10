#!/usr/bin/env bash
# grant-graph-permissions.sh
#
# Grants the three Microsoft Graph application permissions required by
# EntraIdentityAdminService to the App Service system-assigned managed identity.
#
# Permissions granted (app-only, require admin consent):
#   - User.Read.All                    (df021288-bdef-4463-88db-98f22de89214)
#   - Application.Read.All             (9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30)
#   - AppRoleAssignment.ReadWrite.All  (06b708a9-e830-4db3-a914-8e69da51d44f)
#
# Usage:
#   ./grant-graph-permissions.sh <resource-group> <webapp-name>
#
# Requirements:
#   - Azure CLI logged in with a principal that has:
#       - Application.Read.All (Graph)
#       - AppRoleAssignment.ReadWrite.All (Graph)
#     Both require admin consent (grant once via Azure Portal or az ad app permission admin-consent).
#
# Idempotent: existing assignments are detected and skipped.

set -euo pipefail

RESOURCE_GROUP="${1:?Usage: $0 <resource-group> <webapp-name>}"
WEBAPP_NAME="${2:?Usage: $0 <resource-group> <webapp-name>}"

# Well-known Microsoft Graph application ID (same in every tenant)
GRAPH_APP_ID="00000003-0000-0000-c000-000000000000"

# App role IDs for the three required permissions (stable across all tenants)
ROLE_USER_READ_ALL="df021288-bdef-4463-88db-98f22de89214"
ROLE_APPLICATION_READ_ALL="9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30"
ROLE_APPROLEASSIGNMENT_READWRITE_ALL="06b708a9-e830-4db3-a914-8e69da51d44f"

echo "==> Resolving managed identity for '$WEBAPP_NAME' in '$RESOURCE_GROUP'..."
API_PRINCIPAL=$(az webapp identity show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$WEBAPP_NAME" \
  --query principalId -o tsv)

if [ -z "$API_PRINCIPAL" ]; then
  echo "ERROR: No system-assigned managed identity found on '$WEBAPP_NAME'. Ensure the App Service has been deployed first."
  exit 1
fi
echo "    principalId: $API_PRINCIPAL"

echo "==> Resolving Microsoft Graph service principal..."
GRAPH_SP_ID=$(az ad sp show --id "$GRAPH_APP_ID" --query id -o tsv)
echo "    graphSpId: $GRAPH_SP_ID"

grant_role() {
  local ROLE_ID="$1"
  local ROLE_NAME="$2"

  # Check for an existing assignment
  EXISTING=$(az rest --method GET \
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/$GRAPH_SP_ID/appRoleAssignedTo?\$filter=principalId eq '$API_PRINCIPAL' and appRoleId eq '$ROLE_ID'" \
    --query "value[0].id" -o tsv 2>/dev/null || true)

  if [ -n "$EXISTING" ]; then
    echo "    [$ROLE_NAME] already assigned — skipping"
  else
    az rest --method POST \
      --url "https://graph.microsoft.com/v1.0/servicePrincipals/$GRAPH_SP_ID/appRoleAssignedTo" \
      --headers "Content-Type=application/json" \
      --body "{\"principalId\":\"$API_PRINCIPAL\",\"resourceId\":\"$GRAPH_SP_ID\",\"appRoleId\":\"$ROLE_ID\"}" \
      --output none
    echo "    [$ROLE_NAME] granted"
  fi
}

echo "==> Granting Graph app role assignments..."
grant_role "$ROLE_USER_READ_ALL"                   "User.Read.All"
grant_role "$ROLE_APPLICATION_READ_ALL"            "Application.Read.All"
grant_role "$ROLE_APPROLEASSIGNMENT_READWRITE_ALL" "AppRoleAssignment.ReadWrite.All"

echo "==> Done. All three Graph permissions are assigned to '$WEBAPP_NAME'."
