# Unified Deployment Architecture

## Overview

As of version 1.14, **SecureBootWatcher** supports a **unified deployment model** where the Web Dashboard and API are combined into a single application. This simplifies deployment and management in environments where a separate API server is not needed.

## Architecture

### Traditional (Separate) Deployment
```
┌─────────────────┐     HTTPS      ┌──────────────────┐
│  Web Dashboard  │  ──────────►   │   API Server     │
│  Port 7001      │                │   Port 5001      │
└─────────────────┘                └──────────────────┘
         │                                  │
         │                                  │
         └──────────────┬───────────────────┘
                        │
                   ┌────▼────┐
                   │  SQL DB │
                   └─────────┘
```

### Unified Deployment (Default)
```
┌──────────────────────────────────────┐
│    Unified Application (Port 7001)   │
│                                      │
│  ┌──────────────┐  ┌──────────────┐│
│  │ Web Pages    │  │ API          ││
│  │ (Razor)      │  │ Controllers  ││
│  └──────┬───────┘  └──────┬───────┘│
│         │ localhost:7001  │        │
│         └────────┬─────────┘        │
│                  │                  │
│         ┌────────▼─────────┐       │
│         │  Shared Services │       │
│         │  - DbContext     │       │
│         │  - SignalR Hub   │       │
│         │  - Background    │       │
│         └──────────────────┘       │
└──────────────────┬───────────────────┘
                   │
              ┌────▼────┐
              │  SQL DB │
              └─────────┘
```

## Key Features

### Single Application
- **One Process**: Both Web UI and API run in the same ASP.NET Core application
- **Single Port**: Application listens on port 7001 (HTTPS) and 7000 (HTTP)
- **Shared Resources**: Single database connection pool, logging, and configuration

### Loopback HTTP Architecture
- **Razor Pages** use `HttpClient` to call API endpoints on `localhost:7001`
- **API Controllers** serve REST endpoints at `/api/*`
- **Swagger UI** available at `/swagger` (Development mode)
- **SignalR Hub** available at `/dashboardHub`
- **Health Check** available at `/health`

### Benefits
1. **Simplified Deployment**: One application to deploy instead of two
2. **Easier Configuration**: Single `appsettings.json` file
3. **Reduced Infrastructure**: One App Service/VM instead of two
4. **Lower Cost**: Reduced hosting costs in Azure or on-premises
5. **Simpler Security**: No need for API authentication between Web and API
6. **Unified Logging**: All logs in one place
7. **Better for Small-Medium Environments**: Ideal for 1-1000 devices

### Trade-offs
- **Horizontal Scaling**: Cannot scale Web and API independently
- **Resource Contention**: Web and API share CPU/memory
- **Load Pattern**: Best for environments with similar Web/API load

## Configuration

### appsettings.json

The unified application uses a merged configuration:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=SRVSQL;Database=SecureBootDashboard;..."
  },
  "ApiSettings": {
    "BaseUrl": "https://localhost:7001",
    "BypassSslValidation": true
  },
  "Authentication": {
    "Provider": "None"  // or "EntraId" or "Windows"
  },
  "QueueProcessor": {
    "Enabled": true,
    ...
  },
  "Performance": {
    "RateLimiting": { "Enabled": true },
    "OutputCaching": { "Enabled": true },
    "Compression": { "Enabled": true }
  }
}
```

### Key Configuration Points

1. **ApiSettings.BaseUrl**: Set to `https://localhost:7001` for unified mode
2. **Urls**: Application listens on `https://localhost:7001;http://localhost:7000`
3. **Authentication**: Applies to both Web pages and API endpoints
4. **Background Services**: QueueProcessor and DeviceCleanup run in the same process

## Deployment

### Azure App Service

1. **Create a Single App Service**:
   ```bash
   az webapp create --name myapp --resource-group mygroup --plan myplan
   ```

2. **Deploy the unified application**:
   ```bash
   dotnet publish SecureBootDashboard.Web/SecureBootDashboard.Web.csproj -c Release
   az webapp deployment source config-zip --name myapp --resource-group mygroup --src publish.zip
   ```

3. **Configure Application Settings**:
   - Set connection string: `SqlServer`
   - Set `ASPNETCORE_ENVIRONMENT`: `Production`
   - Optional: Set `ApiSettings__BaseUrl` to your app's URL

### IIS / Windows Server

1. **Install .NET 10 Hosting Bundle**
2. **Publish the application**:
   ```powershell
   dotnet publish SecureBootDashboard.Web/SecureBootDashboard.Web.csproj -c Release -o C:\inetpub\wwwroot\secureboot
   ```
