# Secure Boot Certificate Watcher

> **Monitor and govern the expiration and deployment of Secure Boot certificates across Windows fleets.**

This solution monitors Secure Boot certificate status on Windows devices by capturing registry snapshots and Windows event logs, then transmitting reports to a centralized dashboard for compliance tracking and alerting.

---

## Overview

**Secure Boot Certificate Watcher** is a multi-component system designed to help IT operations teams track Secure Boot certificate updates—particularly the UEFI CA 2023 rollout—and identify devices requiring intervention before certificates expire.

### Architecture

```
┌─────────────────────────────────────────────┐
│  Windows Devices (.NET Framework 4.8)       │
│  ┌───────────────────────────────────────┐  │
│  │ SecureBootWatcher.Client              │  │
│  │  • Registry polling                   │  │
│  │  • Event log capture                  │  │
│  │  • Local/Queue/HTTP sinks             │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
                    │
                    ▼
    ┌───────────────────────────────┐
    │  Azure Queue Storage          │
    │  (optional message buffer)    │
    └───────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────┐
│  Dashboard API (ASP.NET Core 8)             │
│  ┌───────────────────────────────────────┐  │
│  │ SecureBootDashboard.Api               │  │
│  │  • Report ingestion (POST)            │  │
│  │  • EF Core or file-based persistence  │  │
│  │  • Recent reports & detail queries    │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────┐
│  Dashboard Web (Razor Pages)                │
│  ┌───────────────────────────────────────┐  │
│  │ SecureBootDashboard.Web               │  │
│  │  • Device/report visualization        │  │
│  │  • Compliance summaries               │  │
│  │  • Alert history                      │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

---

## Components

### 1. **SecureBootWatcher.Client** (.NET Framework 4.8)
- Runs on managed Windows devices (desktops, servers, VMs)
- Captures Secure Boot servicing registry keys and relevant Windows event logs
- Supports three reporting sinks:
  - **File Share**: writes JSON payloads to a network share
  - **Azure Queue**: enqueues messages via Azure Storage Queues (managed identity or connection string)
  - **Web API**: HTTP POST directly to the dashboard ingestion endpoint

### 2. **SecureBootWatcher.Shared** (netstandard2.0)
- Shared models, configuration, validation, and storage contracts
- Used by client, API, and web projects for consistent DTOs

### 3. **SecureBootDashboard.Api** (ASP.NET Core 8)
- REST API for ingesting reports and querying aggregated data
- Two storage backends:
  - **EF Core + SQL Server**: full relational persistence with migrations
  - **File-based JSON store**: lightweight option for air-gapped or SQL-free environments
- Endpoints:
  - `POST /api/SecureBootReports` – ingest client payloads
  - `GET /api/SecureBootReports/{id}` – retrieve individual report details
  - `GET /api/SecureBootReports/recent?limit=50` – list recent reports
  - `GET /health` – health checks

### 4. **SecureBootDashboard.Web** (ASP.NET Core 8 Razor Pages)
- Dashboard UI for viewing devices, reports, compliance status, and alerts
- Consumes API endpoints to render summaries and drill-down views

---

## Prerequisites

### Development
- **.NET SDK 8.0+** (for API & Web projects)
- **.NET Framework 4.8 Developer Pack** (for client)
- **SQL Server** (LocalDB, Express, or full instance) *or* configure file storage
- **Visual Studio 2022** or **VS Code** with C# extensions

### Runtime (Client)
- **Windows 10/11** or **Windows Server 2016+**
- **.NET Framework 4.8 runtime**
- Network or Azure connectivity for sinks

### Runtime (Dashboard API/Web)
- **Azure App Service** (Linux or Windows) *or* on-premises IIS/Kestrel
- **SQL Server** (Azure SQL Database, SQL Managed Instance, or on-prem) *or* file storage mount
- **Azure Storage Account** (optional, for Azure Queue sink)

---

## Getting Started

### 1. Clone the Repository
```powershell
git clone https://github.com/robgrame/Nimbus.BootCertWatcher.git
cd Nimbus.BootCertWatcher
```

### 2. Build the Solution
```powershell
dotnet build SecureBootWatcher.sln
```

### 3. Configure the Client
Edit `SecureBootWatcher.Client\appsettings.json`:
```json
{
  "SecureBootWatcher": {
    "FleetId": "fleet-01",
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    "EventLookbackPeriod": "1.00:00:00",
    "Sinks": {
      "EnableWebApi": true,
      "WebApi": {
        "BaseAddress": "https://your-dashboard-api.azurewebsites.net",
        "IngestionRoute": "/api/SecureBootReports"
      }
    }
  }
}
```

### 4. Configure the API
Edit `SecureBootDashboard.Api\appsettings.json`:
```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=your-server.database.windows.net;Database=SecureBootDashboard;User Id=your-user;Password=your-password;TrustServerCertificate=True"
  },
  "Storage": {
    "Provider": "EfCore"
  }
}
```

**Alternative (File Storage)**:
```json
{
  "Storage": {
    "Provider": "File",
    "File": {
      "BasePath": "C:\\ProgramData\\SecureBootDashboard\\reports"
    }
  }
}
```

### 5. Apply EF Core Migrations
```powershell
dotnet ef database update --project SecureBootDashboard.Api
```

### 6. Run the API
```powershell
dotnet run --project SecureBootDashboard.Api
```
Navigate to `https://localhost:5001/swagger` to test ingestion endpoints.

