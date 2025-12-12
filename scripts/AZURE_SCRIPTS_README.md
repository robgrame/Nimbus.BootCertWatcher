# Azure Deployment Scripts - Quick Reference

Complete suite of scripts for deploying SecureBootDashboard to Microsoft Azure.

---

## ?? Scripts Overview

| Script | Purpose | Deployment Type | Duration |
|--------|---------|----------------|----------|
| `Deploy-AzureHybrid.ps1` | Deploy infrastructure only (SQL, Storage, Key Vault) | Hybrid (On-Prem + Azure) | ~10 min |
| `Deploy-AzureFullStack.ps1` | Deploy complete solution to Azure | Full Cloud | ~20 min |
| `Deploy-AzureInfrastructure.bicep` | Infrastructure as Code template | Any | Varies |
| `Configure-AzureMonitoring.ps1` | Setup monitoring and alerts | Post-deployment | ~5 min |

---

## ?? Quick Start Guide

### Scenario 1: Hybrid Deployment (Recommended)

**Use Case**: Keep compute on-premises, use Azure for database and storage.

```powershell
# 1. Login to Azure
az login

# 2. Deploy Azure infrastructure
.\scripts\Deploy-AzureHybrid.ps1 `
    -SubscriptionId "your-subscription-id" `
    -ResourceGroupName "rg-secureboot-prod" `
    -Location "westeurope" `
    -EnableDiagnostics

# 3. Apply database migrations
cd SecureBootDashboard.Api
$sqlConnection = az keyvault secret show `
    --vault-name kv-secureboot-prod-xxx `
    --name SqlConnectionString `
    --query value -o tsv
dotnet ef database update --connection $sqlConnection

# 4. Deploy API/Web to on-premises servers
.\scripts\Deploy-Solution.ps1 -Component API -Environment Production
.\scripts\Deploy-Solution.ps1 -Component Web -Environment Production
```

**Total Time**: ~30 minutes

---

### Scenario 2: Full Azure Deployment

**Use Case**: Cloud-first deployment, fully managed services.

```powershell
# 1. Login to Azure
az login

# 2. Create deployment package
.\scripts\Create-DeploymentPackage.ps1 `
    -Version "1.5.0" `
    -Configuration "Release" `
    -SkipTests

# 3. Deploy to Azure
.\scripts\Deploy-AzureFullStack.ps1 `
    -SubscriptionId "your-subscription-id" `
    -ResourceGroupName "rg-secureboot-prod" `
    -Location "westeurope" `
    -AppServicePlanSku "P1v3" `
    -EnableAutoScale `
    -DeployBinaries `
    -BinariesPath ".\deploy\packages\SecureBootDashboard-Deploy-v1.5.0\binaries"

# 4. Apply database migrations
# (Connection string available in App Service configuration)

# 5. Configure monitoring
.\scripts\Configure-AzureMonitoring.ps1 `
    -SubscriptionId "your-subscription-id" `
    -ResourceGroupName "rg-secureboot-prod" `
    -AlertEmails @("admin@company.com", "ops@company.com") `
    -CreateDashboard
```

**Total Time**: ~40 minutes

---

### Scenario 3: Infrastructure as Code (Bicep)

**Use Case**: Repeatable deployments, multi-environment.

```bash
# 1. Review/edit parameters
code scripts/parameters.prod.json

# 2. Deploy with Bicep
az deployment group create \
  --resource-group rg-secureboot-prod \
  --template-file scripts/Deploy-AzureInfrastructure.bicep \
  --parameters @scripts/parameters.prod.json

# 3. Verify deployment
az deployment group show \
  --resource-group rg-secureboot-prod \
  --name Deploy-AzureInfrastructure \
  --query properties.outputs