3. **Create IIS Site** pointing to the published folder
4. **Configure Application Pool** (.NET CLR: No Managed Code)
5. **Update appsettings.json** with your connection string

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY publish/ .
EXPOSE 7001
EXPOSE 7000
ENTRYPOINT ["dotnet", "SecureBootDashboard.Web.dll"]
```

## Endpoints

### Web Pages (Razor Pages)
- `/` - Dashboard homepage
- `/Devices/List` - Device list
- `/Devices/Details/{id}` - Device details
- `/Reports/Details/{id}` - Report details
- `/Settings/*` - Configuration pages

### API Endpoints
- `GET /api/Devices` - List all devices
- `GET /api/Devices/{id}` - Get device details
- `GET /api/SecureBootReports` - List reports
- `GET /api/SecureBootReports/{id}` - Get report details
- `GET /api/WindowsVersion/*` - Windows version tracking
- `POST /api/SecureBootReports` - Submit new report (from clients)
- Full API documentation at `/swagger` (Development mode)

### System Endpoints
- `GET /health` - Health check
- `WS /dashboardHub` - SignalR real-time updates

## Performance Considerations

### Recommended Hardware
- **Small (1-100 devices)**: 2 vCPU, 4GB RAM
- **Medium (100-500 devices)**: 4 vCPU, 8GB RAM  
- **Large (500-1000 devices)**: 8 vCPU, 16GB RAM

### Scaling Options
If you need to scale beyond 1000 devices or have high traffic:
1. **Vertical Scaling**: Increase CPU/RAM of the unified app
2. **Separate Deployment**: Revert to separate Web and API deployments
3. **Load Balancing**: Use multiple instances with sticky sessions for Web, and load balance API

### Performance Features (Enabled by Default)
- **Output Caching**: Reduces database queries for repeated requests
- **Response Compression**: Reduces bandwidth (Brotli/Gzip)
- **Rate Limiting**: Prevents abuse (1000 requests/minute)
- **Connection Pooling**: Efficient database connections (pool size: 200)

## Monitoring

### Application Insights
Both Web and API telemetry is sent to a single Application Insights resource:
- Cloud Role: `SecureBootDashboard.Unified`
- Metrics: Requests, Dependencies, Exceptions, Performance Counters
- Logs: Structured logging via Serilog

### Health Checks
- **Endpoint**: `GET /health`
- **Checks**: Database connectivity, basic application health
- **Response**: JSON with status and details

### Logs
Logs are written to:
- **Console**: Standard output
- **File**: `logs/unified-.log` (rolling daily, 30 days retention)
- **Application Insights**: Structured telemetry

## Troubleshooting

### Application Won't Start
1. Check logs in `logs/unified-<date>.log`
2. Verify SQL Server connection string
3. Ensure port 7001 is not in use
4. Check Application Insights connection string (optional)

### API Calls Fail from Web Pages
1. Verify `ApiSettings.BaseUrl` is set to `https://localhost:7001` (or your app URL)
2. Check `ApiSettings.BypassSslValidation` is `true` for development
3. Ensure API controllers are registered (check Swagger at `/swagger`)

### Background Services Not Running
1. Check `QueueProcessor.Enabled` is `true`
2. Verify Azure Queue connection string (if using queues)
3. Check logs for service startup messages
4. Ensure database is accessible

### Database Connection Errors
1. Verify connection string in appsettings.json
2. Check SQL Server is accessible from application
3. Ensure database exists (run migrations if needed):
   ```bash
   dotnet ef database update --project SecureBootDashboard.Web
   ```

## Migration from Separate Deployment

If you have an existing deployment with separate Web and API applications:

1. **Backup Database**: Always backup before migration
2. **Update Configuration**: 
   - Copy API's appsettings.json sections to Web's appsettings.json
   - Update `ApiSettings.BaseUrl` to unified app URL
3. **Deploy Unified App**: Deploy SecureBootDashboard.Web
4. **Test**: Verify all functionality works
5. **Decommission Old API**: Stop and remove the separate API application
6. **Update Client Configuration**: Point PowerShell clients to new unified API URL

## When to Use Separate Deployment

Consider separate Web and API deployments if:
- **High Device Count**: > 1000 devices with frequent reporting
- **Different Scale Needs**: API needs more resources than Web (or vice versa)
- **Geographic Distribution**: API needs to be closer to devices than Web
- **Security Requirements**: API requires different security controls than Web
- **Load Pattern**: Very different traffic patterns for Web vs API

## Support

For issues or questions:
- Check logs in `logs/` folder
- Review Application Insights telemetry
- Consult main [README.md](../README.md) for general documentation
- Check [TROUBLESHOOTING.md](../TROUBLESHOOTING_PORTS.md) for common issues
