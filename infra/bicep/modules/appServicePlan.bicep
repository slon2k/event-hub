@description('Base workload name, e.g. "eventhub".')
@maxLength(40)
param baseName string

@description('Environment moniker, e.g. "dev", "test", "prod".')
param environment string

@description('Azure region for all resources.')
param location string

@description('App Service plan SKU name. The tier is derived automatically.')
@allowed(['F1', 'B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P0v3', 'P1v3', 'P2v3', 'P3v3'])
param skuName string = 'B1'

@description('Number of workers for the plan.')
@minValue(1)
param skuCapacity int = 1

@description('Extra tags merged into the defaults.')
param extraTags object = {}

var normalizedEnvironment = toLower(environment)
var appServicePlanName = '${baseName}-${normalizedEnvironment}-plan'

var skuTierMap = {
  F1: 'Free'
  B1: 'Basic'
  B2: 'Basic'
  B3: 'Basic'
  S1: 'Standard'
  S2: 'Standard'
  S3: 'Standard'
  P0v3: 'PremiumV3'
  P1v3: 'PremiumV3'
  P2v3: 'PremiumV3'
  P3v3: 'PremiumV3'
}
var skuTier = skuTierMap[skuName]

var baseTags = {
  environment: normalizedEnvironment
  workload: 'webapi'
  managedBy: 'iac'
}
var finalTags = union(baseTags, extraTags)

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: skuName
    tier: skuTier
    capacity: skuCapacity
  }
  kind: 'linux'
  tags: finalTags
  properties: {
    reserved: true
  }
}

output appServicePlanName string = appServicePlan.name
output appServicePlanId string = appServicePlan.id
output skuName string = skuName