### 7. Run the Client
```powershell
cd SecureBootWatcher.Client\bin\Debug\net48
.\SecureBootWatcher.Client.exe
```
Watch logs for successful registry snapshot and event capture, then confirm payloads reach your API.

---

## Deployment Guide

### Azure Resources Required

#### 1. **Azure SQL Database** (if using EF Core storage)
- **SKU**: Basic/Standard tier (S0 or higher) or serverless
- **Firewall**: allow Azure services + your on-prem IPs
- **Authentication**: SQL auth or Azure AD managed identity

#### 2. **Azure App Service (API)**
- **Plan**: Basic B1 or higher (Linux or Windows)
- **Runtime**: .NET 8
- **Configuration**:
  - Add connection string `SqlServer` in **Application Settings**
  - Set `Storage__Provider=EfCore`
  - Optionally enable managed identity and grant SQL DB access

#### 3. **Azure App Service (Web)**
- **Plan**: can share same plan as API or separate
- **Runtime**: .NET 8
- **Configuration**: point `ApiBaseUrl` to your API app service URL

#### 4. **Azure Storage Account** (optional, for Azure Queue sink)
- **SKU**: Standard LRS/GRS
- **Queue**: create queue named `secureboot-reports`
- **Access**: generate SAS token or enable managed identity for clients

---

### Step-by-Step Deployment

#### **A. Provision Azure Resources**

1. **Create Resource Group**
   ```bash
   az group create --name rg-secureboot-prod --location eastus
   ```

2. **Create Azure SQL Server & Database**
   ```bash
   az sql server create \
     --name sql-secureboot-prod \
     --resource-group rg-secureboot-prod \
     --location eastus \
     --admin-user sqladmin \
     --admin-password "YourSecureP@ssw0rd!"

   az sql db create \
     --resource-group rg-secureboot-prod \
     --server sql-secureboot-prod \
     --name SecureBootDashboard \
     --service-objective S0
   ```

3. **Create App Service Plan**
   ```bash
   az appservice plan create \
     --name plan-secureboot-prod \
     --resource-group rg-secureboot-prod \
     --sku B1 \
     --is-linux
   ```

4. **Create API App Service**
   ```bash
   az webapp create \
     --name app-secureboot-api-prod \
     --resource-group rg-secureboot-prod \
     --plan plan-secureboot-prod \
     --runtime "DOTNETCORE:8.0"
   ```

5. **Create Web App Service**
   ```bash
   az webapp create \
     --name app-secureboot-web-prod \
     --resource-group rg-secureboot-prod \
     --plan plan-secureboot-prod \
     --runtime "DOTNETCORE:8.0"
   ```

6. **(Optional) Create Storage Account for Queue**
   ```bash
   az storage account create \
     --name stsecurebootprod \
     --resource-group rg-secureboot-prod \
     --location eastus \
     --sku Standard_LRS

   az storage queue create \
     --name secureboot-reports \
     --account-name stsecurebootprod
   ```

#### **B. Configure API App Service**

1. **Set Connection String**
   ```bash
   az webapp config connection-string set \
     --name app-secureboot-api-prod \
     --resource-group rg-secureboot-prod \
     --settings SqlServer="Server=tcp:sql-secureboot-prod.database.windows.net,1433;Database=SecureBootDashboard;User ID=sqladmin;Password=YourSecureP@ssw0rd!;Encrypt=True;TrustServerCertificate=False" \
     --connection-string-type SQLAzure
   ```

2. **Set Application Settings**
   ```bash
   az webapp config appsettings set \
     --name app-secureboot-api-prod \
     --resource-group rg-secureboot-prod \
     --settings Storage__Provider=EfCore
   ```

3. **Apply EF Migrations** (via local machine or CI/CD)
   ```powershell
   # Update connection string in appsettings.json temporarily
   dotnet ef database update --project SecureBootDashboard.Api
   ```

4. **Deploy API**
   ```bash
   cd SecureBootDashboard.Api
   dotnet publish -c Release -o ./publish
   az webapp deploy \
     --name app-secureboot-api-prod \
     --resource-group rg-secureboot-prod \
     --src-path ./publish.zip
   ```

#### **C. Configure Web App Service**