```

**Total Time**: ~15 minutes

---

## ?? Detailed Script Documentation

### Deploy-AzureHybrid.ps1

Deploys minimal Azure infrastructure for hybrid deployment.

**Parameters:**

```powershell
-SubscriptionId <string>          # Required: Azure subscription ID
-ResourceGroupName <string>       # Resource group name (default: rg-secureboot-prod)
-Location <string>                # Azure region (default: westeurope)
-EnvironmentName <string>         # Environment tag (default: prod)
-SqlAdminUser <string>            # SQL admin username (default: sqladmin)
-SqlAdminPassword <SecureString>  # SQL admin password (auto-generated if not provided)
-AllowedIpAddresses <string[]>    # IP addresses for SQL firewall
-EnableDiagnostics                # Enable diagnostics and monitoring
-WhatIf                           # Preview changes without creating resources
```

**Resources Created:**
- Azure SQL Server + Database (Standard S2)
- Storage Account (GRS) + Queue
- Key Vault (with connection strings stored)
- Log Analytics Workspace (if -EnableDiagnostics)

**Example:**

```powershell
.\scripts\Deploy-AzureHybrid.ps1 `
    -SubscriptionId "12345678-1234-1234-1234-123456789abc" `
    -ResourceGroupName "rg-secureboot-prod" `
    -Location "westeurope" `
    -AllowedIpAddresses @("203.0.113.50", "203.0.113.51") `
    -EnableDiagnostics
```

**Output:**
```
? SQL Server: sql-secureboot-prod-202501.database.windows.net
? Database: SecureBootDashboard
? Storage Account: stsecurebootprod202501
? Key Vault: kv-secureboot-prod-202501
```

---

### Deploy-AzureFullStack.ps1

Deploys complete solution to Azure App Services.

**Parameters:**

```powershell
-SubscriptionId <string>          # Required: Azure subscription ID
-ResourceGroupName <string>       # Resource group name
-Location <string>                # Azure region
-EnvironmentName <string>         # Environment (dev/staging/prod)
-AppServicePlanSku <string>       # App Service Plan SKU (default: P1v3)
-EnableAutoScale                  # Enable auto-scaling (2-10 instances)
-EnableFrontDoor                  # Deploy Azure Front Door (CDN + WAF)
-DeployBinaries                   # Deploy application binaries
-BinariesPath <string>            # Path to binaries folder
-WhatIf                           # Preview changes
```

**Resources Created:**
- Everything from Hybrid +
- App Service Plan (Linux)
- App Service (API)
- App Service (Web)
- Application Insights
- Managed Identities (for Key Vault and Storage access)
- Auto-scaling rules (if enabled)
- Azure Front Door (if enabled)

**Example:**

```powershell
.\scripts\Deploy-AzureFullStack.ps1 `
    -SubscriptionId "12345678-1234-1234-1234-123456789abc" `
    -ResourceGroupName "rg-secureboot-prod" `
    -AppServicePlanSku "P1v3" `
    -EnableAutoScale `
    -DeployBinaries `
    -BinariesPath ".\deploy\packages\SecureBootDashboard-Deploy-v1.5.0\binaries"
```

**Output:**
```
? API App: https://app-secureboot-api-prod-202501.azurewebsites.net
? Web App: https://app-secureboot-web-prod-202501.azurewebsites.net
? Application Insights: appi-secureboot-prod
? Auto-scaling: Enabled (min: 2, max: 10)
```

---

### Deploy-AzureInfrastructure.bicep

Infrastructure as Code template for repeatable deployments.

**Parameters (parameters.json):**

```json
{
  "environmentName": { "value": "prod" },
  "deploymentScenario": { "value": "hybrid" | "fullstack" },
  "sqlAdminLogin": { "value": "sqladmin" },
  "sqlAdminPassword": { "reference": { "keyVault": {...} } },
  "sqlDatabaseSku": { "value": "S2" },
  "appServicePlanSku": { "value": "P1v3" },
  "enableApplicationInsights": { "value": true },
  "enableAutoScaling": { "value": false },
  "allowedIpAddresses": { "value": ["203.0.113.50"] }
}
```

**Deployment:**

```bash
# Create resource group
az group create --name rg-secureboot-prod --location westeurope

# Deploy (hybrid)
az deployment group create \
  --resource-group rg-secureboot-prod \
  --template-file scripts/Deploy-AzureInfrastructure.bicep \
  --parameters environmentName=prod \
               deploymentScenario=hybrid \
               sqlAdminLogin=sqladmin \
               sqlAdminPassword='YourSecurePassword!123'

# Deploy (fullstack)
az deployment group create \
  --resource-group rg-secureboot-prod \
  --template-file scripts/Deploy-AzureInfrastructure.bicep \
  --parameters @scripts/parameters.prod.json
```

