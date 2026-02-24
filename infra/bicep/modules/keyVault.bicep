@description('Name of the Key Vault')
param keyVaultName string
@description('Location for Key Vault')
param location string = resourceGroup().location

@description('Key-value pairs of secrets to create in the Key Vault. Example: { "sql-admin-password": "yourpass", "api-key": "yourkey" }')
@secure()
param secrets object = {}

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
  }
}


resource keyVaultSecrets 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = [for secretName in objectKeys(secrets): {
  parent: keyVault
  name: secretName
  properties: {
    value: secrets[secretName]
  }
}]


output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output secretUris array = [for (secretName, i) in objectKeys(secrets): {
  name: secretName
  uri: keyVaultSecrets[i].properties.secretUri
}]
