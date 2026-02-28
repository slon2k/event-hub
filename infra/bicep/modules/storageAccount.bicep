@description('Storage account name. Must be globally unique, 3-24 chars, lowercase alphanumeric only.')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Azure region.')
param location string = resourceGroup().location

@description('Storage SKU.')
@allowed(['Standard_LRS', 'Standard_GRS', 'Standard_ZRS', 'Premium_LRS'])
param sku string = 'Standard_LRS'

@description('Extra tags merged into the defaults.')
param extraTags object = {}

var baseTags = { workload: 'eventhub', managedBy: 'iac' }
var finalTags = union(baseTags, extraTags)

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: finalTags
  sku: { name: sku }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
#disable-next-line outputs-should-not-contain-secrets
output primaryConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
