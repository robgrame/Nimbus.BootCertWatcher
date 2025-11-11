# Secure Boot Certificate Watcher

> **Monitor and govern the expiration and deployment of Secure Boot certificates across Windows fleets with real-time analytics.**

**Version 1.3** - Now with real-time SignalR updates and Excel/CSV export capabilities!

This solution monitors Secure Boot certificate status on Windows devices by capturing registry snapshots and Windows event logs, then transmitting reports to a centralized dashboard for compliance tracking, real-time monitoring, and alerting.

---

## 📸 Screenshots

### Dashboard Homepage
Modern analytics dashboard with interactive charts, real-time compliance monitoring, and SignalR updates.

*Screenshot placeholder - see [docs/SCREENSHOTS_GUIDE.md](docs/SCREENSHOTS_GUIDE.md) for instructions*

### Device Management
Comprehensive device list with advanced filtering, search capabilities, and export to Excel/CSV.

*Screenshot placeholder - see [docs/SCREENSHOTS_GUIDE.md](docs/SCREENSHOTS_GUIDE.md) for instructions*

### Analytics Charts
Interactive Chart.js visualizations showing compliance trends and deployment status.

*Screenshot placeholder - see [docs/SCREENSHOTS_GUIDE.md](docs/SCREENSHOTS_GUIDE.md) for instructions*

---

## ✨ Key Features

### 🔴 **NEW in v1.3** - Real-time Updates (SignalR)
- **Live Dashboard**: Instant notifications when new reports arrive
- **WebSocket Connection**: Real-time device status updates without page refresh
- **Connection Indicator**: Visual feedback for connection state (green/yellow/red/gray dot)
- **Auto-Reconnection**: Exponential backoff reconnection strategy (2s, 10s, 30s, 60s)
- **Toast Notifications**: Pop-up alerts for important events
- **Animated Updates**: Smooth transitions for statistics cards and charts

### 📊 **NEW in v1.3** - Export & Reporting
- **Excel Export**: Professional formatted exports with color-coding
  - Color-coded deployment states (green/yellow/red)
  - Auto-sized columns and frozen headers
  - Filterable data with summary rows
  - Export timestamp and metadata
- **CSV Export**: UTF-8 encoded exports for data analysis
  - Clean column names and calculated fields
  - Compatible with Excel, Power BI, and analytics tools
- **Bulk Export**: Export all devices or filtered subsets
- **Device-Specific**: Export individual device report history

### 📊 Dashboard & Analytics
- **Interactive Charts**: Compliance status, deployment states, and historical trends (Chart.js 4.4)
- **Real-time Monitoring**: Live device status with automatic updates via SignalR
- **Splash Screen**: Professional loading screen with smooth animations
- **Clickable Metrics**: Navigate from statistics cards directly to filtered device lists
- **Responsive Design**: Optimized for desktop, tablet, and mobile devices

### 💻 Device Management
- **Comprehensive Listing**: Complete inventory with filtering and search
- **Advanced Filters**: Filter by deployment state (Deployed, Pending, Error), fleet, or manufacturer
- **Search Functionality**: Find devices by name, domain, manufacturer, or model
- **Device Details**: Drill-down views with certificate chains and registry snapshots
- **Report History**: Full timeline of reports from each device
- **Export Options**: Download device lists and reports in Excel or CSV format

### 🔐 Certificate Tracking
- **Full Enumeration**: Extracts all X.509 certificates from UEFI firmware databases (db, dbx, KEK, PK)
- **Expiration Alerts**: Automatic warnings for expired or expiring certificates (90-day threshold)
- **Microsoft Detection**: Identifies Microsoft-issued certificates vs. third-party
- **Certificate Details**: Complete X.509 properties including algorithms, key sizes, validity periods

### 📡 Flexible Architecture
- **Multiple Sinks**: File share, Azure Queue Storage, or direct HTTP API ingestion
- **Hybrid Deployment**: Supports cloud (Azure App Service) and on-premises hosting
- **Dual Storage**: EF Core with SQL Server or file-based JSON storage
- **Queue Processing**: Background service for Azure Queue consumption
- **Real-time Hub**: SignalR WebSocket endpoint for live updates

