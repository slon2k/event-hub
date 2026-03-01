@description('Base workload name, e.g. "eventhub".')
@maxLength(40)
param baseName string

@description('Environment moniker, e.g. "dev", "test", "prod".')
param environment string

@description('Azure region.')
param location string = resourceGroup().location

@description('Additional app settings as an array of {name, value} objects.')
param appSettings array = []

@description('Key Vault URI — used to build @Microsoft.KeyVault(...) references.')
param keyVaultUri string

@description('Key Vault secret URI for the Azure Service Bus connection string.')
param serviceBusConnectionStringSecretUri string

@description('Key Vault secret URI for the SQL Server connection string.')
param sqlConnectionStringSecretUri string

@description('Storage account name for managed identity access via managed identity.')
param storageAccountName string

@description('Application Insights connection string for telemetry. Leave empty to disable.')
param applicationInsightsConnectionString string = ''

@description('Extra tags merged into the defaults.')
param extraTags object = {}

var normalizedEnvironment = toLower(environment)
var functionAppName = '${baseName}-${normalizedEnvironment}-func'
var consumptionPlanName = '${baseName}-${normalizedEnvironment}-func-plan'

var baseTags = {
  environment: normalizedEnvironment
  workload: 'functions'
  managedBy: 'iac'
}
var finalTags = union(baseTags, extraTags)

// ── Consumption plan (Y1/Dynamic, Windows) ───────────────────────────────────
// Linux Consumption plans cannot be deployed to resource groups that already
// contain Windows-based App Service resources. Windows Consumption supports
// the .NET isolated worker runtime identically.

resource consumptionPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: consumptionPlanName
  location: location
  tags: finalTags
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

// ── Function App ──────────────────────────────────────────────────────────────

var defaultAppSettings = [
  // Runtime
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet-isolated'
  }
  // Service Bus
  {
    name: 'ServiceBusConnectionString'
    value: '@Microsoft.KeyVault(SecretUri=${serviceBusConnectionStringSecretUri})'
  }
  {
    name: 'ServiceBusTopicName'
    value: 'notifications'
  }
  {
    name: 'ServiceBusSubscriptionName'
    value: 'email'
  }
  // Outbox polling interval (every 10 seconds)
  {
    name: 'OutboxTimerCronExpression'
    value: '*/10 * * * * *'
  }
  // Key Vault URI (informational — used by any code that constructs KV references at runtime)
  {
    name: 'KeyVault__Uri'
    value: keyVaultUri
  }
]

// Storage — managed identity avoids the KV-reference bootstrapping race:
// the Functions runtime needs storage before KV refs resolve.
var storageSettings = [
  {
    name: 'AzureWebJobsStorage__accountName'
    value: storageAccountName
  }
  {
    name: 'AzureWebJobsStorage__credential'
    value: 'managedidentity'
  }
]

var aiSettings = !empty(applicationInsightsConnectionString)
  ? [
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsConnectionString
      }
    ]
  : []

var effectiveAppSettings = concat(defaultAppSettings, storageSettings, aiSettings, appSettings)

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: finalTags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: consumptionPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.3'
      appSettings: effectiveAppSettings
      connectionStrings: [
        {
          name: 'DefaultConnection'
          type: 'SQLAzure'
          connectionString: '@Microsoft.KeyVault(SecretUri=${sqlConnectionStringSecretUri})'
        }
      ]
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output functionAppName string = functionApp.name
output functionAppId string = functionApp.id
output functionAppDefaultHostName string = functionApp.properties.defaultHostName
output functionAppPrincipalId string = functionApp.identity.principalId
