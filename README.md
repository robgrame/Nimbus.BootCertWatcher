# Secure Boot Certificate Watcher

> **Monitor and govern the expiration and deployment of Secure Boot certificates across Windows fleets.**

This solution monitors Secure Boot certificate status on Windows devices by capturing registry snapshots and Windows event logs, then transmitting reports to a centralized dashboard for compliance tracking and alerting.

---

## 📸 Screenshots

### Dashboard Homepage
Modern analytics dashboard with interactive charts and real-time compliance monitoring.

*Screenshot placeholder - see [docs/SCREENSHOTS_GUIDE.md](docs/SCREENSHOTS_GUIDE.md) for instructions*

### Device Management
Comprehensive device list with advanced filtering and search capabilities.

*Screenshot placeholder - see [docs/SCREENSHOTS_GUIDE.md](docs/SCREENSHOTS_GUIDE.md) for instructions*

### Analytics Charts
Interactive Chart.js visualizations showing compliance trends and deployment status.

*Screenshot placeholder - see [docs/SCREENSHOTS_GUIDE.md](docs/SCREENSHOTS_GUIDE.md) for instructions*

---

## ✨ Key Features

### 📊 Dashboard & Analytics
- **Interactive Charts**: Compliance status, deployment states, and historical trends (Chart.js 4.4)
- **Real-time Monitoring**: Live device status with customizable refresh intervals
- **Splash Screen**: Professional loading screen with smooth animations
- **Clickable Metrics**: Navigate from statistics cards directly to filtered device lists
- **Responsive Design**: Optimized for desktop, tablet, and mobile devices

### 💻 Device Management
- **Comprehensive Listing**: Complete inventory with filtering and search
- **Advanced Filters**: Filter by deployment state (Deployed, Pending, Error), fleet, or manufacturer
- **Search Functionality**: Find devices by name, domain, manufacturer, or model
- **Device Details**: Drill-down views with certificate chains and registry snapshots
- **Report History**: Full timeline of reports from each device

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

### 🔒 Enterprise Security
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
- Captures Secure Boot servicing registry keys and relevant Windows event logs
- **NEW**: Enumerates UEFI firmware certificates (db, dbx, KEK, PK databases)
- Supports three reporting sinks:
  - **File Share**: writes JSON payloads to a network share
  - **Azure Queue**: enqueues messages via Azure Storage Queues (managed identity or certificate auth)
  - **Web API**: HTTP POST directly to the dashboard ingestion endpoint
- Configured via `appsettings.json` with fleet-specific settings

### 2. **SecureBootWatcher.LinuxClient** (.NET 8) **NEW**
- Runs on Linux systems with UEFI firmware support
- Cross-platform client for monitoring Secure Boot certificates on Linux
- Reads EFI variables from `/sys/firmware/efi/efivars`
- Queries systemd journal (journald) for boot-related events
- Enumerates UEFI certificates using direct EFI variable access
- Supports same three reporting sinks as Windows client
- Targets `linux-x64` and `linux-arm64` architectures
- Configured via `appsettings.json` with fleet-specific settings

### 3. **SecureBootWatcher.Shared** (netstandard2.0)
- Shared models, configuration, validation, and storage contracts
- Used by client, API, and web projects for consistent DTOs
- Includes certificate enumeration models and validation logic

### 4. **SecureBootDashboard.Api** (ASP.NET Core 8)
- REST API for ingesting reports and querying aggregated data
- Two storage backends:
  - **EF Core + SQL Server**: full relational persistence with migrations
  - **File-based JSON store**: lightweight option for air-gapped or SQL-free environments
- Background queue processor for Azure Queue consumption
- Endpoints:
  - `POST /api/SecureBootReports` – ingest client payloads
  - `GET /api/Devices` – list devices with summaries
  - `GET /api/Devices/{id}` – device details
  - `GET /api/Devices/{id}/reports` – report history
  - `GET /api/SecureBootReports/{id}` – individual report details
  - `GET /health` – health checks

### 5. **SecureBootDashboard.Web** (ASP.NET Core 8 Razor Pages)
- Modern, responsive dashboard UI for viewing devices, reports, and compliance
- **Features**:
  - Splash screen with smooth animations
  - Interactive Chart.js analytics (compliance, deployment states, trends)
  - Device list page with advanced filtering and search
  - Device details with certificate chain visualization
  - Report history with drill-down capabilities
  - About page with architecture and technology stack