### 🔒 Enterprise Security
- **Authentication**: Entra ID (Azure AD) and Windows Domain authentication
- **Managed Identity**: Azure AD authentication for database and storage access
- **Certificate-based Auth**: Client certificate authentication for Azure Queue
- **RBAC Support**: Fine-grained Azure role assignments
- **Network Isolation**: VNet integration and private endpoint support
- **Audit Logging**: Comprehensive Serilog logging with structured data

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
│  │  • Certificate enumeration            │  │
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
│  │  • Queue processor background service │  │
│  │  • Device/report query endpoints      │  │
│  │  • SignalR Hub for real-time updates  │  │
│  │  • Excel/CSV export endpoints         │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────┐
│  Dashboard Web (Razor Pages)                │
│  ┌───────────────────────────────────────┐  │
│  │ SecureBootDashboard.Web               │  │
│  │  • Device/report visualization        │  │
│  │  • Interactive analytics charts       │  │
│  │  • SignalR client for live updates    │  │
│  │  • Export buttons and handlers        │  │
│  │  • Compliance summaries               │  │
│  │  • Certificate details                │  │
│  │  • Alert history                      │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

---

## Components

### 1. **SecureBootWatcher.Client** (.NET Framework 4.8)
- Runs on managed Windows devices (desktops, servers, VMs)
- Supports two execution modes:
  - **Once**: Single-shot execution for scheduled tasks (default)
  - **Continuous**: Long-running service mode with periodic polling
- Captures Secure Boot servicing registry keys and relevant Windows event logs
- Enumerates UEFI firmware certificates (db, dbx, KEK, PK databases)
- Supports three reporting sinks:
  - **File Share**: writes JSON payloads to a network share
  - **Azure Queue**: enqueues messages via Azure Storage Queues (managed identity or certificate auth)
  - **Web API**: HTTP POST directly to the dashboard ingestion endpoint
- Configured via `appsettings.json` with fleet-specific settings

### 2. **SecureBootWatcher.Shared** (netstandard2.0)
- Shared models, configuration, validation, and storage contracts
- Used by client, API, and web projects for consistent DTOs
- Includes certificate enumeration models and validation logic

### 3. **SecureBootDashboard.Api** (ASP.NET Core 8)
- REST API for ingesting reports and querying aggregated data
- Two storage backends:
  - **EF Core + SQL Server**: full relational persistence with migrations
  - **File-based JSON store**: lightweight option for air-gapped or SQL-free environments
- Background queue processor for Azure Queue consumption
- **NEW**: SignalR Hub for real-time notifications
- **NEW**: Export service for Excel/CSV generation
- Endpoints:
  - `POST /api/SecureBootReports` – ingest client payloads (with SignalR broadcast)
  - `GET /api/Devices` – list devices with summaries
  - `GET /api/Devices/export/excel` – export all devices to Excel
  - `GET /api/Devices/export/csv` – export all devices to CSV
  - `GET /api/Devices/{id}` – device details
  - `GET /api/Devices/{id}/reports` – report history
  - `GET /api/Devices/{id}/reports/export/excel` – export device reports to Excel
  - `GET /api/Devices/{id}/reports/export/csv` – export device reports to CSV
  - `GET /api/SecureBootReports/{id}` – individual report details
  - `WS /dashboardHub` – SignalR WebSocket endpoint
  - `GET /health` – health checks

### 4. **SecureBootDashboard.Web** (ASP.NET Core 8 Razor Pages)
- Modern, responsive dashboard UI for viewing devices, reports, and compliance
- **Features**:
  - **NEW**: Real-time SignalR client with auto-reconnection
  - **NEW**: Export buttons for Excel/CSV downloads (coming soon)
  - Splash screen with smooth animations
  - Interactive Chart.js analytics (compliance, deployment states, trends)
  - Device list page with advanced filtering and search
  - Device details with certificate chain visualization
  - Report history with drill-down capabilities
  - About page with architecture and technology stack
  - Authentication support (Entra ID / Windows Domain)