**Outputs:**

```bash
# Get outputs
az deployment group show \
  --resource-group rg-secureboot-prod \
  --name Deploy-AzureInfrastructure \
  --query properties.outputs

# Example output
{
  "apiUrl": "https://app-secureboot-api-prod.azurewebsites.net",
  "sqlServerFqdn": "sql-secureboot-prod.database.windows.net",
  "keyVaultName": "kv-secureboot-prod"
}
```

---

### Configure-AzureMonitoring.ps1

Sets up comprehensive monitoring and alerting.

**Parameters:**

```powershell
-SubscriptionId <string>          # Required: Azure subscription ID
-ResourceGroupName <string>       # Required: Resource group name
-AppInsightsName <string>         # Application Insights name (auto-detected if not provided)
-ApiAppServiceName <string>       # API App Service name (for CPU alerts)
-WebAppServiceName <string>       # Web App Service name (for CPU alerts)
-SqlServerName <string>           # SQL Server name (for database alerts)
-AlertEmails <string[]>           # Email addresses for alert notifications
-CreateDashboard                  # Create Azure Portal dashboard
-WhatIf                           # Preview changes
```

**Alert Rules Created:**
- High API failure rate (>10/min)
- Slow response time (>5s avg)
- High exception rate (>5/min)
- Low availability (<95%)
- High CPU usage (>80%)
- High SQL DTU usage (>80%)
- High SQL storage (>85%)
- SQL deadlocks detected

**Example:**

```powershell
.\scripts\Configure-AzureMonitoring.ps1 `
    -SubscriptionId "12345678-1234-1234-1234-123456789abc" `
    -ResourceGroupName "rg-secureboot-prod" `
    -ApiAppServiceName "app-secureboot-api-prod" `
    -WebAppServiceName "app-secureboot-web-prod" `
    -SqlServerName "sql-secureboot-prod" `
    -AlertEmails @("admin@company.com", "ops@company.com") `
    -CreateDashboard
```

**Output:**
```
? Application Insights: appi-secureboot-prod
? Action Group: SecureBootDashboard-Alerts
? Alert Rules: 8 rules created
? Dashboard: SecureBootDashboard-Monitoring
```

---

## ?? Troubleshooting

### Common Issues

#### Issue: "SQL Server name already exists"

**Solution**: SQL Server names are globally unique. The scripts append a unique suffix.

```powershell
# Force new unique name
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
.\scripts\Deploy-AzureHybrid.ps1 -ResourceGroupName "rg-secureboot-$timestamp"
```

#### Issue: "Insufficient permissions"

**Solution**: Ensure you have required Azure RBAC roles:

```bash
# Check current role
az role assignment list --assignee $(az account show --query user.name -o tsv)

# Required roles:
# - Contributor (resource creation)
# - User Access Administrator (role assignments for Managed Identity)
```

#### Issue: "Bicep deployment fails"

**Solution**: Validate template before deployment:

```bash
# Validate template
az deployment group validate \
  --resource-group rg-secureboot-prod \
  --template-file scripts/Deploy-AzureInfrastructure.bicep \
  --parameters @scripts/parameters.prod.json

# Check for errors in output
```

#### Issue: "App Service deployment timeout"

**Solution**: Increase timeout and check application logs:

```bash
# View deployment logs
az webapp log tail --name app-secureboot-api-prod --resource-group rg-secureboot-prod