1. **Set API Base URL**
   ```bash
   az webapp config appsettings set \
     --name app-secureboot-web-prod \
     --resource-group rg-secureboot-prod \
     --settings ApiBaseUrl=https://app-secureboot-api-prod.azurewebsites.net
   ```

2. **Deploy Web**
   ```bash
   cd SecureBootDashboard.Web
   dotnet publish -c Release -o ./publish
   az webapp deploy \
     --name app-secureboot-web-prod \
     --resource-group rg-secureboot-prod \
     --src-path ./publish.zip
   ```

#### **D. Deploy Client to Endpoints**

1. **Build Client**
   ```powershell
   dotnet publish SecureBootWatcher.Client -c Release -r win-x86 --self-contained false
   ```

2. **Package Output**
   - ZIP contents of `SecureBootWatcher.Client\bin\Release\net48\win-x86\publish\`
   - Distribute via Group Policy, SCCM, Intune, or manual install

3. **Configure Client** (each endpoint)
   - Edit `appsettings.json`:
     ```json
     {
       "SecureBootWatcher": {
         "Sinks": {
           "EnableWebApi": true,
           "WebApi": {
             "BaseAddress": "https://app-secureboot-api-prod.azurewebsites.net",
             "IngestionRoute": "/api/SecureBootReports"
           }
         }
       }
     }
     ```
   - Optionally configure Azure Queue sink with storage account endpoint

4. **Schedule Client Execution**
   - Create scheduled task:
     ```powershell
     $action = New-ScheduledTaskAction -Execute "C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe"
     $trigger = New-ScheduledTaskTrigger -Daily -At "09:00AM"
     Register-ScheduledTask -TaskName "SecureBootWatcher" -Action $action -Trigger $trigger -User "SYSTEM"
     ```

---

## Configuration Reference

### Client (`appsettings.json`)
| Setting | Description | Default |
|---------|-------------|---------|
| `FleetId` | Optional fleet/group identifier | `null` |
| `RegistryPollInterval` | How often to snapshot registry | `00:30:00` |
| `EventQueryInterval` | How often to query event logs | `00:30:00` |
| `EventLookbackPeriod` | Event log time window | `1.00:00:00` |
| `Sinks.EnableFileShare` | Write reports to file share | `false` |
| `Sinks.FileShare.RootPath` | UNC path for file sink | `null` |
| `Sinks.EnableAzureQueue` | Enqueue reports to Azure Queue | `false` |
| `Sinks.AzureQueue.QueueEndpoint` | Storage queue URI | `null` |
| `Sinks.AzureQueue.ConnectionString` | Storage connection string | `null` |
| `Sinks.EnableWebApi` | POST reports to dashboard API | `false` |
| `Sinks.WebApi.BaseAddress` | API base URL | `null` |

### API (`appsettings.json`)
| Setting | Description | Default |
|---------|-------------|---------|
| `ConnectionStrings.SqlServer` | SQL Server connection string | LocalDB default |
| `Storage.Provider` | `EfCore` or `File` | `EfCore` |
| `Storage.File.BasePath` | Directory for JSON file store | `C:\ProgramData\SecureBootDashboard\reports` |

---

## Security Considerations

- **Managed Identity**: Use Azure Managed Identity for API → SQL Database and Client → Storage Queue to avoid credential exposure
- **TLS/HTTPS**: Enforce HTTPS for all API endpoints; use valid certificates in production
- **Network Isolation**: Place API in private VNet with App Gateway or Front Door if exposing publicly
- **RBAC**: Restrict SQL Database access to API service principal only
- **Client Authentication**: Consider adding bearer token or certificate-based auth to API ingestion endpoint for production

---

## Monitoring & Operations

- **Application Insights**: Enable on both API and Web app services for telemetry, exceptions, and performance traces
- **Log Analytics**: Configure diagnostic logs for SQL Database to track query performance and throttling
- **Alerts**: Set up Azure Monitor alerts for:
  - API 500 errors or high latency
  - SQL DTU/CPU thresholds
  - Queue message age (if using Azure Queue)

---

## Troubleshooting

### Client not sending reports
1. Check `appsettings.json` sink configuration
2. Verify network connectivity to API or Azure Storage
3. Review client logs (console or Windows Event Log if configured)
4. Confirm `.NET Framework 4.8` runtime installed

### API ingestion failures
1. Check SQL connection string and firewall rules
2. Review API logs via Application Insights or `dotnet run` console
3. Validate EF migrations applied: `dotnet ef migrations list`
4. Test health endpoint: `GET /health`

### Missing data in dashboard
1. Confirm API is reachable from web app
2. Check `ApiBaseUrl` setting in web app configuration
3. Verify reports exist in database: query `SecureBootReports` table
4. Review web app logs for HTTP errors

---

## Contributing

Contributions welcome! Please open issues for bugs or feature requests, and submit pull requests for enhancements.

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

---

## Support

For questions or support, contact the repository maintainer or open a GitHub issue.
