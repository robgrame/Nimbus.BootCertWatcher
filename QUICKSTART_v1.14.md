# SecureBootWatcher v1.14 - Unified Deployment Quick Start

## What Changed in v1.14

✨ **Major Update**: Web Dashboard and API are now merged into a **single application** for simplified deployment.

### Before (v1.13 and earlier)
```
Two separate applications:
├── SecureBootDashboard.Web (Port 7001) - UI
└── SecureBootDashboard.Api (Port 5001) - API
```

### After (v1.14)
```
One unified application:
└── SecureBootDashboard.Web (Port 7001) - UI + API
```

## Quick Deploy Guide

### 1. Azure App Service (Recommended)

**Single App Service deployment:**

```bash
# 1. Build and publish
cd SecureBootDashboard.Web
dotnet publish -c Release -o ./publish

# 2. Create zip
cd publish
zip -r ../SecureBootDashboard.Web.zip .

# 3. Deploy to Azure
az webapp deployment source config-zip \
  --resource-group YourResourceGroup \
  --name YourAppName \
  --src ../SecureBootDashboard.Web.zip
```

**Configure App Settings:**
```bash
# Required: SQL Server connection string
az webapp config connection-string set \
  --name YourAppName \
  --resource-group YourResourceGroup \
  --connection-string-type SQLServer \
  --settings SqlServer="Server=yourserver.database.windows.net;..."

# Optional: Application Insights
az webapp config appsettings set \
  --name YourAppName \
  --resource-group YourResourceGroup \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=..."
```

### 2. IIS on Windows Server

```powershell
# 1. Install .NET 10 Hosting Bundle (if not already installed)
# Download from: https://dotnet.microsoft.com/download/dotnet/10.0

# 2. Publish the application
cd SecureBootDashboard.Web
dotnet publish -c Release -o C:\inetpub\wwwroot\SecureBootDashboard

# 3. Create IIS Site
Import-Module WebAdministration
New-WebAppPool -Name "SecureBootDashboard" -Force
Set-ItemProperty IIS:\AppPools\SecureBootDashboard -Name "managedRuntimeVersion" -Value ""
New-WebSite -Name "SecureBootDashboard" `
    -Port 443 `
    -PhysicalPath "C:\inetpub\wwwroot\SecureBootDashboard" `
    -ApplicationPool "SecureBootDashboard"

# 4. Update appsettings.json with your SQL Server connection string
notepad C:\inetpub\wwwroot\SecureBootDashboard\appsettings.json
```

**In appsettings.json, update:**
```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=YOURSERVER;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### 3. Docker

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY publish/ .
EXPOSE 7001
EXPOSE 7000
ENTRYPOINT ["dotnet", "SecureBootDashboard.Web.dll"]
```

```bash
# Build and run
docker build -t secureboot:1.14 .
docker run -d -p 7001:7001 -p 7000:7000 \
  -e ConnectionStrings__SqlServer="Server=host.docker.internal;..." \
  secureboot:1.14
```

## Configuration

### Essential Settings

**appsettings.json (or App Settings in Azure):**

```json
{
  "ConnectionStrings": {
    "SqlServer": "YOUR_SQL_SERVER_CONNECTION_STRING"
  },
  "Authentication": {
    "Provider": "None"  // or "EntraId" or "Windows"
  },
  "QueueProcessor": {
    "Enabled": true,
    "QueueServiceUri": "https://youraccount.queue.core.windows.net",
    "QueueName": "secureboot-reports"
  },
  "ApplicationInsights": {
    "ConnectionString": "YOUR_APPINSIGHTS_CONNECTION_STRING"  // Optional
  }
}
```

### Optional: Azure Queue Authentication

If using Azure Queue for report ingestion:

```json
{
  "QueueProcessor": {
    "AuthenticationMethod": "Certificate",  // or "ConnectionString"
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "CertificateThumbprint": "YOUR_CERT_THUMBPRINT"
  }
}
```

## Verification

### 1. Check Application Health

```bash
curl https://yourdomain.com/health
```

Expected response: `{"status":"Healthy"}`

### 2. Access Web Dashboard

Navigate to: `https://yourdomain.com/`

You should see the dashboard homepage.

### 3. Check API Endpoints

