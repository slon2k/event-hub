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

// sqlAdminPassword is a required @secure() param — supply at deploy time:
//   az deployment group create ... --parameters sqlAdminPassword=$env:SQL_ADMIN_PASSWORD
