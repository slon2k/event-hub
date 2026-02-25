using '../../main.bicep'

param baseName = 'eventhub'
param environment = 'test'
param skuName = 'F1'
param sqlServerName = 'eventhub-test-sql'
param sqlDatabaseName = 'eventhub-test-db'
param sqlDatabaseSku = { name: 'Basic', tier: 'Basic' }
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'test'
}

// Supply via SQL_ADMIN_PASSWORD env var — never hard-code here.
// CI: set as a GitHub Actions environment secret.
// Local: $env:SQL_ADMIN_PASSWORD = '<password>' before deploying.
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