- Consumes API endpoints with resilience policies (Polly)

---

## 🆕 What's New in v1.3

### Real-time Dashboard (SignalR)
```javascript
// Automatic connection to SignalR hub
// Live updates for:
- New report arrivals
- Device status changes
- Compliance metrics updates
- Device count changes
- System alerts
```

### Export Capabilities
```csharp
// Export all devices to Excel
GET /api/Devices/export/excel

// Export all devices to CSV
GET /api/Devices/export/csv

// Export device-specific reports
GET /api/Devices/{id}/reports/export/excel
GET /api/Devices/{id}/reports/export/csv
```

**Excel Features**:
- Color-coded deployment states
- Auto-sized columns with frozen headers
- Filterable data
- Summary rows with totals
- Professional formatting

**CSV Features**:
- UTF-8 encoding with BOM
- Calculated fields (status, age)
- Compatible with Excel and analytics tools

### Enhanced User Experience
- Visual connection status indicator
- Toast notifications for events
- Animated value transitions
- Smooth chart updates
- Professional loading screens

---

## Prerequisites

### Development
- **.NET SDK 8.0+** (for API & Web projects)
- **.NET Framework 4.8 Developer Pack** (for client)
- **SQL Server** (LocalDB, Express, or full instance) *or* configure file storage
- **Visual Studio 2022** or **VS Code** with C# extensions
- **PowerShell 5.0+** (for certificate enumeration and deployment scripts)

### Runtime (Client)
- **Windows 10/11** or **Windows Server 2016+** with UEFI firmware
- **.NET Framework 4.8 runtime**
- **PowerShell 5.0+** with SecureBoot module
- Administrator/SYSTEM privileges (for registry and certificate access)
- Network or Azure connectivity for sinks

### Runtime (Dashboard API/Web)
- **Azure App Service** (Linux or Windows) *or* on-premises IIS/Kestrel
- **SQL Server** (Azure SQL Database, SQL Managed Instance, or on-prem) *or* file storage mount
- **Azure Storage Account** (optional, for Azure Queue sink)
- **WebSocket support** (for SignalR real-time features)

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

### 6. Run the Dashboard (Quick Start)

**Using batch script (Windows):**
```powershell
.\start-dev.bat
```

**Using PowerShell script:**
```powershell
.\start-dev.ps1
```

**Manual start:**
```powershell
# Terminal 1: Start API
cd SecureBootDashboard.Api
dotnet run

# Terminal 2: Start Web
cd SecureBootDashboard.Web
dotnet run
```

Navigate to:
- **Web Dashboard**: `https://localhost:7001`
- **API Swagger**: `https://localhost:5001/swagger`
- **SignalR Hub**: `wss://localhost:5001/dashboardHub`

### 7. Run the Client
```powershell
cd SecureBootWatcher.Client\bin\Debug\net48
.\SecureBootWatcher.Client.exe
```
Watch logs for successful registry snapshot, certificate enumeration, event capture, and confirm payloads reach your API. You should see real-time updates in the dashboard!

---

## 📚 Documentation

Comprehensive documentation is available in the [`docs/`](docs/) directory:

### Getting Started
- **[Quick Start Guide](docs/QUICK_START_SPLASH_CHARTS.md)** - Get up and running in minutes
- **[Deployment Guide](docs/DEPLOYMENT_GUIDE.md)** - Production deployment instructions
- **[Configuration Guide](docs/PORT_CONFIGURATION.md)** - Customize ports and settings

### **NEW** - Q1 2025 Features
- **[SignalR Real-time Guide](docs/SIGNALR_REALTIME_COMPLETE.md)** - Complete SignalR implementation
- **[Q1 2025 Features Plan](docs/Q1_2025_FEATURES_PLAN.md)** - Feature roadmap and planning
- **[Export Implementation](docs/PULL_REQUEST_Q1_2025.md)** - Excel/CSV export details
- **[Merge Summary](docs/MERGE_MAIN_Q1_2025.md)** - v1.3 merge documentation

