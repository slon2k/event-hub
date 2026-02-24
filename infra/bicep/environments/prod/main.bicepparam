using '../../main.bicep'

param baseName = 'eventhub'
param environment = 'prod'
param skuName = 'B1'
param sqlServerName = 'eventhub-prod-sql'
param sqlDatabaseName = 'eventhub-prod-db'
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'prod'
}
