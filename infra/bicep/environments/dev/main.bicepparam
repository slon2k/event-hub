using '../../main.bicep'

// Development environment parameters
param baseName = 'eventhub'
param environment = 'dev'
param skuName = 'F1'
param sqlServerName = 'eventhub-dev-sql'
param sqlDatabaseName = 'eventhub-dev-db'
param sqlDatabaseSku = { name: 'Basic', tier: 'Basic' }
param useFreeLimit = false
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'dev'
}

// Supply via env vars — never hard-code here.
// CI: set as GitHub Actions environment secrets.
// Local: $env:VAR = '<value>' before deploying.
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
param graphTenantId = readEnvironmentVariable('GRAPH_TENANT_ID', '')
param graphClientId = readEnvironmentVariable('GRAPH_CLIENT_ID', '')
param graphClientSecret = readEnvironmentVariable('GRAPH_CLIENT_SECRET', '')

// App settings
param appSettings = [
  {
    name: 'Authentication__Mode'
    value: 'AzureAd'
  }
  {
    name: 'AzureAd__Authority'
    value: 'https://login.microsoftonline.com/8dd52aee-fd49-4e5c-ace3-0a0e907b0529/v2.0'
  }
  {
    name: 'AzureAd__Audience'
    value: 'api://09af58ae-9706-469f-8dfe-c913428505fd'
  }
  {
    // API app registration client ID — used by EntraIdentityAdminService to
    // resolve the API service principal and look up app role IDs in Graph
    name: 'Graph__ApiAppClientId'
    value: '09af58ae-9706-469f-8dfe-c913428505fd'
  }
]

// Function app settings — ACS stub active; emails are written to the EmailOutbox table in Azure
// Table Storage instead of being sent via ACS. Inspect rows in Storage Explorer or the Portal.
param functionAppSettings = [
  {
    name: 'AcsEmail__UseStub'
    value: 'true'
  }
  {
    name: 'AcsEmail__SenderAddress'
    value: 'noreply@eventhub.example.com'
  }
  {
    name: 'App__BaseUrl'
    value: 'https://eventhub-dev-api.azurewebsites.net'
  }
  {
    name: 'EmailOutboxTableName'
    value: 'EmailOutbox'
  }
]
