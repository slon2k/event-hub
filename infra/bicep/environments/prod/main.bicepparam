using '../../main.bicep'

param baseName = 'eventhub'
param environment = 'prod'
param skuName = 'B1'
param sqlServerName = 'eventhub-prod-sql'
param sqlDatabaseName = 'eventhub-prod-db'
param sqlDatabaseSku = { name: 'S0', tier: 'Standard' }
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'prod'
}

// Supply via SQL_ADMIN_PASSWORD env var — never hard-code here.
// CI: set as a GitHub Actions environment secret.
// Local: $env:SQL_ADMIN_PASSWORD = '<password>' before deploying.
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