# Restart app service
az webapp restart --name app-secureboot-api-prod --resource-group rg-secureboot-prod
```

---

## ?? Cost Estimates

### Hybrid Deployment (Monthly)

| Resource | SKU | Cost (EUR/month) |
|----------|-----|------------------|
| Azure SQL Database | S2 Standard | €120 |
| Storage Account | GRS, 100GB | €5 |
| Key Vault | Standard | €5 |
| **Total** | | **€130** |

### Full Azure Deployment (Monthly)

| Resource | SKU | Cost (EUR/month) |
|----------|-----|------------------|
| Azure SQL Database | S2 Standard | €120 |
| App Service Plan | P1v3 Premium | €150 |
| Storage Account | GRS, 100GB | €5 |
| Key Vault | Standard | €5 |
| Application Insights | 5GB/month | €15 |
| **Total** | | **€295** |

**With Auto-scaling**: €295 + (additional instances × €75) per month

---

## ?? Security Best Practices

### Pre-Deployment

- [ ] Generate strong SQL admin password
- [ ] Obtain SSL certificates from CA
- [ ] Review firewall rules
- [ ] Configure VPN/ExpressRoute for hybrid

### Post-Deployment

- [ ] Rotate SQL admin password
- [ ] Configure Managed Identities
- [ ] Enable Azure Security Center
- [ ] Review NSG rules
- [ ] Setup Azure Backup for SQL Database
- [ ] Enable diagnostic logging for all resources

### Ongoing

- [ ] Monitor security alerts
- [ ] Review access logs monthly
- [ ] Update TLS certificates before expiration
- [ ] Perform security assessments quarterly

---

## ?? Monitoring Best Practices

### Key Metrics to Track

**API Performance**:
- Request rate (requests/sec)
- Response time (p50, p95, p99)
- Failure rate (%)
- Exception count

**Database**:
- DTU usage (%)
- Storage usage (%)
- Connection count
- Query duration

**App Services** (Full Azure):
- CPU usage (%)
- Memory usage (%)
- HTTP queue length
- Instance count

### Kusto Queries

See `Configure-AzureMonitoring.ps1` output for useful queries.

---

## ?? CI/CD Integration

### Azure DevOps Pipeline

```yaml
# azure-pipelines-deploy.yml
trigger:
  branches:
    include:
      - main

variables:
  subscriptionId: '12345678-1234-1234-1234-123456789abc'
  resourceGroup: 'rg-secureboot-prod'

stages:
- stage: DeployInfrastructure
  jobs:
  - job: Bicep
    steps:
    - task: AzureCLI@2
      inputs:
        azureSubscription: 'Azure-Prod'
        scriptType: 'bash'
        scriptLocation: 'inlineScript'
        inlineScript: |
          az deployment group create \
            --resource-group $(resourceGroup) \
            --template-file scripts/Deploy-AzureInfrastructure.bicep \
            --parameters @scripts/parameters.prod.json

- stage: DeployApplication
  dependsOn: DeployInfrastructure
  jobs:
  - job: AppServices
    steps:
    - task: PowerShell@2
      inputs:
        filePath: 'scripts/Deploy-AzureFullStack.ps1'
        arguments: '-SubscriptionId $(subscriptionId) -DeployBinaries -BinariesPath $(Build.ArtifactStagingDirectory)'
```

### GitHub Actions

```yaml
# .github/workflows/azure-deploy.yml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
    
    - name: Deploy Infrastructure
      run: |
        az deployment group create \
          --resource-group rg-secureboot-prod \
          --template-file scripts/Deploy-AzureInfrastructure.bicep \
          --parameters @scripts/parameters.prod.json
```

---

## ?? Related Documentation

- [AZURE_DEPLOYMENT_GUIDE.md](../docs/AZURE_DEPLOYMENT_GUIDE.md) - Complete deployment guide
- [SERVER_INFRASTRUCTURE_DEPLOYMENT.md](../docs/SERVER_INFRASTRUCTURE_DEPLOYMENT.md) - On-premises deployment
- [Create-DeploymentPackage.ps1](./Create-DeploymentPackage.ps1) - Build deployment package

---

## ?? Support

For issues or questions:

1. Check [Troubleshooting](#troubleshooting) section
2. Review Azure deployment logs:
   ```bash
   az deployment group list --resource-group rg-secureboot-prod
   ```
3. Check Application Insights logs (Full Azure only)
4. Review Event Viewer (Hybrid on-premises)

---

**Last Updated**: 2025-01-14
**Version**: 1.0
