# Deploy-ApiServer.ps1 - Quick Start Guide

## Prerequisites

### Required Software
- **Windows Server** with IIS installed
- **PowerShell 5.0** or later (run as Administrator)
- **.NET 10 Hosting Bundle** ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Valid SSL/TLS Certificate** (installed in Cert:\LocalMachine\My store)
- **Published API binaries** (from `dotnet publish SecureBootDashboard.Api --configuration Release`)

### Required IIS Management Tools
One of the following must be installed:
- `Web-Scripting-Tools` (recommended) - Installs WebAdministration module
- `Web-Mgmt-Tools` - Alternative IIS management tools
- IISAdministration module (automatic fallback if WebAdministration not available)

**Install Management Tools**:
```powershell
# Recommended (WebAdministration module)
Install-WindowsFeature -Name Web-Scripting-Tools -IncludeManagementTools

# Alternative
Install-WindowsFeature -Name Web-Mgmt-Tools
```

---

## SSL Certificate Setup

### Find Your Certificate Thumbprint
```powershell
# List all certificates in Personal store
Get-ChildItem -Path "Cert:\LocalMachine\My" | Format-Table Subject, Thumbprint

# Or search for a specific certificate
Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object { $_.Subject -like "*yourdomain.com*" } | Select-Object Subject, Thumbprint
```

---

## Basic Deployment

### Step 1: Test without making changes (WhatIf mode)
```powershell
# Run from the repository root directory
cd C:\Users\<user>\source\repos\robgrame\Nimbus.BootCertWatcher

.\scripts\Deploy-ApiServer.ps1 `
    -SiteName "SecureBootDashboard.Api" `
    -AppPoolName "SecureBootDashboard.Api" `
    -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "ABC123DEF456789..." `
    -WhatIf
```

### Step 2: Execute actual deployment
```powershell
.\scripts\Deploy-ApiServer.ps1 `
    -SiteName "SecureBootDashboard.Api" `
    -AppPoolName "SecureBootDashboard.Api" `
    -PhysicalPath "C:\inetpub\SecureBootDashboard.Api" `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "ABC123DEF456789..."
```

---

## Advanced Options

### Enable HTTP Binding (with redirect to HTTPS)
```powershell
.\scripts\Deploy-ApiServer.ps1 `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "ABC123DEF456789..." `
    -CreateHttpBinding `
    -EnableHttpRedirect
```

### Custom Ports (for testing/development)
```powershell
.\scripts\Deploy-ApiServer.ps1 `
    -HostHeader "api.yourdomain.com" `
    -HttpsPort 8443 `
    -HttpPort 8080 `
    -SslCertificateThumbprint "ABC123DEF456789..."
```

### Custom Source and Destination Paths
```powershell
.\scripts\Deploy-ApiServer.ps1 `
    -SourcePath "\\share\api-binaries\net10.0\publish" `
    -PhysicalPath "D:\CustomPath\SecureBootDashboard.Api" `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "ABC123DEF456789..."
```

---

## Parameter Reference

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-SiteName` | "SecureBootDashboard.Api" | IIS website name |
| `-AppPoolName` | "SecureBootDashboard.Api" | IIS application pool name |
| `-PhysicalPath` | "C:\inetpub\SecureBootDashboard.Api" | Physical directory for site files |
| `-HostHeader` | "api.yourdomain.com" | Host header (domain name) |
| `-HttpsPort` | 443 | HTTPS port number |
| `-HttpPort` | 80 | HTTP port number |
| `-SslCertificateThumbprint` | (none) | SSL certificate thumbprint from cert store |
| `-SourcePath` | ".\SecureBootDashboard.Api\bin\Release\net10.0\publish" | Path to published binaries |
| `-EnableHttpRedirect` | (flag) | Redirect HTTP to HTTPS |
| `-CreateHttpBinding` | (flag) | Create HTTP binding (without redirect) |
| `-WhatIf` | (flag) | Show what would be done without making changes |

---

## What the Script Does

The deployment script performs these steps automatically:

1. ? **Check Prerequisites**
   - Verifies IIS is installed
   - Checks for .NET 10 Hosting Bundle
   - Validates SSL certificate exists
   - Confirms published binaries are available

2. ? **Create Application Pool**
   - Creates or reuses existing app pool
   - Configures .NET Core runtime (no managed code)
   - Sets identity to ApplicationPoolIdentity
   - Disables periodic restarts and idle timeouts
   - Enables AlwaysRunning start mode

3. ? **Copy Files**
   - Stops running application pool (if needed)
   - Creates backup of existing deployment
   - Copies new binaries to physical path
   - Creates logs directory

4. ? **Create IIS Website**
   - Creates website with HTTPS binding
   - Binds SSL certificate to site
   - Optionally adds HTTP binding
   - Associates with application pool

5. ? **Configure Website Settings**
   - Sets max request size to 100 MB
   - Enables HTTP compression
   - Enables HTTP logging
   - Configures request timeout (5 minutes)

6. ? **Start Services**
   - Starts application pool
   - Starts website

7. ? **Verify Deployment**
   - Tests health endpoint (`/health`)
   - Confirms API is responding

---

## Post-Deployment Configuration

After successful deployment, configure your application:

### 1. Connection Strings
Edit `C:\inetpub\SecureBootDashboard.Api\appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=SecureBootDashboard;User Id=sa;Password=YOUR_PASSWORD;"
  }
}
```

### 2. Azure Configuration (if using)
```json
{
  "AzureQueueSettings": {
    "ConnectionString": "DefaultEndpointsProtocol=https;...",
    "QueueName": "dashboard-reports"
  },
  "AzureStorageSettings": {
    "ConnectionString": "DefaultEndpointsProtocol=https;..."
  }
}
```

### 3. Logging Configuration
```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "C:\\Logs\\SecureBootDashboard\\api-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### 4. Mutual TLS (Optional)
```json
{
  "MutualTls": {
    "Enabled": true,
    "ClientCertificatePath": "C:\\Certificates\\client-ca.crt"
  }
}
```

