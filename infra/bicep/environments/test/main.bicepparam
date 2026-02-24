using '../../main.bicep'

param baseName = 'eventhub'
param environment = 'test'
param skuName = 'F1'
param sqlServerName = 'eventhub-test-sql'
param sqlDatabaseName = 'eventhub-test-db'
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'test'
}
