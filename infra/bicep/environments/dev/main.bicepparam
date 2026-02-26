using '../../main.bicep'

// Development environment parameters
param baseName = 'eventhub'
param environment = 'dev'
param skuName = 'F1'
param sqlServerName = 'eventhub-dev-sql'
param sqlDatabaseName = 'eventhub-dev-db'
param sqlDatabaseSku = { name: 'Basic', tier: 'Basic' } // ignored when useFreeLimit is true
param useFreeLimit = true
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'dev'
}

// Supply via SQL_ADMIN_PASSWORD env var — never hard-code here.
// CI: set as a GitHub Actions environment secret.
// Local: $env:SQL_ADMIN_PASSWORD = '<password>' before deploying.
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