### 5. Database Migrations
```powershell
# Apply EF Core migrations to database
cd SecureBootDashboard.Api
dotnet ef database update --configuration Release
```

---

## Troubleshooting

### Script Fails with "Cannot find drive 'IIS'"
**Cause**: WebAdministration module not available, using IISAdministration fallback

**Solution**: 
- This is expected and handled by script
- Script will use ServerManager API instead
- Advanced configuration steps will be skipped
- Configure advanced settings manually in IIS Manager if needed

### SSL Certificate Error
**Error**: "SSL certificate not found with thumbprint: ABC123..."

**Solution**:
1. Verify certificate is installed: `Get-ChildItem Cert:\LocalMachine\My`
2. Get correct thumbprint: `Get-ChildItem Cert:\LocalMachine\My | Select Thumbprint`
3. Remove spaces from thumbprint if copying
4. Re-run script with correct thumbprint

### Health Check Fails
**Error**: "API health check failed"

**Solution**:
1. Check if application pool is running: `Get-WebAppPoolState "SecureBootDashboard.Api"`
2. Check IIS logs: `C:\inetpub\logs\LogFiles\W3SVC*\`
3. Check application logs: `C:\Logs\SecureBootDashboard\`
4. Review Event Viewer: Application > ASP.NET Core
5. Verify appsettings.Production.json is valid JSON
6. Check database connection string

### Port Already in Use
**Error**: "Address already in use"

**Solution**:
1. Change HTTPS port: `.\Deploy-ApiServer.ps1 -HttpsPort 8443`
2. Or find what's using the port:
   ```powershell
   netstat -ano | findstr :443
   tasklist | findstr <PID>
   ```

### Website Won't Start
**Error**: "Unable to start website automatically"

**Solution**:
1. Check Application Pool status: `Get-WebAppPoolState "SecureBootDashboard.Api"`
2. Check physical path exists and is readable
3. Check appsettings files are valid JSON
4. Check IIS logs for detailed error messages
5. Manually start in IIS Manager to see error dialog

---

## Access Your API

After successful deployment, access your API:

**Health Check**:
```
https://api.yourdomain.com/health
```

**Swagger Documentation**:
```
https://api.yourdomain.com/swagger
```

**API Base URL**:
```
https://api.yourdomain.com/api
```

---

## Backup and Recovery

The script automatically creates backups:

**Location**: `C:\inetpub\SecureBootDashboard.Api.backup_YYYYMMDDHHMMSS\`

**Restore from Backup**:
```powershell
# If deployment fails, restore backup
Remove-Item C:\inetpub\SecureBootDashboard.Api -Recurse
Rename-Item C:\inetpub\SecureBootDashboard.Api.backup_<timestamp> `
           C:\inetpub\SecureBootDashboard.Api
```

---

## Support

For issues with:
- **IIS Configuration**: See Windows Server documentation
- **SSL Certificates**: See docs/SSL_CERTIFICATE_BYPASS.md
- **API Deployment**: See docs/SERVER_INFRASTRUCTURE_DEPLOYMENT.md
- **Azure Integration**: See docs/AZURE_DEPLOYMENT_GUIDE.md

