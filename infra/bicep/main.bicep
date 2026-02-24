@description('Base workload name, e.g. "eventhub".')
@maxLength(40)
param baseName string

@description('Environment moniker: "dev", "test", or "prod".')
@allowed(['dev', 'test', 'prod'])
param environment string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('App Service plan SKU name.')
@allowed(['F1', 'B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P0v3', 'P1v3', 'P2v3', 'P3v3'])
param skuName string = 'B1'

@description('Number of workers for the App Service plan.')
@minValue(1)
param skuCapacity int = 1

@description('Linux runtime stack in <RUNTIME>|<VERSION> format.')
param linuxFxVersion string = 'DOTNETCORE|10.0'

@description('Additional app settings as an array of {name, value} objects.')
param appSettings array = []

@description('Connection strings (each object requires name, type, value).')
param connectionStrings array = []

@description('Extra tags merged into the resource defaults.')
param extraTags object = {}

@description('Name of the SQL server.')
param sqlServerName string

@description('Name of the SQL database.')
param sqlDatabaseName string

@description('SQL administrator username.')
param sqlAdminUser string = 'sqladmin'

@description('Name of the Key Vault.')
// Simpler, unique, and more readable Key Vault name: baseName-env-kv-xxxxxx
param keyVaultName string = toLower('${take(baseName, 8)}-${environment}-kv-${take(uniqueString(resourceGroup().id), 6)}')

// ── Variables ─────────────────────────────────────────────────────────────────

// Deterministic password derived from the resource group — stable across re-deployments
// and never stored in source control. It is written to Key Vault on every deploy.
var sqlPasswordSalt = 'sql-admin'
var sqlAdminPasswordGenerated = 'P${uniqueString(resourceGroup().id, sqlPasswordSalt)}Qq1!'

// ── Modules ──────────────────────────────────────────────────────────────────

module plan 'modules/appServicePlan.bicep' = {
  name: 'appServicePlan'
  params: {
    baseName: baseName
    environment: environment
    location: location
    skuName: skuName
    skuCapacity: skuCapacity
    extraTags: extraTags
  }
}

module api 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    baseName: baseName
    environment: environment
    location: location
    appServicePlanId: plan.outputs.appServicePlanId
    linuxFxVersion: linuxFxVersion
    alwaysOn: skuName != 'F1'
    appSettings: appSettings
    connectionStrings: connectionStrings
    extraTags: extraTags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    sqlServerName: sqlServerName
    sqlDbName: sqlDatabaseName
    sqlAdminUser: sqlAdminUser
    sqlAdminPassword: sqlAdminPasswordGenerated
    location: location
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    keyVaultName: keyVaultName
    location: location
    secrets: {
      'sql-admin-password': sqlAdminPasswordGenerated
    }
  }
}


// ── Variables for Outputs ─────────────────────────────────────────────────────

var sqlAdminPasswordSecretObj = filter(keyVault.outputs.secretUris, s => s.name == 'sql-admin-password')

output appServicePlanName string = plan.outputs.appServicePlanName
output appServicePlanId string = plan.outputs.appServicePlanId
output webAppName string = api.outputs.webAppName
output webAppId string = api.outputs.webAppId
output webAppDefaultHostName string = api.outputs.webAppDefaultHostName
output webAppPrincipalId string = api.outputs.webAppPrincipalId
output sqlServerName string = sql.outputs.sqlServerName
output sqlDatabaseName string = sql.outputs.sqlDbName
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output keyVaultName string = keyVault.outputs.keyVaultName
output keyVaultUri string = keyVault.outputs.keyVaultUri
output sqlAdminPasswordSecretUri string = length(sqlAdminPasswordSecretObj) > 0 ? sqlAdminPasswordSecretObj[0].uri : ''
