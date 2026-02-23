using '../../main.bicep'

// Development environment parameters
param baseName = 'eventhub'
param environment = 'dev'
param skuName = 'F1'
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'dev'
}