### Features & Implementation
- **[Dashboard Charts & Splash Screen](docs/DASHBOARD_CHARTS_SPLASH_IMPLEMENTATION.md)** - Analytics features
- **[Device List Separation](docs/DEVICE_LIST_SEPARATION.md)** - UI reorganization details
- **[Certificate Enumeration](docs/CERTIFICATE_ENUMERATION.md)** - UEFI certificate tracking
- **[Logo & Banner Implementation](docs/LOGO_BANNER_IMPLEMENTATION.md)** - Branding assets
- **[Authentication Setup](docs/AUTHENTICATION_SETUP.md)** - Entra ID and Windows auth

### Operations & Troubleshooting
- **[Logging Guide](docs/LOGGING_GUIDE.md)** - Serilog configuration and best practices
- **[Queue Processor Monitoring](docs/QUEUE_PROCESSOR_MONITORING.md)** - Background service health
- **[Retry & Resilience](docs/RETRY_RESILIENCE_GUIDE.md)** - Polly policies explained
- **[Troubleshooting Ports](docs/TROUBLESHOOTING_PORTS.md)** - Resolve port conflicts
- **[Host Aborted Issues](docs/HOSTABORTED_TROUBLESHOOTING.md)** - Fix cancellation errors

### Client Deployment
- **[Client Run Mode Configuration](docs/CLIENT_RUNMODE_CONFIGURATION.md)** - Single-shot vs continuous execution modes
- **[Client Deployment Scripts](docs/CLIENT_DEPLOYMENT_SCRIPTS.md)** - PowerShell automation
- **[Startup Logging](docs/STARTUP_LOGGING.md)** - Client initialization diagnostics
- **[Precompiled Package Deployment](docs/PRECOMPILED_PACKAGE_DEPLOYMENT.md)** - Package deployment

### Reference
- **[Architecture Summary](docs/WEB_IMPLEMENTATION_SUMMARY.md)** - Web dashboard components
- **[Device Deduplication](docs/DEVICE_DEDUPLICATION_SUMMARY.md)** - Data integrity mechanisms
- **[Screenshots Guide](docs/SCREENSHOTS_GUIDE.md)** - Create documentation screenshots

---

## 🚀 Quick Commands

### Development
```powershell
# Build all projects
dotnet build

# Run tests
dotnet test

# Start development servers (with SignalR)
.\start-dev.ps1

# Deploy client locally
.\scripts\Deploy-Client.ps1 -CreateScheduledTask -ApiBaseUrl "https://localhost:5001"

# Export devices (via API)
curl https://localhost:5001/api/Devices/export/excel -o devices.xlsx
curl https://localhost:5001/api/Devices/export/csv -o devices.csv
```

### Production
```powershell
# Publish API (with SignalR hub)
dotnet publish SecureBootDashboard.Api -c Release -o ./publish/api

# Publish Web (with SignalR client)
dotnet publish SecureBootDashboard.Web -c Release -o ./publish/web

# Deploy client package
.\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://app-prod.azurewebsites.net" -FleetId "prod"
```

---

## Technology Stack

### Frontend
- **ASP.NET Core 8** - Razor Pages
- **Bootstrap 5** - Responsive UI framework
- **Chart.js 4.4** - Interactive analytics charts
- **SignalR Client 8.0** - Real-time WebSocket client *(NEW)*
- **Font Awesome 6.5** - Icon library
- **jQuery 3.x** - DOM manipulation

### Backend
- **ASP.NET Core 8** - Web API
- **SignalR 1.2.0** - Real-time communication hub *(NEW)*
- **ClosedXML 0.105.0** - Excel export generation *(NEW)*
- **CsvHelper 33.1.0** - CSV export generation *(NEW)*
- **Entity Framework Core 8** - ORM with SQL Server
- **Polly 8.x** - Resilience and retry policies
- **Serilog 3.x** - Structured logging
- **Azure SDK for .NET** - Azure Storage integration
- **Microsoft.Identity.Web** - Authentication *(NEW)*

