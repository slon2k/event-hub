@description('Name of the Key Vault')
param keyVaultName string

@description('Location for Key Vault')
param location string = resourceGroup().location

@description('Key-value pairs of secrets to create in the Key Vault. Example: { "sql-admin-password": "yourpass", "api-key": "yourkey" }')
@secure()
param secrets object = {}

@description('Enable purge protection. Irreversible once set — recommended for prod to prevent accidental permanent deletion.')
param enablePurgeProtection bool = false

@description('Principal IDs (object IDs) to grant the Key Vault Secrets User role. Typically includes the App Service managed identity.')
param secretsUserPrincipalIds array = []

var keyVaultSecretsUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e0' // Key Vault Secrets User (read-only)
)

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: enablePurgeProtection ? true : null
  }
}

resource keyVaultSecrets 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = [for secretName in objectKeys(secrets): {
  parent: keyVault
  name: secretName
  properties: {
    value: secrets[secretName]
  }
}]

resource secretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in secretsUserPrincipalIds: {
  scope: keyVault
  name: guid(keyVault.id, principalId, keyVaultSecretsUserRoleId)
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}]

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
#disable-next-line outputs-should-not-contain-secrets
output secretUris array = [for (secretName, i) in objectKeys(secrets): {
  name: secretName
  uri: keyVaultSecrets[i].properties.secretUri
}]
