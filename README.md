# Secure Boot Certificate Watcher

> **Monitor and govern the expiration and deployment of Secure Boot certificates across Windows fleets with real-time analytics.**

**Version 1.11** - Windows Version Tracking & Build Security!

This solution monitors Secure Boot certificate status on Windows devices by capturing registry snapshots and Windows event logs, then transmitting reports to a centralized dashboard for compliance tracking, real-time monitoring, and alerting.

---

## 📸 Screenshots

### Dashboard Homepage
Modern analytics dashboard with interactive charts, real-time compliance monitoring, and SignalR updates.

### Device Management
Comprehensive device list with advanced filtering, search capabilities, and export to Excel/CSV.

### Analytics Charts
Interactive Chart.js visualizations showing compliance trends and deployment status.

---

## ✨ Key Features

### 🔴 **NEW in v1.11** - Windows Version Tracking & Build Security
- **Windows Version Database**: Track Windows 10/11 versions and builds
  - Integration with [WindowsVersionsCore](https://github.com/robgrame/WindowsVersionsCore)
  - Automatic sync of version data from Microsoft's official sources
  - Support lifecycle tracking (release date, end of support)
- **Build Security Verification**: Identify outdated and insecure builds
  - `IsSecure` flag for each build in database
  - `IsLatest` indicator for current builds
  - Security notes and recommendations
  - KB article tracking
- **REST API Endpoints**: Complete API for version management
  - Sync version data from WindowsVersionsCore
  - Check build security status
  - Get version and build information
  - Fleet-wide statistics
  - List devices with outdated builds
- **Dashboard Pages**: Three new pages for version tracking
  - **Windows Versions** - Overview with sync capability
  - **Outdated Devices** - Devices needing Windows updates
  - **Build Details** - Build history and timeline
- **Dashboard Widget**: Windows Build Security statistics
  - Total tracked devices
  - Secure builds percentage
  - Outdated and unknown builds count
  - Quick navigation to detail pages
- **Testing & Documentation**: Comprehensive testing suite
  - PowerShell API test script
  - Database verification script
  - Complete API documentation

### 🔴 **NEW in v1.10** - Ready to Update Status Indicator
- **Visual Readiness Tracking**: Instant identification of devices ready for UEFI CA 2023 update
  - Color-coded status badges (Ready/Partial/Not Ready/Unknown)
  - Firmware readiness indicator (release date >= 2024-01-01)
  - OS build compatibility checker (Windows 10 19045+, Windows 11 22621+, 26100+)
- **Enhanced Device List**: New "Ready to Update" column with smart indicators
  - ✅ Green: Both firmware and OS ready for update
  - ⚠️ Yellow: Partial readiness (firmware OR OS ready)
  - ❌ Red: Not ready (neither firmware nor OS ready)
  - ❓ Gray: Unknown status (missing data)
- **Comprehensive Logic**: Multi-criteria readiness assessment
  - Firmware release date validation
  - OS build number parsing and validation
  - Automatic status calculation per device
- **Complete Test Coverage**: 26 test cases covering all scenarios
  - Firmware boundary testing (2024-01-01 cutoff)
  - Windows 10/11 build validation
  - Null and invalid data handling

### 🔴 **NEW in v1.9** - Complete Command Management Suite
- **Web UI for Command Management**: Full-featured dashboard for sending and managing commands
- **Command Sending Interface**: Intuitive form to send configuration commands to devices
  - Certificate Update commands with force update option
  - Microsoft Update Opt-In/Out configuration
  - Telemetry Level validation and configuration
- **Command History & Tracking**: Complete audit trail of all commands
  - Real-time status updates (Pending, Fetched, Processing, Completed, Failed)
  - Command statistics dashboard with filterable views
  - Cancel pending commands before execution
- **Advanced Scheduling**: Schedule commands for future execution
  - Priority-based command execution
  - Configurable execution timing
  - Retry tracking with fetch count monitoring
- **Batch Operations**: Send commands to multiple devices simultaneously
  - Manual device selection with select-all checkbox
  - Filter-based selection (Fleet, Manufacturer, Deployment State)
  - Select all devices option for fleet-wide operations
  - Real-time result tracking with success/failure reporting
- **Command Details Page**: Comprehensive command information viewer
  - Full command lifecycle tracking with timeline visualization
  - Execution results with verification status
  - Device information integration
  - JSON parameter display with syntax highlighting
  - Cancel pending commands capability
- **Dashboard Integration**: Command statistics on homepage
  - Total Commands counter with drill-down
  - Pending Commands alert card
  - Completed/Failed command tracking
  - Quick access to Command History with filters
- **Enhanced User Experience**: Streamlined command management
  - Consistent UI across all command pages
  - Real-time status updates via SignalR
  - Color-coded status badges and icons
  - Responsive design for all screen sizes

### 🔴 **NEW in v1.7** - Remote Command Processing
The dashboard now includes powerful remote command processing capabilities:

**Centralized Command Control**:
- Send configuration commands and scripts to one or more devices
- Define reusable command templates with variables for flexibility
- Secure execution with SYSTEM privileges, using encrypted channels

**Command Management**:
- View command execution status in real-time
- Retry or cancel pending commands
- Download command output and logs

**Dashboard Integration**:
- New command status cards and charts
- Additional columns for command status in device list
- Detailed command execution history in device details

**Improved User Experience**:
- Enhanced device details page with command processing features
- Italian language support for all labels
- Responsive design for all screen sizes

### 🔴 **NEW in v1.6** - Telemetry & CFR Tracking
- **Telemetry Policy Monitoring**: Track Windows diagnostic data levels (Security/Basic/Enhanced/Full)
- **CFR Eligibility Detection**: Automatic detection of Controlled Feature Rollout eligibility
  - Microsoft Update Managed Opt-In status
  - Telemetry level validation (requires Basic or higher)
  - Windows UEFI CA 2023 capability tracking
- **UpdateType Tracking**: Monitor requested update types (DB, Boot Manager)
- **Dashboard Integration**: New statistics cards and charts for CFR/telemetry metrics
- **Device List Enhancements**: Additional columns for telemetry and CFR status
- **Detailed Reporting**: Complete CFR eligibility analysis in device details

### 🔴 **NEW in v1.5** - Enhanced Certificate Visualization
- **Detailed Certificate Tables**: Comprehensive certificate information display
  - Subject, Issuer, Validity Periods, Thumbprint
  - Certificate Version, Signature Algorithm, Key Size
  - Microsoft Certificate Badge Indicator
- **Certificate Statistics**: Quick overview of certificate health
  - Total certificate count per database (db, dbx, KEK, PK)
  - Expired certificates count
  - Expiring soon certificates (within 90 days)
- **Collapsible Sections**: Organized display by UEFI database
  - Expandable/collapsible panels for each database
  - Easy navigation through large certificate sets
- **Color-Coded Status**: Visual indicators for certificate health
  - Red rows: Expired certificates
  - Yellow rows: Certificates expiring within 90 days
  - Green/normal: Valid certificates
- **Days Until Expiration**: Clear visibility of remaining certificate validity
- **Italian Localization**: Full Italian language support for labels and messages

### 🔴 **v1.3** - Real-time Updates (SignalR)
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
- **Mutual TLS (mTLS)**: X.509 certificate authentication for API communications *(NEW)*
  - Client → API certificate validation
  - Web → API certificate validation
  - Certificate thumbprint and issuer allowlisting
  - Configurable certificate revocation checking
- **RBAC Support**: Fine-grained Azure role assignments
- **Network Isolation**: VNet integration and private endpoint support
- **Audit Logging**: Comprehensive Serilog logging with structured data

### ⚡ High Performance & Scalability *(NEW)*
- **Rate Limiting**: Intelligent throttling (1000 req/min per client, 500 concurrent)
- **Output Caching**: Redis-backed distributed caching for read-heavy endpoints
- **Response Compression**: Brotli/Gzip compression for bandwidth optimization
- **Connection Pooling**: Optimized database connection pooling (200 max connections)
- **5000+ RPS Capable**: Enterprise-grade architecture supporting high request rates
- **Auto-Scaling**: Horizontal scaling support with Azure App Service or Kubernetes
- **Health Checks**: Comprehensive health monitoring endpoints

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

### 0. **WindowsVersionsCore** (ASP.NET Core 10)
- **Integrated internal project** (migrated from external repository on 2025-11-24)
- Originally from: https://github.com/robgrame/WindowsVersionsCore
- Provides Windows 10/11 version and build tracking data
- Used by:
  - `SecureBootDashboard.Api` - Windows version services
  - `SecureBootDashboard.WindowsVersionApi` - Standalone Windows version API
- Features:
  - Windows 10/11 version database
  - Build number tracking
  - Support lifecycle information
  - KB article references

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
- **NEW**: PowerShell alternative available - see **SecureBootWatcher PowerShell Client** below

### 1b. **SecureBootWatcher PowerShell Client** (PowerShell 5.0+)
- **Pure PowerShell implementation** for easier device management deployment
- Same inventory features as .NET Framework client (no client auto-update)
- Ideal for Intune, SCCM, or other MDM solutions
- Supports two reporting sinks:
  - **File Share**: writes JSON payloads to a network share
  - **Web API**: HTTP POST directly to the dashboard ingestion endpoint
- Simple script-based deployment without compiled binaries
- Configured via `appsettings.json` (same format as .NET client)
- Complete Intune Win32 App deployment scripts included
- See [PowerShell Client Documentation](docs/POWERSHELL_CLIENT.md) for details

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
  - **NEW in v1.5**: Enhanced certificate details with color-coded status tables
  - **NEW in v1.5**: Collapsible certificate sections by database (db, dbx, KEK, PK)
  - **NEW in v1.5**: Certificate statistics dashboard (total, expired, expiring)
  - Real-time SignalR client with auto-reconnection
  - Export buttons for Excel/CSV downloads
  - Splash screen with smooth animations
  - Interactive Chart.js analytics (compliance, deployment states, trends)
  - Device list page with advanced filtering and search
  - Device details with comprehensive certificate visualization
  - Report history with drill-down capabilities
  - About page with architecture and technology stack
  - Authentication support (Entra ID / Windows Domain)
  - Italian localization support
- Consumes API endpoints with resilience policies (Polly)

---

## 🆕 What's New in v1.11

### Windows Version Tracking & Build Security
The dashboard now includes comprehensive Windows version tracking and build security verification:

**Database Schema**:
- New `WindowsVersions` table for tracking Windows 10/11 versions
- New `WindowsBuilds` table with security status flags (`IsSecure`, `IsLatest`)
- Automatic synchronization from WindowsVersionsCore

**Service Layer**:
- `WindowsVersionService` for build security verification
- Integration with [WindowsVersionsCore](https://github.com/robgrame/WindowsVersionsCore)
- Automatic marking of secure vs outdated builds

**REST API (7 Endpoints)**:
- `POST /api/WindowsVersion/sync` - Synchronize version data
- `GET /api/WindowsVersion/check-build/{buildNumber}` - Verify build security
- `GET /api/WindowsVersion/versions` - List all Windows versions
- `GET /api/WindowsVersion/versions/{version}/builds` - Get builds for version
- `GET /api/WindowsVersion/versions/{version}/latest-secure` - Get latest secure build
- `GET /api/WindowsVersion/statistics` - Fleet-wide build statistics
- `GET /api/WindowsVersion/devices/outdated` - List devices with outdated builds

**Dashboard Pages (3 New Pages)**:
- **Windows Versions** (`/Windows/Versions`)
  - Overview of all tracked Windows versions
  - Sync button for manual data refresh
  - Build distribution chart
  - Statistics cards (tracked, secure, outdated, unknown)
- **Outdated Devices** (`/Windows/OutdatedDevices`)
  - List of devices requiring Windows updates
  - Export to CSV/Excel functionality
  - Critical vs outdated classification
  - Direct link to device details
- **Build Details** (`/Windows/Builds/{version}`)
  - Complete build history for a version
  - Timeline chart of releases
  - KB article links
  - Security status for each build

**Dashboard Widget**:
- Windows Build Security statistics on main dashboard
- 4 interactive cards:
  - Tracked Devices
  - Secure Builds (with percentage)
  - Outdated Builds
  - Unknown Builds
- Click-through navigation to detail pages

**Testing & Documentation**:
- PowerShell test script (`Test-WindowsVersionApi.ps1`)
- Database verification script (`Check-WindowsVersionTables.ps1`)
- Comprehensive API documentation (`WINDOWS_VERSION_API.md`)
- Complete release notes (`RELEASE_NOTES_V1.11.0.md`)

**Use Cases**:
- **Compliance Monitoring**: Track which devices have secure Windows builds
- **Patch Management**: Identify devices needing Windows updates
- **Security Auditing**: Generate reports of outdated builds
- **Version Planning**: Plan upgrades based on support lifecycle

---

## 🆕 What's New in v1.10

### Ready to Update Status Indicator
The dashboard now includes a powerful status indicator for UEFI CA 2023 readiness:

**Visual Readiness Tracking**:
- Instant identification of devices ready for update
- Color-coded status badges:
  - **Green**: Ready for update
  - **Yellow**: Partial readiness
  - **Red**: Not ready
  - **Gray**: Unknown status
- Firmware readiness indicator (release date >= 2024-01-01)
- OS build compatibility checker (Windows 10 19045+, Windows 11 22621+, 26100+)

**Enhanced Device List**:
- New "Ready to Update" column with smart indicators
  - ✅ Green: Both firmware and OS ready for update
  - ⚠️ Yellow: Partial readiness (firmware OR OS ready)
  - ❌ Red: Not ready (neither firmware nor OS ready)
  - ❓ Gray: Unknown status (missing data)

**Comprehensive Logic**:
- Multi-criteria readiness assessment
- Firmware release date validation
- OS build number parsing and validation
- Automatic status calculation per device

**Complete Test Coverage**:
- 26 test cases covering all scenarios
- Firmware boundary testing (2024-01-01 cutoff)
- Windows 10/11 build validation
- Null and invalid data handling

---

## 🆕 What's New in v1.9

### Complete Command Management Suite
The dashboard now includes a complete command management UI:

**Web UI for Command Management**:
- Full-featured dashboard for sending and managing commands
- Intuitive form to send configuration commands to devices
  - Certificate Update commands with force update option
  - Microsoft Update Opt-In/Out configuration
  - Telemetry Level validation and configuration

**Command History & Tracking**:
- Complete audit trail of all commands
  - Real-time status updates (Pending, Fetched, Processing, Completed, Failed)
  - Command statistics dashboard with filterable views
  - Cancel pending commands before execution

**Advanced Scheduling**:
- Schedule commands for future execution
  - Priority-based command execution
  - Configurable execution timing
  - Retry tracking with fetch count monitoring

**Batch Operations**:
- Send commands to multiple devices simultaneously
  - Manual device selection with select-all checkbox
  - Filter-based selection (Fleet, Manufacturer, Deployment State)
  - Select all devices option for fleet-wide operations
  - Real-time result tracking with success/failure reporting

**Command Details Page**:
- Comprehensive command information viewer
  - Full command lifecycle tracking with timeline visualization
  - Execution results with verification status
  - Device information integration
  - JSON parameter display with syntax highlighting
  - Cancel pending commands capability

**Dashboard Integration**:
- Command statistics on homepage
  - Total Commands counter with drill-down
  - Pending Commands alert card
  - Completed/Failed command tracking
  - Quick access to Command History with filters

**Enhanced User Experience**:
- Streamlined command management
  - Consistent UI across all command pages
  - Real-time status updates via SignalR
  - Color-coded status badges and icons
  - Responsive design for all screen sizes

---

## 🆕 What's New in v1.7

### Remote Command Processing
The dashboard now includes powerful remote command processing capabilities:

**Centralized Command Control**:
- Send configuration commands and scripts to one or more devices
- Define reusable command templates with variables for flexibility
- Secure execution with SYSTEM privileges, using encrypted channels

**Command Management**:
- View command execution status in real-time
- Retry or cancel pending commands
- Download command output and logs

**Dashboard Integration**:
- New command status cards and charts
- Additional columns for command status in device list
- Detailed command execution history in device details

**Improved User Experience**:
- Enhanced device details page with command processing features
- Italian language support for all labels
- Responsive design for all screen sizes

---

## 🆕 What's New in v1.6

### Telemetry & CFR Tracking
The device details page now features new telemetry and CFR (Controlled Feature Rollout) tracking capabilities:

**Telemetry Policy Monitoring**:
- View current Windows diagnostic data level:
  - Security
  - Basic
  - Enhanced
  - Full
- Automatic detection of Controlled Feature Rollout eligibility:
  - Microsoft Update Managed Opt-In status
  - Telemetry level validation (requires Basic or higher)
  - Windows UEFI CA 2023 capability tracking
- Monitor requested update types (DB, Boot Manager)

**Dashboard Integration**:
- New statistics cards and charts for CFR/telemetry metrics
- Additional columns for telemetry and CFR status in device list
- Complete CFR eligibility analysis in device details

**Improved User Experience**:
- Enhanced device details page with telemetry and CFR tracking
- Italian language support for all labels
- Responsive design for all screen sizes

---

## 🆕 What's New in v1.5

### Enhanced Certificate Visualization
The device details page now features a comprehensive certificate display system:

**Certificate Overview Statistics**:
```
📊 Total Certificates: XX
⚠️ Expired: X
⏰ Expiring Soon (90 days): X
```

**Detailed Certificate Tables**:
- Full certificate information for each UEFI database (db, dbx, KEK, PK)
- Collapsible sections for better organization
- Color-coded status indicators:
  - 🔴 Red: Expired certificates
  - 🟡 Yellow: Certificates expiring within 90 days
  - ✅ Normal: Valid certificates

**Certificate Details Displayed**:
- Subject and Issuer information
- Validity period (Not Before / Not After)
- Days remaining until expiration
- Thumbprint (SHA-1)
- Certificate version
- Signature algorithm
- Public key algorithm and size
- Microsoft certificate badge 🏢

**Improved User Experience**:
- Expandable/collapsable panels per database
- Quick identification of problematic certificates
- Italian language support for all labels
- Responsive design for all screen sizes

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
- **[Windows Version Tracking API](docs/WINDOWS_VERSION_API.md)** - Complete API reference *(NEW v1.11)*
- **[Windows Version Release Notes](docs/RELEASE_NOTES_V1.11.0.md)** - v1.11 feature details *(NEW)*
- **[API Performance Guide](docs/API_PERFORMANCE_GUIDE.md)** - High-performance configuration *(NEW)*
- **[Production Deployment Performance](docs/PRODUCTION_DEPLOYMENT_PERFORMANCE.md)** - Enterprise deployment *(NEW)*
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
- **[Mutual TLS Configuration](docs/MUTUAL_TLS_CONFIGURATION.md)** - X.509 certificate authentication *(NEW)*
- **[Mutual TLS Implementation Summary](docs/MUTUAL_TLS_IMPLEMENTATION_SUMMARY.md)** - mTLS implementation details *(NEW)*

### Operations & Troubleshooting
- **[Logging Guide](docs/LOGGING_GUIDE.md)** - Serilog configuration and best practices
- **[Queue Processor Monitoring](docs/QUEUE_PROCESSOR_MONITORING.md)** - Background service health
- **[Retry & Resilience](docs/RETRY_RESILIENCE_GUIDE.md)** - Polly policies explained
- **[Troubleshooting Ports](docs/TROUBLESHOOTING_PORTS.md)** - Resolve port conflicts
- **[Host Aborted Issues](docs/HOSTABORTED_TROUBLESHOOTING.md)** - Fix cancellation errors

### Client Deployment
- **[PowerShell Client Guide](docs/POWERSHELL_CLIENT.md)** - PowerShell client deployment and configuration *(NEW)*
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

### ✅ v1.11 - Current Release
- [x] **Windows Version Tracking** - Complete
  - Database schema for versions and builds
  - Integration with WindowsVersionsCore
  - 7 REST API endpoints
  - 3 dashboard pages
  - Dashboard widget
  - PowerShell testing scripts
- [x] **Ready to Update Status Indicator** - Complete (v1.10)
  - Visual readiness tracking in Device List
  - Firmware release date validation (>= 2024-01-01)
  - OS build compatibility checking
  - Color-coded status badges
  - Comprehensive test suite (26 tests)
- [x] **Batch Command Operations** - Complete (v1.9)
  - Multi-device selection UI with 5 selection modes
  - Fleet-wide command deployment
- [x] **Command Details Page** - Complete (v1.9)
  - Comprehensive command information display
  - Execution timeline visualization
- [x] **Dashboard Integration** - Complete (v1.9)
  - Command statistics cards on homepage
- [x] **Complete Command Management UI** - Complete (v1.8)
- [x] **Remote Command Processing** - Complete (v1.7)
- [x] **Telemetry & CFR tracking** - Complete (v1.6)
- [x] **Enhanced certificate visualization** - Complete (v1.5)
- [x] **Real-time dashboard updates (SignalR)** - Complete (v1.3)
- [x] **Export reports to Excel/CSV** - Complete (v1.3)

### v1.12 - Q2 2025 (Planned)
- [ ] **Automatic Version Sync** - Background service
  - Weekly automatic sync scheduler
  - Configurable sync schedule
  - Email notifications on completion/failure
- [ ] **Build Compliance Reports**
  - Advanced reporting (Excel/CSV/PDF)
  - Trend analysis over time
  - Alert system for critical builds
- [ ] **Client Integration**
  - Client auto-detection of outdated builds
  - Windows Update trigger recommendations
  - WSUS/Windows Update for Business integration

### v2.0 - Q2 2025
- [ ] Dark mode theme support
- [ ] Custom alert thresholds per fleet
- [ ] Command templates library
- [ ] Advanced command analytics and reporting
- [ ] Email notifications for command failures
- [ ] Command approval workflow
- [ ] Role-based access control (RBAC)
- [ ] Automated remediation workflows

### v2.5 - Q3 2025

---

<div style="text-align: center; font-size: 0.9em; color: gray;">

**Made with ❤️ for the IT Community**

**Version 1.11** - Windows Version Tracking & Build Security

[⬆ Back to Top](#secure-boot-certificate-watcher)

</div>

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

### Command execution issues
1. Verify commands are enabled in client configuration
2. Check client has Administrator privileges
3. Review command execution logs in client output
4. Verify command status in dashboard Command History
5. Check registry write permissions
6. Ensure client is polling for commands regularly

### Batch command failures
1. Review failed device list in batch result
2. Check individual device connectivity
3. Verify all target devices are registered
4. Review command parameters for errors
5. Check API logs for batch operation errors
6. Retry failed devices individually

For detailed troubleshooting steps, see:
- [Troubleshooting Guide](docs/TROUBLESHOOTING_PORTS.md)
- [Host Aborted Guide](docs/HOSTABORTED_TROUBLESHOOTING.md)
- [SignalR Implementation Guide](docs/SIGNALR_REALTIME_COMPLETE.md)
- [Command Processing Troubleshooting](docs/COMMAND_PROCESSING_TROUBLESHOOTING.md)

---

## Contributing
