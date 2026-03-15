@description('Base workload name, e.g. "eventhub".')
@maxLength(40)
param baseName string

@description('Environment moniker, e.g. "dev", "test", "prod".')
param environment string

@description('Azure region.')
param location string = resourceGroup().location

@description('Retention period in days for Log Analytics. Default 30, max 730.')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

@description('Extra tags merged into the defaults.')
param extraTags object = {}

var normalizedEnvironment = toLower(environment)
var workspaceName = '${baseName}-${normalizedEnvironment}-logs'
var appInsightsName = '${baseName}-${normalizedEnvironment}-ai'

var baseTags = {
  environment: normalizedEnvironment
  workload: 'observability'
  managedBy: 'iac'
}
var finalTags = union(baseTags, extraTags)

// ── Log Analytics Workspace ───────────────────────────────────────────────────

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: finalTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ── Application Insights ──────────────────────────────────────────────────────

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: finalTags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    RetentionInDays: retentionInDays
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output appInsightsName string = appInsights.name
output appInsightsId string = appInsights.id
#disable-next-line outputs-should-not-contain-secrets
output connectionString string = appInsights.properties.ConnectionString
output instrumentationKey string = appInsights.properties.InstrumentationKey
output workspaceName string = logAnalyticsWorkspace.name
output workspaceId string = logAnalyticsWorkspace.id
