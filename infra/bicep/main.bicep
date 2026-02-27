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

@secure()
@description('SQL administrator password. Must be supplied at deploy time — do not store in source control.')
param sqlAdminPassword string

@description('SQL database SKU. Use { name: "Basic", tier: "Basic" } for dev/test or { name: "S0", tier: "Standard" } for prod. Ignored when useFreeLimit is true.')
param sqlDatabaseSku object = { name: 'Basic', tier: 'Basic' }

@description('Use the Azure SQL free offer (up to 10 free databases per subscription). Provisions GP Serverless Gen5 2 vCores.')
param useFreeLimit bool = false

@description('Behaviour when the free limit is exhausted: AutoPause or BillOverUsage.')
@allowed(['AutoPause', 'BillOverUsage'])
param freeLimitExhaustionBehavior string = 'AutoPause'

@description('Enable Key Vault purge protection. Irreversible once set. Set true for prod to prevent permanent secret loss.')
param enablePurgeProtection bool = false

@description('Name of the Key Vault.')
// Simpler, unique, and more readable Key Vault name: baseName-env-kv-xxxxxx
param keyVaultName string = toLower('${take(baseName, 8)}-${environment}-kv-${take(uniqueString(resourceGroup().id), 6)}')

// ── Modules ──────────────────────────────────────────────────────────────────

// Build the Key Vault URI from the deterministic vault name.
// This avoids a circular dependency between the api and keyVault modules.
// Note: az.environment().suffixes.keyvaultDns already includes a leading dot (e.g. ".vault.azure.net").
var kvBaseUri = 'https://${keyVaultName}${az.environment().suffixes.keyvaultDns}/'

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

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    sqlServerName: sqlServerName
    sqlDbName: sqlDatabaseName
    sqlAdminPassword: sqlAdminPassword
    sqlAdminUser: sqlAdminUser
    location: location
    databaseSku: sqlDatabaseSku
    useFreeLimit: useFreeLimit
    freeLimitExhaustionBehavior: freeLimitExhaustionBehavior
    extraTags: extraTags
  }
}

var sqlConnectionString = 'Server=tcp:${sql.outputs.sqlServerFqdn},1433;Initial Catalog=${sqlDatabaseName};User ID=${sqlAdminUser};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var sqlConnectionStringKvRef = '@Microsoft.KeyVault(SecretUri=${kvBaseUri}secrets/sql-connection-string/)'

module api 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    baseName: baseName
    environment: environment
    location: location
    appServicePlanId: plan.outputs.appServicePlanId
    linuxFxVersion: linuxFxVersion
    alwaysOn: skuName != 'F1'
    appSettings: concat(appSettings, [
      {
        name: 'KeyVault__Uri'
        value: kvBaseUri
      }
    ])
    connectionStrings: concat(connectionStrings, [
      {
        name: 'DefaultConnection'
        type: 'SQLAzure'
        value: sqlConnectionStringKvRef
      }
    ])
    extraTags: extraTags
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    keyVaultName: keyVaultName
    location: location
    enablePurgeProtection: enablePurgeProtection
    secretsUserPrincipalIds: [api.outputs.webAppPrincipalId]
    secrets: {
      'sql-connection-string': sqlConnectionString
    }
  }
}


// ── Variables for Outputs ─────────────────────────────────────────────────────

var sqlConnectionStringSecretObj = filter(keyVault.outputs.secretUris, s => s.name == 'sql-connection-string')

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
#disable-next-line outputs-should-not-contain-secrets
output sqlConnectionStringSecretUri string = length(sqlConnectionStringSecretObj) > 0 ? sqlConnectionStringSecretObj[0].uri : ''