- Consumes API endpoints with resilience policies (Polly)

---

## Prerequisites

### Development
- **.NET SDK 8.0+** (for API, Web, and Linux client projects)
- **.NET Framework 4.8 Developer Pack** (for Windows client)
- **SQL Server** (LocalDB, Express, or full instance) *or* configure file storage
- **Visual Studio 2022** or **VS Code** with C# extensions
- **PowerShell 5.0+** (for Windows certificate enumeration and deployment scripts)

### Runtime (Windows Client)
- **Windows 10/11** or **Windows Server 2016+** with UEFI firmware
- **.NET Framework 4.8 runtime**
- **PowerShell 5.0+** with SecureBoot module
- Administrator/SYSTEM privileges (for registry and certificate access)
- Network or Azure connectivity for sinks

### Runtime (Linux Client) **NEW**
- **Linux distribution** with UEFI firmware support (Ubuntu 20.04+, RHEL 8+, Debian 11+, etc.)
- **.NET 8 runtime** (`dotnet-runtime-8.0`)
- **systemd** with journald (for event logging)
- Root/sudo privileges (for EFI variable access at `/sys/firmware/efi/efivars`)
- Network or Azure connectivity for sinks
- Optional: `mokutil` for Secure Boot status checking
- **PowerShell 5.0+** with SecureBoot module
- Administrator/SYSTEM privileges (for registry and certificate access)
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

### 7. Run the Windows Client
```powershell
cd SecureBootWatcher.Client\bin\Debug\net48
.\SecureBootWatcher.Client.exe
```
Watch logs for successful registry snapshot, certificate enumeration, event capture, and confirm payloads reach your API.

### 8. Run the Linux Client (Optional)
```bash
cd SecureBootWatcher.LinuxClient
dotnet run

# Or publish and run as self-contained:
dotnet publish -c Release -r linux-x64 --self-contained
cd bin/Release/net8.0/linux-x64/publish
sudo ./SecureBootWatcher.LinuxClient
```
**Note**: Root/sudo privileges are required to access EFI variables at `/sys/firmware/efi/efivars`.

Watch logs for successful EFI variable access, certificate enumeration, journal event capture, and confirm payloads reach your API.

---

## 📚 Documentation

Comprehensive documentation is available in the [`docs/`](docs/) directory:

### Getting Started
- **[Quick Start Guide](docs/QUICK_START_SPLASH_CHARTS.md)** - Get up and running in minutes
- **[Deployment Guide](docs/DEPLOYMENT_GUIDE.md)** - Production deployment instructions
- **[Configuration Guide](docs/PORT_CONFIGURATION.md)** - Customize ports and settings

### Features & Implementation
- **[Dashboard Charts & Splash Screen](docs/DASHBOARD_CHARTS_SPLASH_IMPLEMENTATION.md)** - Analytics features
- **[Device List Separation](docs/DEVICE_LIST_SEPARATION.md)** - UI reorganization details
- **[Certificate Enumeration](docs/CERTIFICATE_ENUMERATION.md)** - UEFI certificate tracking
- **[Logo & Banner Implementation](docs/LOGO_BANNER_IMPLEMENTATION.md)** - Branding assets

### Operations & Troubleshooting
- **[Logging Guide](docs/LOGGING_GUIDE.md)** - Serilog configuration and best practices
- **[Queue Processor Monitoring](docs/QUEUE_PROCESSOR_MONITORING.md)** - Background service health
- **[Retry & Resilience](docs/RETRY_RESILIENCE_GUIDE.md)** - Polly policies explained
- **[Troubleshooting Ports](docs/TROUBLESHOOTING_PORTS.md)** - Resolve port conflicts
- **[Host Aborted Issues](docs/HOSTABORTED_TROUBLESHOOTING.md)** - Fix cancellation errors

### Client Deployment
- **[Client Deployment Scripts](docs/CLIENT_DEPLOYMENT_SCRIPTS.md)** - PowerShell automation
- **[Startup Logging](docs/STARTUP_LOGGING.md)** - Client initialization diagnostics

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

# Start development servers
.\start-dev.ps1

# Deploy client locally
.\scripts\Deploy-Client.ps1 -CreateScheduledTask -ApiBaseUrl "https://localhost:5001"
```

### Production
```powershell
# Publish API
dotnet publish SecureBootDashboard.Api -c Release -o ./publish/api

