# Deployment Guide – Secure Boot Certificate Watcher

This guide provides step-by-step instructions for deploying the **Secure Boot Certificate Watcher** solution to Azure, including manual resource provisioning, configuration, and client rollout.

---

## Table of Contents
1. [Prerequisites](#prerequisites)
2. [Azure Resources Overview](#azure-resources-overview)
3. [Step 1: Provision Azure Resources](#step-1-provision-azure-resources)
4. [Step 2: Configure & Deploy API](#step-2-configure--deploy-api)
5. [Step 3: Configure & Deploy Web Dashboard](#step-3-configure--deploy-web-dashboard)
6. [Step 4: Deploy Client to Windows Devices](#step-4-deploy-client-to-windows-devices)
7. [Step 5: Validate End-to-End Flow](#step-5-validate-end-to-end-flow)
8. [Optional: Enable Azure Queue Sink](#optional-enable-azure-queue-sink)
9. [Troubleshooting](#troubleshooting)

---

## Prerequisites

- **Azure Subscription** with Contributor or Owner role
- **Azure CLI** (`az`) installed and authenticated: `az login`
- **.NET 8 SDK** installed locally
- **.NET Framework 4.8 Developer Pack** (for client builds)
- **SQL Server Management Studio** or **Azure Data Studio** (optional, for database inspection)
- **Git** for cloning the repository

---

## Azure Resources Overview

| Resource | Purpose | SKU/Tier |
|----------|---------|----------|
| **Resource Group** | Logical container for all resources | N/A |
| **Azure SQL Server** | Hosts the dashboard database | Basic/Standard |
| **Azure SQL Database** | Stores device reports, events, and metadata | S0 or Serverless |
| **App Service Plan** | Hosts API and Web apps | B1 or higher |
| **App Service (API)** | REST API for report ingestion and queries | .NET 8 |
| **App Service (Web)** | Razor Pages dashboard UI | .NET 8 |
| **Storage Account** *(optional)* | Azure Queue for message buffering | Standard LRS |

---

## Step 1: Provision Azure Resources

### 1.1 Create Resource Group
```bash
az group create \
  --name rg-secureboot-prod \
  --location eastus
```

### 1.2 Create Azure SQL Server
```bash
az sql server create \
  --name sql-secureboot-prod \
  --resource-group rg-secureboot-prod \
  --location eastus \
  --admin-user sqladmin \
  --admin-password "YourSecureP@ssw0rd!"
```

**Configure Firewall Rules**
```bash
# Allow Azure services
az sql server firewall-rule create \
  --resource-group rg-secureboot-prod \
  --server sql-secureboot-prod \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Allow your IP for migrations (replace with your public IP)
az sql server firewall-rule create \
  --resource-group rg-secureboot-prod \
  --server sql-secureboot-prod \
  --name AllowMyIP \
  --start-ip-address YOUR_PUBLIC_IP \
  --end-ip-address YOUR_PUBLIC_IP
```

### 1.3 Create Azure SQL Database
```bash
az sql db create \
  --resource-group rg-secureboot-prod \
  --server sql-secureboot-prod \
  --name SecureBootDashboard \
  --service-objective S0 \
  --backup-storage-redundancy Local
```

### 1.4 Create App Service Plan
```bash
az appservice plan create \
  --name plan-secureboot-prod \
  --resource-group rg-secureboot-prod \
  --location eastus \
  --sku B1 \
  --is-linux
```
> **Note**: Use `--is-linux` for Linux hosting; omit for Windows hosting.

### 1.5 Create API App Service
```bash
az webapp create \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --plan plan-secureboot-prod \
  --runtime "DOTNETCORE:8.0"
```

### 1.6 Create Web App Service
```bash
az webapp create \
  --name app-secureboot-web-prod \
  --resource-group rg-secureboot-prod \
  --plan plan-secureboot-prod \
  --runtime "DOTNETCORE:8.0"
```

### 1.7 (Optional) Create Storage Account for Azure Queue
```bash
az storage account create \
  --name stsecurebootprod \
  --resource-group rg-secureboot-prod \
  --location eastus \
  --sku Standard_LRS

# Create queue
az storage queue create \
  --name secureboot-reports \
  --account-name stsecurebootprod
```

---

## Step 2: Configure & Deploy API

### 2.1 Update Local Configuration
Edit `SecureBootDashboard.Api/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=tcp:sql-secureboot-prod.database.windows.net,1433;Database=SecureBootDashboard;User ID=sqladmin;Password=YourSecureP@ssw0rd!;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Storage": {
    "Provider": "EfCore"
  }
}
```

### 2.2 Apply EF Core Migrations
From repository root:
```powershell
dotnet ef database update --project SecureBootDashboard.Api --startup-project SecureBootDashboard.Api
```
> This creates the `Devices`, `SecureBootReports`, and `SecureBootEvents` tables in Azure SQL Database.

### 2.3 Publish API
```powershell
cd SecureBootDashboard.Api
dotnet publish -c Release -o ./publish
```

### 2.4 Package for Deployment
```powershell
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force
```

### 2.5 Deploy to Azure App Service
```bash
az webapp deploy \
  --resource-group rg-secureboot-prod \
  --name app-secureboot-api-prod \
  --src-path ./publish.zip \
  --type zip
```

### 2.6 Configure App Service Settings
```bash
# Set connection string via Azure portal or CLI
az webapp config connection-string set \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --settings SqlServer="Server=tcp:sql-secureboot-prod.database.windows.net,1433;Database=SecureBootDashboard;User ID=sqladmin;Password=YourSecureP@ssw0rd!;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" \
  --connection-string-type SQLAzure

# Set storage provider
az webapp config appsettings set \
  --name app-secureboot-api-prod \
  --resource-group rg-secureboot-prod \
  --settings Storage__Provider=EfCore
```

### 2.7 Verify API Deployment
Navigate to:
```
https://app-secureboot-api-prod.azurewebsites.net/swagger
```
Test the `/api/SecureBootReports` endpoint and `/health` endpoint.

---

## Step 3: Configure & Deploy Web Dashboard

### 3.1 Update Local Configuration
Edit `SecureBootDashboard.Web/appsettings.json`:
```json
{
  "ApiBaseUrl": "https://app-secureboot-api-prod.azurewebsites.net"
}
```
> Add this setting to configure API base URL for the web app.

### 3.2 Publish Web
```powershell
cd SecureBootDashboard.Web
dotnet publish -c Release -o ./publish
```

### 3.3 Package for Deployment
```powershell
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force
```

### 3.4 Deploy to Azure App Service
```bash
az webapp deploy \
  --resource-group rg-secureboot-prod \
  --name app-secureboot-web-prod \
  --src-path ./publish.zip \
  --type zip
```

### 3.5 Configure App Service Settings
```bash
az webapp config appsettings set \
  --name app-secureboot-web-prod \
  --resource-group rg-secureboot-prod \
  --settings ApiBaseUrl=https://app-secureboot-api-prod.azurewebsites.net
```

### 3.6 Verify Web Dashboard
Navigate to:
```
https://app-secureboot-web-prod.azurewebsites.net
```
Dashboard should load (though no data yet until clients report).

---

## Step 4: Deploy Client to Windows Devices

### 4.1 Build Client
From repository root:
```powershell
dotnet publish SecureBootWatcher.Client -c Release -r win-x86 --self-contained false -o ./client-publish
```

### 4.2 Configure Client
Edit `client-publish/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "SecureBootWatcher": {
    "FleetId": "fleet-prod",
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    "EventLookbackPeriod": "1.00:00:00",
    "EventChannels": [
      "Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin",
      "Microsoft-Windows-CodeIntegrity/Operational"
    ],
    "Sinks": {
      "EnableFileShare": false,
      "EnableAzureQueue": false,
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://app-secureboot-api-prod.azurewebsites.net",
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:00:30"
      }
    }
  }
}
```

### 4.3 Package Client
```powershell
Compress-Archive -Path ./client-publish/* -DestinationPath SecureBootWatcher-Client.zip
```

### 4.4 Distribute Client
Deploy via:
- **Group Policy**: Copy to NETLOGON share, deploy via startup script
- **Microsoft Endpoint Manager (Intune)**: Create Win32 app package
- **SCCM/ConfigMgr**: Create application deployment
- **Manual**: Copy to `C:\Program Files\SecureBootWatcher\` on target devices

### 4.5 Install and Schedule Client
On each target device:
```powershell
# Extract to install directory
Expand-Archive -Path SecureBootWatcher-Client.zip -DestinationPath "C:\Program Files\SecureBootWatcher"

# Create scheduled task (run daily)
$action = New-ScheduledTaskAction -Execute "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"
$trigger = New-ScheduledTaskTrigger -Daily -At "09:00AM"
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName "SecureBootWatcher" -Action $action -Trigger $trigger -Principal $principal -Description "Monitors Secure Boot certificate status"
```

### 4.6 Test Client Manually
```powershell
cd "C:\Program Files\SecureBootWatcher"
.\SecureBootWatcher.Client.exe
```
Review console output for successful registry capture and API POST.

---

## Step 5: Validate End-to-End Flow

### 5.1 Verify Client Execution
On a test device:
1. Run client manually or wait for scheduled task
2. Check console logs for:
   ```
   [Information] Registry snapshot captured
   [Information] Sending report to Web API sink...
   [Information] Successfully sent report to https://app-secureboot-api-prod.azurewebsites.net/api/SecureBootReports
   ```

### 5.2 Confirm API Ingestion
Navigate to:
```
https://app-secureboot-api-prod.azurewebsites.net/api/SecureBootReports/recent?limit=10
```
Should return JSON array with recent reports.

### 5.3 View in Dashboard
Open:
```
https://app-secureboot-web-prod.azurewebsites.net
```
Navigate to Reports or Devices page; confirm data appears.

### 5.4 Query SQL Database (Optional)
Connect to Azure SQL Database via SSMS/Azure Data Studio:
```sql
SELECT TOP 10 * FROM SecureBootReports ORDER BY CreatedAtUtc DESC;
SELECT TOP 10 * FROM Devices;
SELECT TOP 10 * FROM SecureBootEvents;
```

---

## Optional: Enable Azure Queue Sink

If you prefer message-based ingestion (e.g., for high-volume fleets or background processing):

### 1. Create Storage Account & Queue
(Already done in Step 1.7 if provisioned)

### 2. Configure Client for Queue Sink
Edit `client-publish/appsettings.json`:
```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableAzureQueue": true,
      "EnableWebApi": false,
      "AzureQueue": {
        "QueueEndpoint": "https://stsecurebootprod.queue.core.windows.net/secureboot-reports",
        "QueueName": "secureboot-reports",
        "MaxSendRetryCount": 5
      }
    }
  }
}
```

### 3. Grant Client Access
**Option A: Managed Identity** (for Azure VMs)
```bash
# Assign identity to VM
az vm identity assign --resource-group rg-vms --name vm-client-01

# Grant Storage Queue Data Contributor role
VM_IDENTITY=$(az vm show --resource-group rg-vms --name vm-client-01 --query identity.principalId -o tsv)
az role assignment create \
  --assignee $VM_IDENTITY \
  --role "Storage Queue Data Contributor" \
  --scope /subscriptions/{subscription-id}/resourceGroups/rg-secureboot-prod/providers/Microsoft.Storage/storageAccounts/stsecurebootprod
```

**Option B: Connection String** (for on-prem/domain-joined devices)
```json
{
  "AzureQueue": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=stsecurebootprod;AccountKey=YOUR_STORAGE_KEY;EndpointSuffix=core.windows.net"
  }
}
```

### 4. Add Queue Processing to API
Implement a background service in `SecureBootDashboard.Api` to dequeue messages and persist via `IReportStore`.

---

## Troubleshooting

### Issue: Client fails to send reports
**Symptoms**: Console shows HTTP errors or timeouts

**Resolution**:
1. Verify API URL in `appsettings.json`
2. Check network connectivity: `Test-NetConnection app-secureboot-api-prod.azurewebsites.net -Port 443`
3. Ensure firewall/proxy allows outbound HTTPS
4. Review client logs for detailed error messages

### Issue: API returns 500 Internal Server Error
**Symptoms**: Swagger or client POST fails with 500

**Resolution**:
1. Check API logs in Azure Portal → App Service → Log stream
2. Verify SQL connection string and firewall rules
3. Confirm EF migrations applied: `dotnet ef migrations list --project SecureBootDashboard.Api`
4. Test SQL connectivity from local machine

### Issue: Dashboard shows no data
**Symptoms**: Web app loads but reports/devices empty

**Resolution**:
1. Confirm API is reachable: `Invoke-RestMethod -Uri https://app-secureboot-api-prod.azurewebsites.net/health`
2. Check `ApiBaseUrl` setting in web app configuration
3. Query SQL directly to verify data exists
4. Review web app logs for HTTP errors

### Issue: EF migrations fail
**Symptoms**: `dotnet ef database update` errors

**Resolution**:
1. Ensure your IP is whitelisted in SQL Server firewall
2. Verify connection string syntax (correct server name, credentials, database)
3. Test connection: `sqlcmd -S sql-secureboot-prod.database.windows.net -U sqladmin -P YourSecureP@ssw0rd! -d SecureBootDashboard`
4. Check EF Tools version: `dotnet tool update --global dotnet-ef`

---

## Security Best Practices

1. **Use Managed Identity**: Enable managed identity for API and grant SQL DB access via Azure AD authentication
2. **Rotate Secrets**: Store connection strings in Azure Key Vault and reference via app settings
3. **Restrict Firewall**: Limit SQL Server firewall to App Service outbound IPs only
4. **Enable HTTPS Only**: Enforce HTTPS-only traffic for all app services
5. **Monitor Access**: Enable diagnostic logs for SQL Database and Application Insights for app services

---

## Next Steps

- **Set up Application Insights**: Enable telemetry for API and Web apps
- **Configure Alerts**: Create Azure Monitor alerts for API errors, SQL DTU, and queue depth
- **Automate Deployments**: Use Azure DevOps or GitHub Actions for CI/CD pipelines
- **Scale Client Rollout**: Deploy to production fleets via Intune or SCCM
- **Add Authentication**: Implement Azure AD authentication for dashboard access

---

## Support

For issues or questions, open a GitHub issue or contact the repository maintainer.
