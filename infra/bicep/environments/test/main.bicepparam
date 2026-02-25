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

// sqlAdminPassword is a required @secure() param — supply at deploy time:
//   az deployment group create ... --parameters sqlAdminPassword=$env:SQL_ADMIN_PASSWORD
