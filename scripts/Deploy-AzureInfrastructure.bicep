// ===============================================================================
// Deploy-AzureInfrastructure.bicep
//
// Azure Infrastructure as Code template for SecureBootDashboard
// Supports multiple deployment scenarios:
// - Hybrid: SQL + Storage + Key Vault only
// - FullStack: All Azure services including App Services
//
// Usage:
//   az deployment group create \
//     --resource-group rg-secureboot-prod \
//     --template-file Deploy-AzureInfrastructure.bicep \
//     --parameters @parameters.json
//
// ===============================================================================

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string = 'prod'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Deployment scenario')
@allowed([
  'hybrid'
  'fullstack'
])
param deploymentScenario string = 'hybrid'

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('SQL Database service objective')
@allowed([
  'Basic'
  'S0'
  'S1'
  'S2'
  'S3'
  'P1'
  'P2'
])
param sqlDatabaseSku string = 'S2'

@description('App Service Plan SKU (fullstack only)')
@allowed([
  'B1'
  'B2'
  'B3'
  'S1'
  'S2'
  'S3'
  'P1v2'
  'P2v2'
  'P3v2'
  'P1v3'
  'P2v3'
  'P3v3'
])
param appServicePlanSku string = 'P1v3'

@description('Enable Application Insights (fullstack only)')
param enableApplicationInsights bool = true

@description('Enable auto-scaling (fullstack only)')
param enableAutoScaling bool = false

@description('Client IP addresses allowed to access SQL Server')
param allowedIpAddresses array = []

@description('Tags to apply to all resources')
param tags object = {
  Environment: environmentName
  Application: 'SecureBootDashboard'
  ManagedBy: 'Bicep'
}

// ===============================================================================
// Variables
// ===============================================================================

var uniqueSuffix = substring(uniqueString(resourceGroup().id), 0, 6)
var resourcePrefix = 'secureboot-${environmentName}'

// Resource names
var sqlServerName = 'sql-${resourcePrefix}-${uniqueSuffix}'
var sqlDatabaseName = 'SecureBootDashboard'
var storageAccountName = 'st${replace(resourcePrefix, '-', '')}${uniqueSuffix}'
var keyVaultName = 'kv-${resourcePrefix}-${uniqueSuffix}'
var queueName = 'secureboot-reports'

// FullStack resources
var logAnalyticsWorkspaceName = 'log-${resourcePrefix}'
var appInsightsName = 'appi-${resourcePrefix}'
var appServicePlanName = 'plan-${resourcePrefix}'
var apiAppServiceName = 'app-${resourcePrefix}-api-${uniqueSuffix}'
var webAppServiceName = 'app-${resourcePrefix}-web-${uniqueSuffix}'

// ===============================================================================
// SQL Server and Database
// ===============================================================================

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: sqlDatabaseSku
    tier: startsWith(sqlDatabaseSku, 'P') ? 'Premium' : 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 268435456000 // 250 GB
    zoneRedundant: false
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Geo'
  }
}

// Allow Azure services
resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Allow specific IP addresses
resource sqlFirewallRules 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = [for (ip, index) in allowedIpAddresses: {
  parent: sqlServer
  name: 'Allow-IP-${index}'
  properties: {
    startIpAddress: ip
    endIpAddress: ip
  }
}]

// ===============================================================================
// Storage Account and Queue
// ===============================================================================

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_GRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    encryption: {
      services: {
        blob: {
          enabled: true
        }
        file: {
          enabled: true
        }
        queue: {
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource queue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: queueService
  name: queueName
}

// ===============================================================================
// Key Vault
// ===============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enableRbacAuthorization: false
    accessPolicies: []
  }
}

// Store SQL connection string in Key Vault
resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  properties: {
    value: 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${sqlDatabaseName};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False'
  }
}

// Store storage connection string in Key Vault
resource storageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'StorageConnectionString'
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
  }
}

// ===============================================================================
// Application Insights (FullStack only)
// ===============================================================================

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = if (deploymentScenario == 'fullstack' && enableApplicationInsights) {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = if (deploymentScenario == 'fullstack' && enableApplicationInsights) {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: deploymentScenario == 'fullstack' && enableApplicationInsights ? logAnalyticsWorkspace.id : null
  }
}

// ===============================================================================
// App Service Plan (FullStack only)
// ===============================================================================

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = if (deploymentScenario == 'fullstack') {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: appServicePlanSku
    tier: startsWith(appServicePlanSku, 'P') ? 'Premium' : (startsWith(appServicePlanSku, 'S') ? 'Standard' : 'Basic')
  }
  properties: {
    reserved: true // Linux
  }
  kind: 'linux'
}

