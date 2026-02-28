using '../../main.bicep'

param baseName = 'eventhub'
param environment = 'prod'
param skuName = 'B1'
param sqlServerName = 'eventhub-prod-sql'
param sqlDatabaseName = 'eventhub-prod-db'
param sqlDatabaseSku = { name: 'S0', tier: 'Standard' }
param enablePurgeProtection = true
param skuCapacity = 1
param linuxFxVersion = 'DOTNETCORE|10.0'
param extraTags = {
  environment: 'prod'
}

// Supply via SQL_ADMIN_PASSWORD env var — never hard-code here.
// CI: set as a GitHub Actions environment secret.
// Local: $env:SQL_ADMIN_PASSWORD = '<password>' before deploying.
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')

param appSettings = [
  {
    name: 'Authentication__Mode'
    value: 'AzureAd'
  }
  {
    name: 'AzureAd__Authority'
    value: 'https://login.microsoftonline.com/<YOUR_TENANT_ID>/v2.0'
  }
  {
    name: 'AzureAd__Audience'
    value: 'api://<DEV_CLIENT_ID>'
  }
]

// Function app settings — set UseStub=false and provide real ACS config for prod.
// AcsEmail__ConnectionString should be stored in Key Vault; add it as a secret and
// replace the value below with a @Microsoft.KeyVault(...) reference.
param functionAppSettings = [
  {
    name: 'AcsEmail__UseStub'
    value: 'false'
  }
  {
    name: 'AcsEmail__ConnectionString'
    value: '<YOUR_ACS_CONNECTION_STRING>'
  }
  {
    name: 'AcsEmail__SenderAddress'
    value: 'noreply@<YOUR_ACS_VERIFIED_DOMAIN>'
  }
  {
    name: 'App__BaseUrl'
    value: 'https://<YOUR_PROD_URL>'
  }
]
