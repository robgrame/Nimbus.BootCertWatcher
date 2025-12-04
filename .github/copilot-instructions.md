# GitHub Copilot Instructions - Secure Boot Certificate Watcher

## Project Overview

**Secure Boot Certificate Watcher** is an enterprise Windows monitoring solution for tracking Secure Boot certificates, Windows versions, and device compliance across Windows fleets. The solution consists of:

- **.NET Framework 4.8 Client** (SecureBootWatcher.Client) - Runs on Windows devices to collect data
- **.NET 10 Web API** (SecureBootDashboard.Api) - REST API for data ingestion and querying
- **.NET 10 Web Dashboard** (SecureBootDashboard.Web) - Razor Pages dashboard for visualization
- **Shared Libraries** (SecureBootWatcher.Shared, WindowsVersionsCore) - Common models and utilities
- **Test Projects** - Comprehensive test coverage for all components

**Current Version**: 1.13 (using Nerdbank.GitVersioning)

## Technology Stack

### Backend
- **.NET 10** (API & Web)
- **ASP.NET Core 10** (Web API, Razor Pages)
- **Entity Framework Core 10** (SQL Server)
- **SignalR 1.2.0** (Real-time updates)
- **Serilog 10.0** (Structured logging)
- **Azure SDK** (Storage Queues, Identity)
- **Polly 8.x** (Resilience & retry policies)

### Frontend
- **Razor Pages** (server-side rendering)
- **Bootstrap 5** (UI framework)
- **Chart.js 4.4** (Analytics charts)
- **SignalR Client 8.0** (WebSocket client)
- **jQuery 3.x** (DOM manipulation)

### Client
- **.NET Framework 4.8** (Windows compatibility)
- **PowerShell 5.0+** (Certificate enumeration)
- **Windows Registry API** (Registry polling)
- **WMI** (Device information)

### Infrastructure
- **Azure App Service** (Hosting)
- **Azure SQL Database** (Persistence)
- **Azure Queue Storage** (Message buffering)
- **Azure Monitor** (Telemetry)
- **WebSocket Support** (SignalR)

## Architecture Patterns

### Multi-Component Solution
- **Client-Server Architecture**: .NET Framework client → Azure Queue → .NET 10 API
- **Layered Architecture**: Presentation → API → Service → Data layers
- **Repository Pattern**: Data access abstraction with EF Core
- **Background Services**: Queue processor, scheduled tasks
- **Real-time Communication**: SignalR hubs for live dashboard updates

### Data Flow
1. Client collects Secure Boot data from Windows devices
2. Data sent to Azure Queue or directly to API
3. Background service processes queue messages
4. API stores data in SQL Server via EF Core
5. Web dashboard queries API and displays real-time updates via SignalR

### Key Design Principles
- **Nullable Reference Types**: Enabled in all .NET 10 projects
- **Implicit Usings**: Enabled for cleaner code
- **Dependency Injection**: Used throughout API and Web projects
- **Configuration**: appsettings.json with environment-specific overrides
- **Logging**: Serilog with structured logging to Application Insights and files

## Coding Standards

### General Guidelines
- Use C# 13 features (with .NET 10)
- Enable nullable reference types (`<Nullable>enable</Nullable>`)
- Enable implicit usings (`<ImplicitUsings>enable</ImplicitUsings>`)
- Follow Microsoft C# coding conventions
- Use async/await for I/O operations
- Implement proper exception handling with logging
- Use dependency injection for loose coupling

### Naming Conventions
- **Classes**: PascalCase (e.g., `DeviceEntity`, `SecureBootReport`)
- **Methods**: PascalCase (e.g., `GetDeviceAsync`, `ProcessReport`)
- **Properties**: PascalCase (e.g., `DeviceId`, `LastReportDate`)
- **Fields**: camelCase with underscore prefix for private fields (e.g., `_logger`, `_repository`)
- **Interfaces**: PascalCase with 'I' prefix (e.g., `IDeviceRepository`, `IReportService`)
- **Async Methods**: Suffix with 'Async' (e.g., `GetDevicesAsync`)

### Project Structure
- **Models**: DTOs and entity classes in `Models/` folder
- **Services**: Business logic in `Services/` folder
- **Controllers**: API endpoints in `Controllers/` folder (API project)
- **Pages**: Razor pages in `Pages/` folder (Web project)
- **Data**: EF Core context and repositories in `Data/` folder
- **Tests**: Mirror source project structure in test projects

### Testing
- Use xUnit for all tests
- Follow Arrange-Act-Assert pattern
- Use meaningful test names (e.g., `GetDevice_WhenDeviceExists_ReturnsDevice`)
- Mock external dependencies with Moq or NSubstitute
- Aim for high code coverage on business logic

### Documentation
- All public APIs should have XML documentation comments
- Keep README.md up to date with major changes
- Document architectural decisions in `docs/` folder
- Use clear commit messages following conventional commits format

## Build and Test Commands

### Build
```powershell
# Build entire solution
dotnet build SecureBootWatcher.sln

# Build specific project
dotnet build SecureBootDashboard.Api/SecureBootDashboard.Api.csproj

# Build in Release mode
dotnet build -c Release
```

