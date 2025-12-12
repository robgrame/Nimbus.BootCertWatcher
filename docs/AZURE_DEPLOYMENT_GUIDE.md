# Azure Deployment Guide - SecureBootDashboard

## Guida Completa al Deployment su Azure

Questa guida descrive le opzioni e le procedure per deployare SecureBootDashboard su Microsoft Azure, sia completamente che parzialmente.

---

## ?? Indice

1. [Architetture di Deployment](#architetture-di-deployment)
2. [Opzione 1: Hybrid (Consigliata)](#opzione-1-hybrid-consigliata)
3. [Opzione 2: Full Azure](#opzione-2-full-azure)
4. [Opzione 3: Database-Only Azure](#opzione-3-database-only-azure)
5. [Deployment con Azure CLI](#deployment-con-azure-cli)
6. [Deployment con Bicep/ARM](#deployment-con-biceparm)
7. [CI/CD con Azure DevOps](#cicd-con-azure-devops)
8. [Monitoring e Diagnostics](#monitoring-e-diagnostics)
9. [Cost Optimization](#cost-optimization)
10. [Security Best Practices](#security-best-practices)

---

## Architetture di Deployment

### Confronto Opzioni

| Componente | Hybrid | Full Azure | DB-Only Azure |
|------------|--------|------------|---------------|
| **Database** | Azure SQL | Azure SQL | Azure SQL |
| **API Server** | On-Premises IIS | App Service | On-Premises IIS |
| **Web Dashboard** | On-Premises IIS | App Service | On-Premises IIS |
| **Storage Queue** | Azure Queue | Azure Queue | Azure Queue |
| **Client Agent** | On-Premises | On-Premises | On-Premises |
| **Key Vault** | Optional | Azure Key Vault | Optional |
| **Monitoring** | Optional | App Insights | Optional |

### Decision Matrix

| Scenario | Recommended Architecture | Rationale |
|----------|-------------------------|-----------|
| **Enterprise con DC on-prem** | Hybrid | Mantiene controllo su compute, usa Azure per storage/database |
| **Cloud-first organization** | Full Azure | Sfrutta completamente servizi Azure gestiti |
| **Budget limitato** | DB-Only Azure | Minimizza costi, migra solo database |
| **Compliance requirements** | Hybrid | Dati sensibili on-prem, storage in Azure |
| **Global deployment** | Full Azure | Latenza ottimizzata con Azure regions |

---

## Opzione 1: Hybrid (Consigliata)

### Architettura

```
???????????????????????????????????????????????????????????????????????
?                        On-Premises Network                           ?
?  ????????????????     ????????????????     ???????????????????    ?
?  ?   Clients    ???????  API Server  ???????  Web Dashboard  ?    ?
?  ? (Workstations)?     ?   (IIS/VM)   ?     ?    (IIS/VM)     ?    ?
?  ????????????????     ????????????????     ???????????????????    ?
?                               ?                                      ?
????????????????????????????????????????????????????????????????????????
                                ? HTTPS (VPN/ExpressRoute)
                                ?
                    ????????????????????????????
                    ?      Azure Cloud         ?
                    ?  ??????????????????????  ?
                    ?  ?  Azure SQL Database?  ?
                    ?  ?  (PaaS)            ?  ?
                    ?  ??????????????????????  ?
                    ?  ??????????????????????  ?
                    ?  ?  Storage Account   ?  ?
                    ?  ?  (Queue)           ?  ?
                    ?  ??????????????????????  ?
                    ?  ??????????????????????  ?
                    ?  ?  Key Vault         ?  ?
                    ?  ?  (Secrets)         ?  ?
                    ?  ??????????????????????  ?
                    ????????????????????????????
```

### Vantaggi

? **Controllo**: Server on-premises sotto controllo IT  
? **Performance**: Bassa latenza per utenti interni  
? **Compliance**: Compute layer on-prem per compliance  
? **Scalabilità**: Database scalabile su Azure SQL  
? **Backup**: Azure backup automatico  
? **Costi**: Costi compute ottimizzati (usa VM esistenti)

### Componenti Azure

1. **Azure SQL Database**
   - Tier: Standard S2/S3 o General Purpose
   - Backup automatico (7-35 giorni)
   - Geo-replication opzionale

2. **Azure Storage Account**
   - Queue per buffering report
   - Blob per file storage opzionale
   - Geo-redundant storage (GRS)

3. **Azure Key Vault**
   - Certificati per mutual TLS
   - Connection strings
   - Secret per App Registration

4. **Azure Application Insights** (opzionale)
   - Telemetria applicazione
   - Performance monitoring
   - Alerting

### Setup Steps

#### 1. Provisioning Azure Resources

```bash
# Set variables
RESOURCE_GROUP="rg-secureboot-prod"
LOCATION="westeurope"
SQL_SERVER="sql-secureboot-prod"
SQL_DB="SecureBootDashboard"
STORAGE_ACCOUNT="stsecurebootprod"
KEY_VAULT="kv-secureboot-prod"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create SQL Server
az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user sqladmin \
  --admin-password 'YourSecurePassword!123'

# Create SQL Database
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name $SQL_DB \
  --service-objective S2 \
  --backup-storage-redundancy Geo

# Configure firewall (allow on-premises IP range)
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowOnPremises \
  --start-ip-address 203.0.113.0 \
  --end-ip-address 203.0.113.255

# Create Storage Account
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_GRS \
  --kind StorageV2

# Create Queue
az storage queue create \
  --name secureboot-reports \
  --account-name $STORAGE_ACCOUNT

# Create Key Vault
az keyvault create \
  --name $KEY_VAULT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enabled-for-deployment true \
  --enabled-for-template-deployment true
```

#### 2. Configure Connectivity

**Option A: Site-to-Site VPN**

```bash
# Create Virtual Network
az network vnet create \
  --resource-group $RESOURCE_GROUP \
  --name vnet-secureboot \
  --address-prefix 10.1.0.0/16 \
  --subnet-name snet-gateway \
  --subnet-prefix 10.1.255.0/27

# Create VPN Gateway
az network vnet-gateway create \
  --resource-group $RESOURCE_GROUP \
  --name vpn-gateway-secureboot \
  --vnet vnet-secureboot \
  --gateway-type Vpn \
  --vpn-type RouteBased \
  --sku VpnGw1 \
  --public-ip-address pip-vpn-gateway

# Create Local Network Gateway (on-premises)
az network local-gateway create \
  --resource-group $RESOURCE_GROUP \
  --name lng-onpremises \
  --gateway-ip-address <YOUR_ONPREM_PUBLIC_IP> \
  --local-address-prefixes 192.168.0.0/16
```

**Option B: Azure ExpressRoute** (per enterprise)

Contatta il tuo provider di connettività per setup ExpressRoute.

**Option C: Public Endpoints + Private Link**

```bash
# Enable Private Link for SQL
az sql server vnet-rule create \
  --server $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --name allow-onprem \
  --vnet-name vnet-secureboot \
  --subnet snet-data
```

#### 3. Database Migration

```powershell
# Export schema from on-premises (if migrating existing)
SqlPackage.exe /Action:Export \
  /SourceServerName:ONPREM_SQL \
  /SourceDatabaseName:SecureBootDashboard \
  /TargetFile:SecureBootDashboard.bacpac

# Import to Azure SQL
SqlPackage.exe /Action:Import \
  /SourceFile:SecureBootDashboard.bacpac \
  /TargetServerName:sql-secureboot-prod.database.windows.net \
  /TargetDatabaseName:SecureBootDashboard \
  /TargetUser:sqladmin \
  /TargetPassword:YourSecurePassword!123

# Or use EF Core migrations (for new deployment)
cd SecureBootDashboard.Api
dotnet ef database update \
  --connection "Server=sql-secureboot-prod.database.windows.net;Database=SecureBootDashboard;User Id=sqladmin;Password=YourSecurePassword!123;Encrypt=True;"
```

#### 4. Configure On-Premises Servers

**API Server - appsettings.Production.json**

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=sql-secureboot-prod.database.windows.net;Database=SecureBootDashboard;User Id=sqladmin;Password=YourSecurePassword!123;Encrypt=True;TrustServerCertificate=False"
  },
  "QueueProcessor": {
    "Enabled": true,
    "QueueServiceUri": "https://stsecurebootprod.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "Certificate",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "CertificateThumbprint": "YOUR_CERT_THUMBPRINT",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
  }
}
```

#### 5. Store Secrets in Key Vault

```bash
# Store SQL connection string
az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name SqlConnectionString \
  --value "Server=sql-secureboot-prod.database.windows.net;Database=SecureBootDashboard;User Id=sqladmin;Password=YourSecurePassword!123;Encrypt=True"

# Store Storage Account connection string
STORAGE_CONNECTION=$(az storage account show-connection-string \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name StorageConnectionString \
  --value "$STORAGE_CONNECTION"

# Grant API server access to Key Vault
# (if using Managed Identity or App Registration)
az keyvault set-policy \
  --name $KEY_VAULT \
  --object-id <API_SERVER_IDENTITY> \
  --secret-permissions get list
```

---

## Opzione 2: Full Azure

### Architettura

```
                    ????????????????????????????????
                    ?      Azure Cloud             ?
                    ?                              ?
????????????????    ?  ?????????????????????????? ?
?   Clients    ??????  ?   App Service (API)    ? ?
?(Workstations)?    ?  ?   (Linux Container)    ? ?
????????????????    ?  ?????????????????????????? ?
                    ?             ?                ?
                    ?  ?????????????????????????? ?
                    ?  ?   App Service (Web)    ? ?
                    ?  ?   (ASP.NET Core)       ? ?
                    ?  ?????????????????????????? ?
                    ?             ?                ?
                    ?  ?????????????????????????? ?
                    ?  ?   Azure SQL Database   ? ?
                    ?  ?????????????????????????? ?
                    ?  ?????????????????????????? ?
                    ?  ?   Storage Account      ? ?
                    ?  ?   (Queue + Blob)       ? ?
                    ?  ?????????????????????????? ?
                    ?  ?????????????????????????? ?
                    ?  ?   Key Vault            ? ?
                    ?  ?????????????????????????? ?
                    ?  ?????????????????????????? ?
                    ?  ?   Application Insights ? ?
                    ?  ?????????????????????????? ?
                    ?  ?????????????????????????? ?
                    ?  ?   Front Door / CDN     ? ?
                    ?  ?????????????????????????? ?
                    ????????????????????????????????
```

### Vantaggi

? **Managed Services**: Nessuna gestione server  
? **Auto-scaling**: Scale automatico su carico  
? **High Availability**: SLA 99.95%  
? **Global Reach**: Deploy multi-region  
? **Built-in Security**: Azure Security Center, DDoS protection  
? **DevOps Ready**: CI/CD integrato

### Azure Resources

1. **App Service Plan**
   - Tier: P1v3 o superiore (per production)
   - Always On enabled
   - Auto-scaling configurato

2. **App Service (API)**
   - .NET 10 runtime
   - Linux container support
   - Managed Identity enabled

3. **App Service (Web)**
   - .NET 10 runtime
   - Razor Pages support
   - Custom domain + SSL

4. **Azure SQL Database**
   - General Purpose o Business Critical
   - Active Geo-Replication
   - Auto-tuning enabled

5. **Application Insights**
   - Telemetria end-to-end
   - Live metrics
   - Smart detection

6. **Azure Front Door** (opzionale)
   - WAF enabled
   - Global load balancing
   - SSL offloading

### Deployment Script

Vedi: `scripts/Deploy-AzureFullStack.ps1` (creato nel prossimo file)

---

## Opzione 3: Database-Only Azure

### Architettura

```
???????????????????????????????????????????????????????????????????????
?                        On-Premises Network                           ?
?  ????????????????     ????????????????     ???????????????????    ?
?  ?   Clients    ???????  API Server  ???????  Web Dashboard  ?    ?
?  ? (Workstations)?     ?   (IIS/VM)   ?     ?    (IIS/VM)     ?    ?
?  ????????????????     ????????????????     ???????????????????    ?
?                               ?                                      ?
????????????????????????????????????????????????????????????????????????
                                ? SQL over TLS
                                ?
                    ????????????????????????????
                    ?      Azure Cloud         ?
                    ?  ??????????????????????  ?
                    ?  ?  Azure SQL Database?  ?
                    ?  ?  (PaaS)            ?  ?
                    ?  ??????????????????????  ?
                    ????????????????????????????
```

### Quando Usarlo

- Budget limitato (minimizza costi Azure)
- Compliance richiede compute on-premises
- Infrastruttura server già disponibile
- Vuoi solo backup/HA database

### Setup Minimo

```bash
# Create only SQL Database
az sql server create --name sql-secureboot-prod ...
az sql db create --name SecureBootDashboard ...

# Configure firewall for on-prem IP
az sql server firewall-rule create ...

# Update connection string on API server
# No other Azure services needed
```

---

## Deployment con Azure CLI

### Script Completo - Hybrid Deployment

```bash
#!/bin/bash
# deploy-hybrid-azure.sh

set -e

# Configuration
RESOURCE_GROUP="rg-secureboot-prod"
LOCATION="westeurope"
SQL_SERVER="sql-secureboot-prod-$(date +%s)"
SQL_DB="SecureBootDashboard"
STORAGE_ACCOUNT="stsecureboot$(date +%s | cut -c6-)"
QUEUE_NAME="secureboot-reports"
KEY_VAULT="kv-secureboot-prod-$(date +%s | cut -c6-)"
ADMIN_USER="sqladmin"
ADMIN_PASSWORD="$(openssl rand -base64 32 | tr -d '/+=' | cut -c1-20)!Aa1"

echo "========================================"
echo "SecureBootDashboard - Azure Hybrid Deployment"
echo "========================================"
echo ""
echo "Configuration:"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location: $LOCATION"
echo "  SQL Server: $SQL_SERVER"
echo "  Storage Account: $STORAGE_ACCOUNT"
echo "  Key Vault: $KEY_VAULT"
echo ""

# Create Resource Group
echo "Creating resource group..."
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create SQL Server
echo "Creating SQL Server..."
az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $ADMIN_USER \
  --admin-password "$ADMIN_PASSWORD"

# Create SQL Database
echo "Creating SQL Database..."
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name $SQL_DB \
  --service-objective S2 \
  --backup-storage-redundancy Geo \
  --zone-redundant false

# Configure SQL firewall (allow Azure services)
echo "Configuring SQL firewall..."
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Get on-premises public IP (for firewall rule)
MY_IP=$(curl -s https://ifconfig.me)
echo "Detected public IP: $MY_IP"

az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowMyIP \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP

# Create Storage Account
echo "Creating Storage Account..."
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_GRS \
  --kind StorageV2 \
  --min-tls-version TLS1_2

# Get Storage connection string
STORAGE_CONNECTION=$(az storage account show-connection-string \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

# Create Queue
echo "Creating Storage Queue..."
az storage queue create \
  --name $QUEUE_NAME \
  --account-name $STORAGE_ACCOUNT

# Create Key Vault
echo "Creating Key Vault..."
az keyvault create \
  --name $KEY_VAULT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enabled-for-deployment true \
  --enabled-for-template-deployment true \
  --sku standard

# Store secrets in Key Vault
echo "Storing secrets in Key Vault..."
SQL_CONNECTION="Server=$SQL_SERVER.database.windows.net;Database=$SQL_DB;User Id=$ADMIN_USER;Password=$ADMIN_PASSWORD;Encrypt=True;TrustServerCertificate=False"

az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name SqlConnectionString \
  --value "$SQL_CONNECTION"

az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name StorageConnectionString \
  --value "$STORAGE_CONNECTION"

az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name SqlAdminPassword \
  --value "$ADMIN_PASSWORD"

# Enable diagnostics (optional)
echo "Enabling diagnostics..."
# Create Log Analytics workspace
WORKSPACE_NAME="log-secureboot-prod"
az monitor log-analytics workspace create \
  --resource-group $RESOURCE_GROUP \
  --workspace-name $WORKSPACE_NAME \
  --location $LOCATION

WORKSPACE_ID=$(az monitor log-analytics workspace show \
  --resource-group $RESOURCE_GROUP \
  --workspace-name $WORKSPACE_NAME \
  --query id -o tsv)

# Enable SQL diagnostics
az monitor diagnostic-settings create \
  --name sql-diagnostics \
  --resource $(az sql db show --name $SQL_DB --server $SQL_SERVER --resource-group $RESOURCE_GROUP --query id -o tsv) \
  --workspace $WORKSPACE_ID \
  --logs '[{"category": "SQLInsights", "enabled": true}, {"category": "QueryStoreRuntimeStatistics", "enabled": true}]' \
  --metrics '[{"category": "Basic", "enabled": true}]'

# Summary
echo ""
echo "========================================"
echo "Deployment Complete!"
echo "========================================"
echo ""
echo "SQL Server: $SQL_SERVER.database.windows.net"
echo "Database: $SQL_DB"
echo "Admin User: $ADMIN_USER"
echo "Admin Password: *** (stored in Key Vault: $KEY_VAULT)"
echo ""
echo "Storage Account: $STORAGE_ACCOUNT"
echo "Queue Name: $QUEUE_NAME"
echo ""
echo "Key Vault: $KEY_VAULT"
echo ""
echo "Connection Strings (retrieve from Key Vault):"
echo "  az keyvault secret show --vault-name $KEY_VAULT --name SqlConnectionString --query value -o tsv"
echo "  az keyvault secret show --vault-name $KEY_VAULT --name StorageConnectionString --query value -o tsv"
echo ""
echo "Next Steps:"
echo "1. Apply EF Core migrations to database"
echo "2. Update API appsettings.Production.json with connection strings"
echo "3. Create App Registration for Queue access"
echo "4. Deploy API and Web to on-premises servers"
echo ""
```

### PowerShell Version

Vedi: `scripts/Deploy-AzureHybrid.ps1` (prossimo file)

---

## Deployment con Bicep/ARM

### Bicep Template

```bicep
// main.bicep
@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
param environmentName string = 'prod'

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

// Variables
var resourcePrefix = 'secureboot-${environmentName}'
var sqlServerName = 'sql-${resourcePrefix}-${uniqueString(resourceGroup().id)}'
var sqlDatabaseName = 'SecureBootDashboard'
var storageAccountName = 'st${replace(resourcePrefix, '-', '')}${uniqueString(resourceGroup().id)}'
var keyVaultName = 'kv-${resourcePrefix}-${uniqueString(resourceGroup().id)}'
var queueName = 'secureboot-reports'

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'S2'
    tier: 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 268435456000 // 250 GB
    zoneRedundant: false
    readScale: 'Disabled'
  }
}

// Firewall rule - Allow Azure services
resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_GRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

// Queue Service
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2022-09-01' = {
  parent: storageAccount
  name: 'default'
}

// Queue
resource queue 'Microsoft.Storage/storageAccounts/queueServices/queues@2022-09-01' = {
  parent: queueService
  name: queueName
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: keyVaultName
  location: location
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
    accessPolicies: []
  }
}

// Outputs
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storageAccount.name
output storageQueueEndpoint string = storageAccount.properties.primaryEndpoints.queue
output keyVaultName string = keyVault.name
```

### Deploy Bicep Template

```bash
# Create resource group
az group create --name rg-secureboot-prod --location westeurope

# Deploy Bicep template
az deployment group create \
  --resource-group rg-secureboot-prod \
  --template-file main.bicep \
  --parameters environmentName=prod \
               sqlAdminLogin=sqladmin \
               sqlAdminPassword='YourSecurePassword!123'
```

---

## CI/CD con Azure DevOps

### Azure Pipelines YAML

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
      - main
      - release/*

variables:
  buildConfiguration: 'Release'
  azureSubscription: 'SecureBootDashboard-Prod'
  resourceGroup: 'rg-secureboot-prod'
  webAppNameApi: 'app-secureboot-api-prod'
  webAppNameWeb: 'app-secureboot-web-prod'

stages:
- stage: Build
  displayName: 'Build Solution'
  jobs:
  - job: BuildJob
    displayName: 'Build Job'
    pool:
      vmImage: 'ubuntu-latest'
    
    steps:
    - task: UseDotNet@2
      displayName: 'Install .NET 10 SDK'
      inputs:
        version: '10.0.x'
    
    - task: DotNetCoreCLI@2
      displayName: 'Restore NuGet packages'
      inputs:
        command: 'restore'
        projects: '**/*.csproj'
    
    - task: DotNetCoreCLI@2
      displayName: 'Build solution'
      inputs:
        command: 'build'
        projects: '**/*.csproj'
        arguments: '--configuration $(buildConfiguration) --no-restore'
    
    - task: DotNetCoreCLI@2
      displayName: 'Run tests'
      inputs:
        command: 'test'
        projects: '**/*Tests.csproj'
        arguments: '--configuration $(buildConfiguration) --no-build'
    
    - task: DotNetCoreCLI@2
      displayName: 'Publish API'
      inputs:
        command: 'publish'
        publishWebProjects: false
        projects: 'SecureBootDashboard.Api/SecureBootDashboard.Api.csproj'
        arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)/api'
    
    - task: DotNetCoreCLI@2
      displayName: 'Publish Web'
      inputs:
        command: 'publish'
        publishWebProjects: false
        projects: 'SecureBootDashboard.Web/SecureBootDashboard.Web.csproj'
        arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)/web'
    
    - task: PublishBuildArtifacts@1
      displayName: 'Publish artifacts'
      inputs:
        PathtoPublish: '$(Build.ArtifactStagingDirectory)'
        ArtifactName: 'drop'

- stage: DeployDev
  displayName: 'Deploy to Development'
  dependsOn: Build
  condition: succeeded()
  jobs:
  - deployment: DeployJobDev
    displayName: 'Deploy to Dev'
    environment: 'development'
    pool:
      vmImage: 'ubuntu-latest'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: AzureWebApp@1
            displayName: 'Deploy API to Dev'
            inputs:
              azureSubscription: '$(azureSubscription)'
              appType: 'webApp'
              appName: '$(webAppNameApi)-dev'
              package: '$(Pipeline.Workspace)/drop/api'
          
          - task: AzureWebApp@1
            displayName: 'Deploy Web to Dev'
            inputs:
              azureSubscription: '$(azureSubscription)'
              appType: 'webApp'
              appName: '$(webAppNameWeb)-dev'
              package: '$(Pipeline.Workspace)/drop/web'

- stage: DeployProd
  displayName: 'Deploy to Production'
  dependsOn: DeployDev
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
  jobs:
  - deployment: DeployJobProd
    displayName: 'Deploy to Production'
    environment: 'production'
    pool:
      vmImage: 'ubuntu-latest'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: AzureWebApp@1
            displayName: 'Deploy API to Prod'
            inputs:
              azureSubscription: '$(azureSubscription)'
              appType: 'webApp'
              appName: '$(webAppNameApi)'
              package: '$(Pipeline.Workspace)/drop/api'
              deploymentMethod: 'zipDeploy'
          
          - task: AzureWebApp@1
            displayName: 'Deploy Web to Prod'
            inputs:
              azureSubscription: '$(azureSubscription)'
              appType: 'webApp'
              appName: '$(webAppNameWeb)'
              package: '$(Pipeline.Workspace)/drop/web'
              deploymentMethod: 'zipDeploy'
```

---

## Monitoring e Diagnostics

### Application Insights Integration

**appsettings.json (API & Web)**

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=xxx;IngestionEndpoint=https://xxx.applicationinsights.azure.com/",
    "EnableAdaptiveSampling": true,
    "EnablePerformanceCounterCollectionModule": true
  }
}
```

### Queries Utili (Log Analytics)

```kusto
// API Request durations
requests
| where cloud_RoleName == "SecureBootDashboard.Api"
| summarize avg(duration), percentiles(duration, 50, 95, 99) by bin(timestamp, 5m)
| render timechart

// Failed requests
requests
| where cloud_RoleName == "SecureBootDashboard.Api"
| where success == false
| summarize count() by resultCode, bin(timestamp, 1h)
| render barchart

// Database query performance
dependencies
| where type == "SQL"
| summarize avg(duration), count() by name
| order by avg_duration desc
```

---

## Cost Optimization

### Stima Costi Mensili

#### Hybrid Architecture

| Resource | Tier | Est. Cost (EUR/month) |
|----------|------|----------------------|
| Azure SQL Database (S2) | Standard | €120 |
| Storage Account (GRS) | 100 GB | €5 |
| Key Vault | Standard | €5 |
| VPN Gateway (optional) | VpnGw1 | €120 |
| **Total** | | **€250 - €370** |

#### Full Azure Architecture

| Resource | Tier | Est. Cost (EUR/month) |
|----------|------|----------------------|
| App Service Plan (P1v3) | Premium | €150 |
| App Service (API) | - | Included |
| App Service (Web) | - | Included |
| Azure SQL Database (S3) | Standard | €240 |
| Storage Account (GRS) | 100 GB | €5 |
| Key Vault | Standard | €5 |
| Application Insights | 5 GB/month | €15 |
| Front Door (optional) | Standard | €50 |
| **Total** | | **€415 - €465** |

### Cost Saving Tips

1. **Use Reserved Capacity** (SQL Database)
   - Save 20-40% with 1-year or 3-year commitment

2. **Right-size Resources**
   - Start with lower tiers, scale up se necessario
   - Use auto-scaling per App Services

3. **Optimize Storage**
   - Use lifecycle policies per blob storage
   - Archive old data to Cool/Archive tier

4. **Monitor and Alert**
   - Set budget alerts in Azure Cost Management
   - Review Cost Analysis dashboard monthly

---

## Security Best Practices

### Azure Security Checklist

- [ ] Enable Azure Security Center Standard tier
- [ ] Configure Private Endpoints for SQL/Storage
- [ ] Use Managed Identities (no passwords in config)
- [ ] Enable Advanced Threat Protection (SQL)
- [ ] Configure NSG rules for VNet
- [ ] Enable Azure DDoS Protection
- [ ] Use Azure Front Door with WAF
- [ ] Implement Azure Policy for governance
- [ ] Enable diagnostic logging for all resources
- [ ] Configure alerts for security events
- [ ] Regular security reviews con Secure Score

### Managed Identity Setup

```bash
# Enable Managed Identity for App Service
az webapp identity assign \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod

# Get identity object ID
IDENTITY_ID=$(az webapp identity show \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --query principalId -o tsv)

# Grant Key Vault access
az keyvault set-policy \
  --name kv-secureboot-prod \
  --object-id $IDENTITY_ID \
  --secret-permissions get list

# Grant SQL Database access
az sql server ad-admin create \
  --resource-group rg-secureboot-prod \
  --server-name sql-secureboot-prod \
  --display-name app-secureboot-api \
  --object-id $IDENTITY_ID
```

---

## Related Scripts

Vedi anche:
- `scripts/Deploy-AzureHybrid.ps1` - PowerShell hybrid deployment
- `scripts/Deploy-AzureFullStack.ps1` - PowerShell full Azure deployment
- `scripts/Deploy-AzureInfrastructure.bicep` - Bicep IaC template

---

**Document Version**: 1.0  
**Last Updated**: 2025-01-14  
**Classification**: Technical - Azure Deployment
