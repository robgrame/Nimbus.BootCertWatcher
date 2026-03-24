# Secure Boot Certificate Watcher

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/tests-103%20passing-brightgreen)]()

> **Monitor and govern Secure Boot certificate lifecycle across Windows fleets with real-time analytics, remote command management, and compliance dashboards.**

**Version 1.15** — Security Hardening, CORS Restrictions & Test Fixes

---

## Overview

Secure Boot Certificate Watcher is an enterprise solution for monitoring Secure Boot certificate status on Windows devices. It captures registry snapshots and Windows event logs, transmits reports to a centralized API, and provides a real-time dashboard for compliance tracking, alerting, and fleet governance.

### Architecture

```
┌─────────────────────────────────────────────┐
│  Windows Devices (.NET Framework 4.8)       │
│  ┌───────────────────────────────────────┐  │
│  │ SecureBootWatcher.Client              │  │
│  │  • Registry polling                   │  │
│  │  • Event log capture                  │  │
│  │  • UEFI certificate enumeration       │  │
│  │  • Multi-sink delivery                │  │
│  └───────────────────────────────────────┘  │
└────────────────────┬────────────────────────┘
                     │  WebAPI / Azure Queue / FileShare
                     ▼
    ┌───────────────────────────────┐
    │  Azure Queue Storage          │
    │  (optional message buffer)    │
    └───────────────┬───────────────┘
                    ▼
┌─────────────────────────────────────────────┐
│  Dashboard API (ASP.NET Core 10)            │
│  ┌───────────────────────────────────────┐  │
│  │ SecureBootDashboard.Api               │  │
│  │  • Report ingestion & validation      │  │
│  │  • EF Core + SQL Server persistence   │  │
│  │  • Queue processor background service │  │
│  │  • SignalR real-time notifications     │  │
│  │  • Excel/CSV export                   │  │
│  │  • Remote command management          │  │
│  └───────────────────────────────────────┘  │
└────────────────────┬────────────────────────┘
                     ▼
┌─────────────────────────────────────────────┐
│  Dashboard Web (Razor Pages)                │
│  ┌───────────────────────────────────────┐  │
│  │ SecureBootDashboard.Web               │  │
│  │  • Interactive analytics (Chart.js)   │  │
│  │  • Real-time updates (SignalR)        │  │
│  │  • Device & certificate management    │  │
│  │  • Command management UI              │  │
│  │  • Windows version tracking           │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

---

## ✨ Key Features

### 🔐 Certificate Monitoring
- **UEFI Certificate Enumeration**: Scan db, dbx, KEK, PK databases from firmware
- **Expiration Tracking**: Color-coded certificate health (expired, expiring soon, valid)
- **Windows UEFI CA 2023**: Track deployment readiness for the new Microsoft certificate
- **Certificate Validation**: Multi-tier PKI chain validation with CA root verification

### 📊 Dashboard & Analytics
- **Real-time Updates**: SignalR-powered live dashboard with auto-reconnection
- **Interactive Charts**: Chart.js visualizations for compliance trends and deployment status
- **Export**: Excel/CSV export for devices, reports, and certificate data
- **Device Details**: Deep-dive into individual device compliance with full certificate tables

### 🖥️ Windows Version Tracking
- **Version Database**: Track Windows 10/11 versions, builds, and support lifecycle
- **Build Security**: Identify outdated and insecure builds with `IsSecure`/`IsLatest` flags
- **Outdated Devices**: Fleet-wide view of devices needing Windows updates
- **Integration**: Built-in WindowsVersionsCore for Microsoft version data sync

### 📡 Remote Command Management
- **Centralized Control**: Send configuration commands to individual or multiple devices
- **Batch Operations**: Fleet-wide command dispatch with filter-based device selection
- **Command Lifecycle**: Full tracking from Pending → Fetched → Processing → Completed/Failed
- **Scheduling & Priority**: Schedule commands for future execution with priority ordering

### 🔄 Ready-to-Update Assessment
- **Multi-criteria Readiness**: Firmware date + OS build validation for UEFI CA 2023
- **Visual Status**: Color-coded badges (Ready ✅ / Partial ⚠️ / Not Ready ❌ / Unknown ❓)
- **Fleet Overview**: Dashboard widget with readiness statistics

### 📈 Telemetry & CFR Tracking
- **Telemetry Levels**: Monitor Windows diagnostic data levels per device
- **CFR Eligibility**: Detect Controlled Feature Rollout readiness
- **Update Types**: Track requested DB and Boot Manager updates

### 🛡️ Enterprise Security
- **Mutual TLS**: Client certificate authentication support
- **Azure AD / Entra ID**: Dashboard authentication integration
- **Windows Authentication**: Domain-based access control
- **Multi-sink Resilience**: Failover delivery with retry policies (Polly)

### ⚡ Performance & Scalability
- **Rate Limiting**: Configurable request throttling
- **Output Caching**: Tunable response caching
- **Response Compression**: Brotli/Gzip compression
- **Database Optimization**: Connection pooling, query splitting, compiled queries

---

## Components

| Project | Framework | Description |
|---------|-----------|-------------|
| **SecureBootWatcher.Client** | .NET Framework 4.8 | Windows agent — registry polling, event log capture, certificate enumeration |
| **SecureBootWatcher.Shared** | .NET Standard 2.0 | Shared models, configuration, validation contracts |
| **SecureBootDashboard.Api** | ASP.NET Core 10 | REST API — report ingestion, SignalR hub, queue processor, export service |
| **SecureBootDashboard.Web** | ASP.NET Core 10 (Razor Pages) | Dashboard UI — charts, device management, command console |
| **WindowsVersionsCore** | ASP.NET Core 10 | Windows version/build tracking and security assessment |
| **SecureBootReportProxy.Functions** | Azure Functions (.NET 8) | Serverless queue-to-API bridge |
| **PowerShell Client** | PowerShell 5.0+ | Alternative lightweight client for Intune/SCCM deployment |

### Test Projects
- **SecureBootDashboard.Api.Tests** (xUnit, 85 tests) — API controller and service tests
- **SecureBootDashboard.Web.Tests** (xUnit, 8 tests) — Razor page model tests
- **SecureBootWatcher.Client.Tests** (MSTest, 3 tests) — Client service flow tests
- **SecureBootWatcher.Shared.Tests** (xUnit, 7 tests) — Shared model/config tests

---

## Prerequisites

### Development
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48) (for Client project)
- SQL Server (or SQL Server Express / LocalDB)
- Visual Studio 2022+ or VS Code with C# Dev Kit

### Runtime (Client)
- Windows 10/11 or Windows Server 2016+
- .NET Framework 4.8 Runtime (or PowerShell 5.0+ for PS client)
- Administrator privileges (for registry and certificate access)

### Runtime (API & Web Dashboard)
- Windows Server or Azure App Service
- .NET 10 Runtime
- SQL Server 2019+ (or Azure SQL Database)
- *(Optional)* Azure Queue Storage for buffered ingestion

---

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/robgrame/Nimbus.BootCertWatcher.git
cd Nimbus.BootCertWatcher
```

