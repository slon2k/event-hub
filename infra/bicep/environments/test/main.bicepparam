using '../../main.bicep'

param baseName = 'eventhub'
param environment = 'test'
param skuName = 'F1'
param sqlServerName = 'eventhub-test-sql'
param sqlDatabaseName = 'eventhub-test-db'
param sqlDatabaseSku = { name: 'Basic', tier: 'Basic' }  // ignored when useFreeLimit = true
param useFreeLimit = true
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'test'
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
    value: 'api://c97af009-1376-4bf4-a79f-083198225966'
  }
  {
    // API app registration client ID — used by EntraIdentityAdminService to
    // resolve the API service principal and look up app role IDs in Graph
    name: 'Graph__ApiAppClientId'
    value: 'c97af009-1376-4bf4-a79f-083198225966'
  }
]

// Function app settings — ACS stub active; emails are logged to console, not sent.
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
    value: 'https://eventhub-test-api.azurewebsites.net'
  }
]
