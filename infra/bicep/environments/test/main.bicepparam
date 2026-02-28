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

// Supply via SQL_ADMIN_PASSWORD env var — never hard-code here.
// CI: set as a GitHub Actions environment secret.
// Local: $env:SQL_ADMIN_PASSWORD = '<password>' before deploying.
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')

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
