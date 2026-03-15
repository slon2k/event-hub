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

@description('Principal IDs to grant storage roles to (for managed identity access from Functions).')
param functionAppPrincipalIds array = []

var baseTags = { workload: 'eventhub', managedBy: 'iac' }
var finalTags = union(baseTags, extraTags)

// Role definition IDs for storage access required by Azure Functions runtime
var storageBlobDataOwnerRoleId     = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
var storageQueueDataContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var storageTableDataContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')

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

// RBAC role assignments for managed identity access (one set per principal)
resource blobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in functionAppPrincipalIds: {
  scope: storageAccount
  name: guid(storageAccount.id, principalId, storageBlobDataOwnerRoleId)
  properties: {
    roleDefinitionId: storageBlobDataOwnerRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}]

resource queueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in functionAppPrincipalIds: {
  scope: storageAccount
  name: guid(storageAccount.id, principalId, storageQueueDataContributorRoleId)
  properties: {
    roleDefinitionId: storageQueueDataContributorRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}]

resource tableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in functionAppPrincipalIds: {
  scope: storageAccount
  name: guid(storageAccount.id, principalId, storageTableDataContributorRoleId)
  properties: {
    roleDefinitionId: storageTableDataContributorRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}]