### Client
- **.NET Framework 4.8** - Windows compatibility
- **PowerShell 5.0+** - Certificate enumeration
- **Windows Registry API** - Registry polling
- **Event Log API** - Event capture

### Infrastructure
- **Azure App Service** - Hosting (or IIS on-prem)
- **Azure SQL Database** - Data persistence
- **Azure Queue Storage** - Message buffering
- **Azure Monitor** - Telemetry and diagnostics
- **Azure Key Vault** - Secrets management
- **WebSocket Support** - SignalR real-time features *(NEW)*

---

## 🎯 Roadmap

### ✅ v1.3 - Q1 2025 (Current - 37% Complete)
- [x] **Real-time dashboard updates (SignalR)** - Complete
- [x] **Export reports to Excel/CSV** - Backend Complete
- [ ] Export UI in web dashboard (In Progress)
- [ ] Dark mode theme support
- [ ] Custom alert thresholds per fleet

### v1.4 - Q2 2025
- [ ] Complete export UI with download buttons
- [ ] Dark mode theme implementation
- [ ] Fleet-specific alert thresholds
- [ ] Enhanced compliance policies

### v2.0 - Q3 2025
- [ ] Multi-tenant support with RBAC
- [ ] Certificate compliance policies
- [ ] Automated remediation workflows
- [ ] Enhanced analytics (30/60/90 day trends)
- [ ] Machine learning anomaly detection

### v3.0 - Q4 2025
- [ ] Linux client support (.NET 8)
- [ ] API v2 with GraphQL
- [ ] Integration with ServiceNow/Jira
- [ ] Mobile app for iOS/Android

See [Q1 2025 Features Plan](docs/Q1_2025_FEATURES_PLAN.md) for detailed roadmap.

---

## Security Considerations

- **Authentication**: Entra ID (Azure AD) and Windows Domain authentication for dashboard access
- **SignalR Security**: Hub endpoints open in development; add `[Authorize]` for production
- **Export Authorization**: Add authorization checks to export endpoints before production
- **Managed Identity**: Use Azure Managed Identity for API → SQL Database and Client → Storage Queue
- **Certificate Auth**: Client supports certificate-based authentication for Azure Queue Storage
- **TLS/HTTPS**: Enforce HTTPS for all API endpoints; use valid certificates in production
- **WebSocket Security**: Ensure WebSocket connections use WSS (secure) protocol
- **Network Isolation**: Place API in private VNet with App Gateway or Front Door
- **RBAC**: Restrict SQL Database and Storage Queue access with Azure role assignments
- **Secrets Management**: Use Azure Key Vault for connection strings and sensitive configuration

---

## Monitoring & Operations

- **Application Insights**: Enable on both API and Web app services for telemetry, exceptions, and performance traces
- **Log Analytics**: Configure diagnostic logs for SQL Database to track query performance and throttling
- **Serilog Structured Logging**: Comprehensive logging with file rotation and console output
- **Health Checks**: Built-in health endpoints for API (`/health`) and queue processor
- **SignalR Monitoring**: Track connection count, message throughput, and reconnection events
- **Export Metrics**: Monitor export request count, file sizes, and generation time
- **Alerts**: Set up Azure Monitor alerts for:
  - API 500 errors or high latency
  - SQL DTU/CPU thresholds
  - Queue message age (if using Azure Queue)
  - Certificate expiration warnings
  - Client connectivity failures
  - SignalR connection drops
  - Export operation failures

---

## Troubleshooting

### Client not sending reports
1. Check `appsettings.json` sink configuration
2. Verify network connectivity to API or Azure Storage
3. Review client logs (console or Windows Event Log if configured)
4. Confirm `.NET Framework 4.8` runtime installed
5. Ensure PowerShell 5.0+ with SecureBoot module (for certificate enumeration)
6. Run as Administrator to access registry and UEFI variables

