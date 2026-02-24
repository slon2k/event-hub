@description('Name of the SQL server')
param sqlServerName string
@description('Name of the SQL database')
param sqlDbName string
@secure()
@description('SQL admin password')
param sqlAdminPassword string
@description('SQL admin username')
param sqlAdminUser string = 'sqladmin'
@description('Location for SQL resources')
param location string = resourceGroup().location

resource sqlServer 'Microsoft.Sql/servers@2022-02-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminUser
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2022-02-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {}
}

output sqlServerName string = sqlServer.name
output sqlDbName string = sqlDb.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
