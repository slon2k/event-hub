@description('Name of the SQL server.')
param sqlServerName string

@description('Name of the SQL database.')
param sqlDbName string

@secure()
@description('SQL admin password.')
param sqlAdminPassword string

@description('SQL admin username.')
param sqlAdminUser string = 'sqladmin'

@description('Location for SQL resources.')
param location string = resourceGroup().location

@description('Database SKU. Use { name: "Basic", tier: "Basic" } for dev/test, { name: "S0", tier: "Standard" } for prod.')
param databaseSku object = { name: 'Basic', tier: 'Basic' }

@description('Extra tags merged into the defaults.')
param extraTags object = {}

var baseTags = {
  managedBy: 'iac'
}
var finalTags = union(baseTags, extraTags)

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  tags: finalTags
  properties: {
    administratorLogin: sqlAdminUser
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  tags: finalTags
  sku: databaseSku
  properties: {}
}

output sqlServerName string = sqlServer.name
output sqlDbName string = sqlDb.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