Navigate to: `https://yourdomain.com/swagger` (Development only)

Or test directly:
```bash
curl https://yourdomain.com/api/Devices
```

### 4. Verify SignalR

SignalR hub is available at: `https://yourdomain.com/dashboardHub`

Real-time updates should appear on dashboard when devices report.

## Migrating from v1.13 (Separate Deployments)

If you have existing separate Web and API deployments:

### Step 1: Backup
```sql
-- Backup your database
BACKUP DATABASE SecureBootDashboard TO DISK = 'C:\Backups\SecureBootDashboard.bak'
```

### Step 2: Deploy Unified App
Follow deployment guide above to deploy the unified SecureBootDashboard.Web application.

### Step 3: Update Client Configuration

**PowerShell clients need to update API URL** (if API URL was different):

In `appsettings.powershell-client.json`:
```json
{
  "SecureBootWatcher": {
    "ApiUrl": "https://your-unified-app.azurewebsites.net/api/SecureBootReports"
  }
}
```

### Step 4: Test
- Verify dashboard loads
- Submit test report from PowerShell client
- Check report appears in dashboard

### Step 5: Decommission Old API
Once verified, you can stop and delete the old separate API application.

## Troubleshooting

### Application Won't Start

**Check logs:**
- Local: `logs/unified-YYYYMMDD.log`
- Azure: Azure Portal → App Service → Log Stream

**Common issues:**
1. **SQL Server connection**: Verify connection string
2. **Port in use**: Ensure port 7001 is available
3. **Missing database**: Run migrations if needed

### Cannot Access API Endpoints

**Check configuration:**
```bash
# Verify API endpoints are registered
curl https://yourdomain.com/api/Devices
```

**If 404:**
- Check `Program.cs` has `app.MapControllers()`
- Verify controllers are in `Controllers/` folder
- Check application logs for startup errors

### Web Pages Show "API Unavailable"

**Check ApiSettings in appsettings.json:**
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7001"  // Should point to same app
  }
}
```

In production, update to your actual URL or leave as localhost.

### Background Services Not Running

**Check configuration:**
```json
{
  "QueueProcessor": {
    "Enabled": true  // Must be true
  }
}
```

**Check logs for:**
- Queue processor startup messages
- Azure Queue connection errors
- Certificate authentication errors

## Performance Recommendations

### Small Environment (1-100 devices)
- **Azure**: B1 Basic (1 vCPU, 1.75 GB RAM)
- **VM**: 2 vCPU, 4 GB RAM
- **Cost**: ~$15-30/month (Azure)

### Medium Environment (100-500 devices)
- **Azure**: S1 Standard (1 vCPU, 1.75 GB RAM) or P1V2 (1 vCPU, 3.5 GB RAM)
- **VM**: 4 vCPU, 8 GB RAM
- **Cost**: ~$70-140/month (Azure)

### Large Environment (500-1000 devices)
- **Azure**: P2V2 (2 vCPU, 7 GB RAM) or P3V2 (4 vCPU, 14 GB RAM)
- **VM**: 8 vCPU, 16 GB RAM
- **Cost**: ~$150-300/month (Azure)

### >1000 Devices
Consider separate Web and API deployments for independent scaling.

## Support & Documentation

- **Comprehensive Guide**: `docs/UNIFIED_DEPLOYMENT.md`
- **General Documentation**: `README.md`
- **Troubleshooting**: `docs/TROUBLESHOOTING_PORTS.md`
- **API Documentation**: Browse to `/swagger` (Development mode)

## Version Information

- **Version**: 1.14
- **Release Date**: January 2026
- **Breaking Changes**: None (backward compatible)
- **.NET Version**: .NET 10
- **Deployment**: Unified (Web + API in one application)

## Quick Links

- **Dashboard**: `https://yourdomain.com/`
- **API Root**: `https://yourdomain.com/api/`
- **Swagger UI**: `https://yourdomain.com/swagger` (Dev only)
- **Health Check**: `https://yourdomain.com/health`
- **SignalR Hub**: `https://yourdomain.com/dashboardHub`

---

🎉 **Your unified SecureBootWatcher is now ready to use!**

For detailed information, see `docs/UNIFIED_DEPLOYMENT.md`
