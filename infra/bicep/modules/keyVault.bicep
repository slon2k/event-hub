@description('Name of the Key Vault')
param keyVaultName string
@description('Location for Key Vault')
param location string = resourceGroup().location
@secure()
@description('Value for the sql-admin-password secret. Leave empty to skip secret creation.')
param sqlAdminPassword string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: []
    enableSoftDelete: true
    enablePurgeProtection: false
  }
}

resource sqlPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = if (!empty(sqlAdminPassword)) {
  parent: keyVault
  name: 'sql-admin-password'
  properties: {
    value: sqlAdminPassword
  }
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output sqlAdminPasswordSecretUri string = !empty(sqlAdminPassword) ? sqlPasswordSecret.properties.secretUri : ''