// ===============================================================================
// API App Service (FullStack only)
// ===============================================================================

resource apiAppService 'Microsoft.Web/sites@2023-01-01' = if (deploymentScenario == 'fullstack') {
  name: apiAppServiceName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: deploymentScenario == 'fullstack' ? appServicePlan.id : ''
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      http20Enabled: true
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: deploymentScenario == 'fullstack' && enableApplicationInsights ? appInsights.properties.ConnectionString : ''
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'ConnectionStrings__SqlServer'
          value: '@Microsoft.KeyVault(SecretUri=${sqlConnectionStringSecret.properties.secretUri})'
        }
        {
          name: 'QueueProcessor__QueueServiceUri'
          value: storageAccount.properties.primaryEndpoints.queue
        }
      ]
    }
  }
}

// Grant API access to Key Vault
resource apiKeyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = if (deploymentScenario == 'fullstack') {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: deploymentScenario == 'fullstack' ? apiAppService.identity.principalId : ''
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}

// Grant API access to Storage Queue
var storageQueueDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')

resource apiStorageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deploymentScenario == 'fullstack') {
  scope: storageAccount
  name: guid(storageAccount.id, apiAppService.id, storageQueueDataContributorRole)
  properties: {
    roleDefinitionId: storageQueueDataContributorRole
    principalId: deploymentScenario == 'fullstack' ? apiAppService.identity.principalId : ''
    principalType: 'ServicePrincipal'
  }
}

// ===============================================================================
// Web App Service (FullStack only)
// ===============================================================================

resource webAppService 'Microsoft.Web/sites@2023-01-01' = if (deploymentScenario == 'fullstack') {
  name: webAppServiceName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: deploymentScenario == 'fullstack' ? appServicePlan.id : ''
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      http20Enabled: true
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: deploymentScenario == 'fullstack' && enableApplicationInsights ? appInsights.properties.ConnectionString : ''
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'ApiSettings__BaseUrl'
          value: deploymentScenario == 'fullstack' ? 'https://${apiAppService.properties.defaultHostName}' : ''
        }
        {
          name: 'ApiSettings__UseCertificateAuth'
          value: 'false'
        }
      ]
    }
  }
}

// Grant Web access to Key Vault (if needed)
resource webKeyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = if (deploymentScenario == 'fullstack') {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: deploymentScenario == 'fullstack' ? webAppService.identity.principalId : ''
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}

// ===============================================================================
// Auto-scaling (FullStack only)
// ===============================================================================

resource autoScaleSettings 'Microsoft.Insights/autoscalesettings@2022-10-01' = if (deploymentScenario == 'fullstack' && enableAutoScaling) {
  name: '${appServicePlanName}-autoscale'
  location: location
  tags: tags
  properties: {
    enabled: true
    targetResourceUri: deploymentScenario == 'fullstack' ? appServicePlan.id : ''
    profiles: [
      {
        name: 'Default'
        capacity: {
          minimum: '2'
          maximum: '10'
          default: '2'
        }
        rules: [
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: deploymentScenario == 'fullstack' ? appServicePlan.id : ''
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'GreaterThan'
              threshold: 70
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT5M'
            }
          }
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: deploymentScenario == 'fullstack' ? appServicePlan.id : ''
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 30
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT5M'
            }
          }
        ]
      }
    ]
  }
}

// ===============================================================================
// Outputs
// ===============================================================================

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name

output storageAccountName string = storageAccount.name
output storageQueueEndpoint string = storageAccount.properties.primaryEndpoints.queue
output queueName string = queue.name

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri

output appInsightsName string = deploymentScenario == 'fullstack' && enableApplicationInsights ? appInsights.name : ''
output appInsightsConnectionString string = deploymentScenario == 'fullstack' && enableApplicationInsights ? appInsights.properties.ConnectionString : ''

output appServicePlanName string = deploymentScenario == 'fullstack' ? appServicePlan.name : ''
output apiAppServiceName string = deploymentScenario == 'fullstack' ? apiAppService.name : ''
output apiUrl string = deploymentScenario == 'fullstack' ? 'https://${apiAppService.properties.defaultHostName}' : ''
output webAppServiceName string = deploymentScenario == 'fullstack' ? webAppService.name : ''
output webUrl string = deploymentScenario == 'fullstack' ? 'https://${webAppService.properties.defaultHostName}' : ''

output deploymentScenario string = deploymentScenario
output environmentName string = environmentName