# Publish Web
dotnet publish SecureBootDashboard.Web -c Release -o ./publish/web

# Deploy client package
.\scripts\Deploy-Client.ps1 -ApiBaseUrl "https://app-prod.azurewebsites.net" -FleetId "prod"
```

---

## Security Considerations

- **Managed Identity**: Use Azure Managed Identity for API → SQL Database and Client → Storage Queue to avoid credential exposure
- **Certificate Auth**: Client supports certificate-based authentication for Azure Queue Storage
- **TLS/HTTPS**: Enforce HTTPS for all API endpoints; use valid certificates in production
- **Network Isolation**: Place API in private VNet with App Gateway or Front Door if exposing publicly
- **RBAC**: Restrict SQL Database and Storage Queue access with Azure role assignments
- **Client Authentication**: Consider adding bearer token or certificate-based auth to API ingestion endpoint for production
- **Secrets Management**: Use Azure Key Vault for connection strings and sensitive configuration

---

## Monitoring & Operations

- **Application Insights**: Enable on both API and Web app services for telemetry, exceptions, and performance traces
- **Log Analytics**: Configure diagnostic logs for SQL Database to track query performance and throttling
- **Serilog Structured Logging**: Comprehensive logging with file rotation and console output
- **Health Checks**: Built-in health endpoints for API (`/health`) and queue processor
- **Alerts**: Set up Azure Monitor alerts for:
  - API 500 errors or high latency
  - SQL DTU/CPU thresholds
  - Queue message age (if using Azure Queue)
  - Certificate expiration warnings
  - Client connectivity failures

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

### Missing data in dashboard
1. Confirm API is reachable from web app
2. Check `ApiBaseUrl` setting in web app configuration
3. Verify reports exist in database: query `SecureBootReports` table
4. Review web app logs for HTTP errors
5. Check browser console for JavaScript errors

### Certificate enumeration issues
1. Verify Secure Boot is enabled: `Confirm-SecureBootUEFI`
2. Check PowerShell version: `$PSVersionTable.PSVersion` (requires 5.0+)
3. Ensure SecureBoot module is available: `Get-Module -ListAvailable SecureBoot`
4. Run PowerShell as Administrator
5. Review client logs for certificate-related errors

For detailed troubleshooting steps, see the [Troubleshooting Guide](docs/TROUBLESHOOTING_PORTS.md) and [Host Aborted Guide](docs/HOSTABORTED_TROUBLESHOOTING.md).

---

## Technology Stack

### Frontend
- **ASP.NET Core 8** - Razor Pages
- **Bootstrap 5** - Responsive UI framework
- **Chart.js 4.4** - Interactive analytics charts
- **Font Awesome 6.5** - Icon library
- **jQuery 3.x** - DOM manipulation

### Backend
- **ASP.NET Core 8** - Web API
- **Entity Framework Core 8** - ORM with SQL Server
- **Polly 8.x** - Resilience and retry policies
- **Serilog 3.x** - Structured logging
- **Azure SDK for .NET** - Azure Storage integration

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

## Roadmap

### v1.1 (Q1 2025)
- [ ] Real-time dashboard updates (SignalR)
- [ ] Export reports to Excel/CSV
- [ ] Dark mode theme support
- [ ] Custom alert thresholds per fleet

### v1.2 (Q2 2025)
- [ ] Multi-tenant support with RBAC
- [ ] Certificate compliance policies
- [ ] Automated remediation workflows
- [ ] Enhanced analytics (30/60/90 day trends)

### v2.0 (Q3 2025)
- [x] Linux client support (.NET 8) **COMPLETED**
- [ ] API v2 with GraphQL
- [ ] Machine learning anomaly detection
- [ ] Integration with ServiceNow/Jira

See [GitHub Projects](https://github.com/robgrame/Nimbus.BootCertWatcher/projects) for detailed roadmap.

---

## Acknowledgments

- **Microsoft** - Secure Boot specifications and UEFI guidance
- **Chart.js Community** - Excellent charting library
- **Bootstrap Team** - Responsive design framework
- **Serilog Contributors** - Robust logging infrastructure
- **Azure SDK Team** - Comprehensive cloud integration

---

<div align="center">

**Made with ❤️ for the IT Community**

[⬆ Back to Top](#secure-boot-certificate-watcher)

</div>