### 2. Build the Solution

```powershell
dotnet build SecureBootWatcher.sln
```

### 3. Run Tests

```powershell
dotnet test SecureBootWatcher.sln
```

### 4. Configure

Copy and customize the configuration files:

```powershell
# API - set your SQL Server connection string
# Edit SecureBootDashboard.Api/appsettings.json
# Set ConnectionStrings:SqlServer

# Web - set the API base URL
# Edit SecureBootDashboard.Web/appsettings.json
# Set ApiSettings:BaseUrl

# Client - set the API endpoint and sink strategy
# Edit SecureBootWatcher.Client/appsettings.json
# Set SecureBootWatcher:Sinks:WebApi:BaseAddress
```

> ⚠️ **Important**: Never commit secrets to source control. Use environment variables, Azure Key Vault, or User Secrets for production credentials. See [Security Configuration](#security-configuration) below.

### 5. Apply Database Migrations

```powershell
dotnet ef database update --project SecureBootDashboard.Api
```

### 6. Run the Dashboard

```powershell
# Quick start (API + Web in parallel)
.\start-dev.ps1

# Or manually:
cd SecureBootDashboard.Api && dotnet run    # https://localhost:5001
cd SecureBootDashboard.Web && dotnet run    # https://localhost:7001
```

### 7. Run the Client

```powershell
cd SecureBootWatcher.Client && dotnet run
```

---

## Security Configuration

All sensitive configuration values have been removed from the repository. You must configure them through environment variables, Azure Key Vault, or local User Secrets.

| Setting | Location | Description |
|---------|----------|-------------|
| `ConnectionStrings:SqlServer` | API appsettings | SQL Server connection string |
| `ApplicationInsights:ConnectionString` | API/Web appsettings | App Insights instrumentation |
| `QueueProcessor:TenantId` | API appsettings | Azure AD tenant for queue auth |
| `QueueProcessor:ClientId` | API appsettings | App registration client ID |
| `QueueProcessor:CertificateThumbprint` | API appsettings | Auth certificate thumbprint |
| `AzureAd:TenantId` / `AzureAd:ClientId` | Web appsettings | Entra ID authentication |

**Recommended approach for production:**

```powershell
# Use Azure Key Vault references in App Service
az webapp config appsettings set --name <app> --resource-group <rg> \
  --settings "ConnectionStrings__SqlServer=@Microsoft.KeyVault(SecretUri=https://...)"

# Or use environment variables
$env:ConnectionStrings__SqlServer = "Server=...;Database=...;..."
```

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/SecureBootReports` | Ingest client report payloads |
| `GET` | `/api/Devices` | List all devices with summaries |
| `GET` | `/api/Devices/{id}` | Device details |
| `GET` | `/api/Devices/{id}/reports` | Device report history |
| `GET` | `/api/Devices/export/excel` | Export devices to Excel |
| `GET` | `/api/Devices/export/csv` | Export devices to CSV |
| `GET` | `/api/ClientCommands` | Get pending commands for a device |
| `POST` | `/api/ClientCommands` | Create a new device command |
| `GET` | `/api/Settings` | Application settings |
| `GET` | `/api/WindowsVersions` | Windows version data |
| `WS` | `/dashboardHub` | SignalR real-time endpoint |
| `GET` | `/swagger` | OpenAPI / Swagger UI |

---

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Backend** | ASP.NET Core | 10.0 |
| **ORM** | Entity Framework Core | 10.0 |
| **Real-time** | SignalR | 1.2 |
| **Logging** | Serilog + Application Insights | 10.0 |
| **Resilience** | Polly | 8.6 |
| **Frontend** | Razor Pages + Bootstrap 5 | — |
| **Charts** | Chart.js | 4.4 |
| **Export** | ClosedXML + CsvHelper | 0.105 / 33.1 |
| **Client** | .NET Framework | 4.8 |
| **Queue** | Azure Storage Queues | 12.25 |
| **Auth** | Azure.Identity | 1.17 |
| **Testing** | xUnit + MSTest + Moq | Latest |
| **Versioning** | Nerdbank.GitVersioning | 3.9 |

---

## 📚 Documentation

Comprehensive documentation is available in the [`docs/`](docs/) folder:

- **Deployment Guides**: API Server, Web Dashboard, Client, Azure
- **Configuration**: Authentication, Certificates, Mutual TLS, Queue Processor
- **Features**: Command Management, Windows Versions, Export
- **Operations**: Troubleshooting, Diagnostics, Emergency Procedures
- **Release Notes**: Complete changelog from v1.3 through v1.15
- **Client Deployment**: Intune, SCCM, PowerShell packaging

Quick links:
- [Deployment Guide](docs/DEPLOYMENT_GUIDE.md)
- [Mutual TLS Quick Start](docs/MUTUAL_TLS_QUICKSTART.md)
- [Command Management User Guide](docs/COMMAND_MANAGEMENT_USER_GUIDE.md)
- [PowerShell Client](docs/POWERSHELL_CLIENT.md)
- [Azure Deployment](docs/AZURE_DEPLOYMENT_GUIDE.md)

---

## 🚀 Quick Commands

```powershell
# Build
dotnet build SecureBootWatcher.sln

# Test
dotnet test SecureBootWatcher.sln

# Run (development)
.\start-dev.ps1

# Database migration
dotnet ef database update --project SecureBootDashboard.Api

# Add new migration
dotnet ef migrations add <Name> --project SecureBootDashboard.Api

# Run client once
cd SecureBootWatcher.Client && dotnet run
```

---

## Changelog (v1.15)

### Security Hardening
- ✅ Removed all hardcoded secrets from configuration files (Azure TenantId, ClientId, CertificateThumbprint, InstrumentationKeys)
- ✅ Replaced internal server names with localhost defaults
- ✅ Disabled SSL validation bypass in default configuration (`BypassSslValidation: false`)
- ✅ Restricted CORS policy in WindowsVersionsCore (was `AllowAnyOrigin`, now configurable origins with GET-only methods)
- ✅ Removed hardcoded log file paths (was `R:\Nimbus.SecureBootCert\...`)

### Quality
- ✅ Fixed `Assert.IsGreaterThan` argument order bug in `RunAsync_WithRunModeContinuous_ExecutesMultipleTimes` test
- ✅ Increased test timeout for reliable continuous mode validation
- ✅ All 103 tests passing (85 API + 8 Web + 7 Shared + 3 Client)

### Repository Cleanup
- ✅ Moved 29 deployment/troubleshooting markdown files from root to `docs/`
- ✅ Clean root directory with only README.md and essential project files

### Previous Releases
See [Release Notes](docs/) for v1.3 through v1.14 changelog.

---

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Guidelines
- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use conventional commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`)
- Add tests for new functionality
- Update documentation for user-facing changes

---

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.

---

## Author

**Roberto Gramellini** — [GitHub](https://github.com/robgrame)
