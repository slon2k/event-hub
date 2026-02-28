using '../../main.bicep'

param baseName = 'eventhub'
param environment = 'prod'
param skuName = 'B1'
param sqlServerName = 'eventhub-prod-sql'
param sqlDatabaseName = 'eventhub-prod-db'
param serviceBusNamespaceName = 'eventhub-prod-sb'
param sqlDatabaseSku = { name: 'S0', tier: 'Standard' }
param enablePurgeProtection = true
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'prod'
}

// Supply via SQL_ADMIN_PASSWORD env var — never hard-code here.
// CI: set as a GitHub Actions environment secret.
// Local: $env:SQL_ADMIN_PASSWORD = '<password>' before deploying.
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')

param appSettings = [
  {
    name: 'Authentication__Mode'
    value: 'AzureAd'
  }
  {
    name: 'AzureAd__Authority'
    value: 'https://login.microsoftonline.com/<YOUR_TENANT_ID>/v2.0'
  }
  {
    name: 'AzureAd__Audience'
    value: 'api://<DEV_CLIENT_ID>'
  }
]
