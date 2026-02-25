using '../../main.bicep'

// Development environment parameters
param baseName = 'eventhub'
param environment = 'dev'
param skuName = 'F1'
param sqlServerName = 'eventhub-dev-sql'
param sqlDatabaseName = 'eventhub-dev-db'
param sqlDatabaseSku = { name: 'Basic', tier: 'Basic' }
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'dev'
}

// sqlAdminPassword is a required @secure() param — supply at deploy time:
//   az deployment group create ... --parameters sqlAdminPassword=$env:SQL_ADMIN_PASSWORD
