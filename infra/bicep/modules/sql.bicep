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

@description('Database SKU. Use { name: "Basic", tier: "Basic" } for dev/test, { name: "S0", tier: "Standard" } for prod. Ignored when useFreeLimit is true.')
param databaseSku object = { name: 'Basic', tier: 'Basic' }

@description('Use the Azure SQL free offer (100,000 vCore-seconds/month, up to 10 free databases per subscription). When true, the database is provisioned as General Purpose Serverless Gen5 2 vCores.')
param useFreeLimit bool = false

@description('Behaviour when the free limit is exhausted. AutoPause stops the database; BillOverUsage continues at standard rates.')
@allowed(['AutoPause', 'BillOverUsage'])
param freeLimitExhaustionBehavior string = 'AutoPause'

@description('Allow Azure services and resources to access this server (the 0.0.0.0–0.0.0.0 special rule). Enable for App Service. Disable when using VNet integration.')
param allowAzureServicesAccess bool = true

@description('Extra tags merged into the defaults.')
param extraTags object = {}

var baseTags = {
  managedBy: 'iac'
}
var finalTags = union(baseTags, extraTags)

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
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

// Free offer requires the GP Serverless Gen5 2vCore SKU regardless of the databaseSku param.
var effectiveSku = useFreeLimit
  ? { name: 'GP_S_Gen5_2', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  : databaseSku

resource sqlDb 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  tags: finalTags
  sku: effectiveSku
  properties: {
    useFreeLimit: useFreeLimit
    freeLimitExhaustionBehavior: useFreeLimit ? freeLimitExhaustionBehavior : null
  }
}

// The 0.0.0.0–0.0.0.0 range is the Azure-reserved special value that maps to
// "Allow Azure services and resources to access this server" in the portal.
// It does NOT open access to the public internet.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = if (allowAzureServicesAccess) {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerName string = sqlServer.name
output sqlDbName string = sqlDb.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
