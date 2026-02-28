@description('Name of the Service Bus namespace.')
param namespaceName string

@description('Azure region.')
param location string = resourceGroup().location

@description('Name of the topic to create.')
param topicName string = 'notifications'

@description('Subscriptions to create on the topic. Each object requires a "name" property.')
param subscriptions array = [
  { name: 'email' }
]

@description('Duplicate detection history window. Requires Standard tier or higher.')
param duplicateDetectionHistoryTimeWindow string = 'PT10M'

@description('Extra tags merged into the resource defaults.')
param extraTags object = {}

var tags = union({ workload: 'eventhub' }, extraTags)

// ── Namespace ─────────────────────────────────────────────────────────────────

resource namespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: 'Standard' // Topics require Standard tier or higher
    tier: 'Standard'
  }
  properties: {}
}

// ── Topic ─────────────────────────────────────────────────────────────────────

resource topic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: namespace
  name: topicName
  properties: {
    // Duplicate detection deduplicates re-sent messages by MessageId within this window.
    // ProcessOutboxFunction sets MessageId = outbox.Id so a crash-and-replay scenario
    // where the same message is republished will be silently dropped by Service Bus.
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: duplicateDetectionHistoryTimeWindow
    defaultMessageTimeToLive: 'P14D' // 14 days max retention
    enableBatchedOperations: true
  }
}

// ── Subscriptions ─────────────────────────────────────────────────────────────

resource topicSubscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = [
  for sub in subscriptions: {
    parent: topic
    name: sub.name
    properties: {
      maxDeliveryCount: 10 // Dead-letter after 10 failed attempts
      deadLetteringOnMessageExpiration: true
      lockDuration: 'PT1M'
      enableBatchedOperations: true
    }
  }
]

// ── Authorization rule (shared key for apps without managed identity support) ─

resource sendListenRule 'Microsoft.ServiceBus/namespaces/AuthorizationRules@2022-10-01-preview' = {
  parent: namespace
  name: 'EventHubApps'
  properties: {
    rights: ['Send', 'Listen']
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output namespaceName string = namespace.name
output namespaceId string = namespace.id
output topicName string = topic.name
#disable-next-line outputs-should-not-contain-secrets
output primaryConnectionString string = sendListenRule.listKeys().primaryConnectionString