### Test
```powershell
# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true

# Run specific test project
dotnet test SecureBootDashboard.Api.Tests/SecureBootDashboard.Api.Tests.csproj
```

### Run Development Environment
```powershell
# Start API and Web in parallel
.\start-dev.ps1

# Or manually in separate terminals:
cd SecureBootDashboard.Api && dotnet run
cd SecureBootDashboard.Web && dotnet run
```

**Default URLs**:
- API: `https://localhost:7120`
- API Swagger: `https://localhost:7120/swagger`
- Web: `https://localhost:7001`

### Database Migrations
```powershell
# Add new migration
dotnet ef migrations add MigrationName --project SecureBootDashboard.Api

# Update database
dotnet ef database update --project SecureBootDashboard.Api

# Generate SQL script
dotnet ef migrations script --project SecureBootDashboard.Api
```

## Common Development Tasks

### Adding a New API Endpoint
1. Create model in `SecureBootWatcher.Shared/Models/`
2. Add service method in `SecureBootDashboard.Api/Services/`
3. Create controller endpoint in `SecureBootDashboard.Api/Controllers/`
4. Add XML documentation to endpoint
5. Test endpoint via Swagger UI
6. Add unit tests in `SecureBootDashboard.Api.Tests/`

### Adding a New Dashboard Page
1. Create Razor page in `SecureBootDashboard.Web/Pages/`
2. Create page model class (code-behind)
3. Add navigation link in `_Layout.cshtml`
4. Style with Bootstrap 5 classes
5. Test responsiveness on different screen sizes

### Adding Database Table
1. Create entity class in `SecureBootWatcher.Shared/Models/`
2. Add DbSet to `SecureBootDashboard.Api/Data/ApplicationDbContext.cs`
3. Create migration: `dotnet ef migrations add AddTableName`
4. Review generated migration code
5. Apply migration: `dotnet ef database update`

### Adding SignalR Real-time Feature
1. Define hub method in `SecureBootDashboard.Api/Hubs/DashboardHub.cs`
2. Inject IHubContext in service that needs to broadcast
3. Call hub method: `await _hubContext.Clients.All.SendAsync("MethodName", data)`
4. Add client-side handler in JavaScript (e.g., `_Layout.cshtml`)
5. Test real-time updates in browser

## Security Best Practices

### Never Commit
- Connection strings with credentials (use User Secrets or Azure Key Vault)
- API keys or tokens
- Certificates (`.pfx`, `.p12`, `.cer`, `.crt`, `.key`) - already in `.gitignore`
- Personal access tokens

### Always
- Use parameterized queries (EF Core does this automatically)
- Validate all user inputs
- Use HTTPS for all communications
- Implement proper authentication and authorization
- Log security-relevant events
- Keep dependencies up to date

## Troubleshooting

### Build Fails
- Ensure .NET 10 SDK is installed: `dotnet --version`
- Clean and rebuild: `dotnet clean && dotnet build`
- Check for NuGet package issues: `dotnet restore`
- Review build errors in Output window

### Tests Fail
- Ensure database is available for integration tests
- Check test data setup in test fixtures
- Review test logs for specific errors
- Run tests individually to isolate issues

### Runtime Issues
- Check logs in `logs/` folder or Application Insights
- Verify database connection string in `appsettings.json`
- Ensure required services are running (SQL Server, Azure Queue, etc.)
- Check port availability: `netstat -ano | findstr :7120`

### API Not Accessible
- Verify API is running: check console output
- Check firewall settings for ports 7120 (API) and 7001 (Web)
- Review `launchSettings.json` for port configuration
- Test with Swagger UI at `https://localhost:7120/swagger`

## Documentation Resources

Comprehensive documentation is available in the `docs/` folder:
- **API Documentation**: Swagger UI at `/swagger` endpoint
- **Architecture Guides**: `docs/` folder
- **Deployment Guides**: `docs/DEPLOYMENT_GUIDE.md`
- **Troubleshooting**: `docs/TROUBLESHOOTING_PORTS.md`
- **Release Notes**: `docs/RELEASE_NOTES_*.md`

## Version Control

- Use semantic versioning via `version.json` (Nerdbank.GitVersioning)
- Current version: 1.13
- Follow conventional commits: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`
- Create feature branches for new work
- Write descriptive commit messages

## Development Workflow

1. **Create feature branch**: `git checkout -b feature/feature-name`
2. **Make changes**: Edit code, add tests
3. **Build and test**: `dotnet build && dotnet test`
4. **Commit changes**: `git commit -m "feat: add feature"`
5. **Push branch**: `git push origin feature/feature-name`
6. **Create pull request**: Use GitHub UI
7. **Code review**: Address feedback
8. **Merge**: Squash and merge to main

## Additional Notes

- **PowerShell Scripts**: Available in `scripts/` for common tasks
- **Client Deployment**: Use `Deploy-Client.ps1` for automated deployment
- **Database Scripts**: PowerShell scripts for migrations and verification
- **Intune Deployment**: Scripts available for Intune Win32 app deployment