### API ingestion failures
1. Check SQL connection string and firewall rules
2. Review API logs via Application Insights or `dotnet run` console
3. Validate EF migrations applied: `dotnet ef migrations list`
4. Test health endpoint: `GET /health`
5. Verify queue processor is running (if using Azure Queue)
6. Check SignalR hub accessibility: `WS /dashboardHub`

### Missing data in dashboard
1. Confirm API is reachable from web app
2. Check `ApiBaseUrl` setting in web app configuration
3. Verify reports exist in database: query `SecureBootReports` table
4. Review web app logs for HTTP errors
5. Check browser console for JavaScript errors
6. Verify SignalR connection in browser dev tools

### SignalR not working
1. Check browser console for WebSocket errors
2. Verify SignalR hub endpoint: `wss://yourhost/dashboardHub`
3. Ensure WebSocket support in browser (Chrome/Edge/Firefox)
4. Check network allows WebSocket connections (port 443 for WSS)
5. Review API logs for SignalR broadcast errors
6. Test connection: `Ping()` method in browser dev tools

### Export failures
1. Verify export endpoints are accessible
2. Check API logs for export service errors
3. Ensure sufficient memory for large datasets
4. Test with small dataset first
5. Review browser download settings

### Certificate enumeration issues
1. Verify Secure Boot is enabled: `Confirm-SecureBootUEFI`
2. Check PowerShell version: `$PSVersionTable.PSVersion` (requires 5.0+)
3. Ensure SecureBoot module is available: `Get-Module -ListAvailable SecureBoot`
4. Run PowerShell as Administrator
5. Review client logs for certificate-related errors

For detailed troubleshooting steps, see:
- [Troubleshooting Guide](docs/TROUBLESHOOTING_PORTS.md)
- [Host Aborted Guide](docs/HOSTABORTED_TROUBLESHOOTING.md)
- [SignalR Implementation Guide](docs/SIGNALR_REALTIME_COMPLETE.md)

---

## Contributing

Contributions welcome! Please:
1. Open issues for bugs or feature requests
2. Fork the repository for enhancements
3. Submit pull requests with clear descriptions
4. Follow existing code style and conventions
5. Include tests for new features
6. Update documentation as needed

See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines.

---

## License

This project is licensed under the **MIT License**. See [LICENSE](LICENSE) for details.

### Third-Party Licenses
- **SignalR** - Apache 2.0 *(NEW)*
- **ClosedXML** - MIT License *(NEW)*
- **CsvHelper** - MS-PL / Apache 2.0 *(NEW)*
- **Chart.js** - MIT License
- **Bootstrap** - MIT License
- **Font Awesome** - SIL OFL 1.1 / MIT
- **Serilog** - Apache 2.0
- **Polly** - BSD 3-Clause
- **Entity Framework Core** - MIT License
- **Azure SDK** - MIT License

---

## Support

For questions, issues, or support:
- **Documentation**: See [docs/](docs/) directory
- **GitHub Issues**: [Report bugs or request features](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)
- **GitHub Discussions**: [Ask questions or share ideas](https://github.com/robgrame/Nimbus.BootCertWatcher/discussions)
- **Email**: Contact repository maintainers (see [Contributors](https://github.com/robgrame/Nimbus.BootCertWatcher/graphs/contributors))

---

## Acknowledgments

- **Microsoft** - Secure Boot specifications, UEFI guidance, and SignalR framework
- **Chart.js Community** - Excellent charting library
- **Bootstrap Team** - Responsive design framework
- **Serilog Contributors** - Robust logging infrastructure
- **Azure SDK Team** - Comprehensive cloud integration
- **ClosedXML Contributors** - Professional Excel generation
- **CsvHelper Contributors** - Robust CSV handling

---

<div align="center">

**Made with ❤️ for the IT Community**

**Version 1.3** - Real-time monitoring with SignalR and Excel/CSV export

[⬆ Back to Top](#secure-boot-certificate-watcher)

</div>
