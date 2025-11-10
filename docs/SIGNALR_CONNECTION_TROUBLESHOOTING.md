# SignalR Connection Troubleshooting Guide

## Problem Description

The web application is unable to establish SignalR connection with the API hub for real-time updates.

## Current Configuration

### API Configuration (`SecureBootDashboard.Api`)

**appsettings.json:**
```json
{
  "Urls": "https://localhost:5000;http://localhost:5001",
  "WebAppUrl": "https://localhost:7001",
  "AlternativeWebUrls": [
    "https://srvcm00.msintune.lab",
    "https://srvcm00.msintune.lab:443",
    "http://srvcm00.msintune.lab",
    "http://srvcm00.msintune.lab:80"
  ]
}
```

**Actual API Listening Ports:**
- HTTPS: `5001` ?
- HTTP: `5000` ?

**SignalR Hub URL:**
- `https://localhost:5001/dashboardHub` ?

**CORS Configuration:**
- Allowed Origins: `https://localhost:7001`, `https://srvcm00.msintune.lab`, etc.
- Allows Credentials: `true` ? (required for SignalR)
- Allows Any Method: `true` ?
- Allows Any Header: `true` ?

### Web Configuration (`SecureBootDashboard.Web`)

**appsettings.json:**
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

**SignalR Client Configuration:**
```javascript
const apiBaseUrl = document.querySelector('meta[name="api-base-url"]')?.content || window.location.origin;
const hubUrl = `${apiBaseUrl}/dashboardHub`;
```

**Expected Hub URL:**
- `https://localhost:5001/dashboardHub` ?

## Potential Issues

### 1. CORS Configuration Mismatch ??

**Problem:** Web app runs on `https://localhost:7001` but might also need to accept connections from:
- `https://srvcm00.msintune.lab:7001`
- Other alternative URLs

**Current CORS Setup:**
```csharp
var webAppUrl = builder.Configuration.GetValue<string>("WebAppUrl") ?? "https://localhost:7001";
var alternativeUrls = builder.Configuration.GetSection("AlternativeWebUrls").Get<string[]>() ?? Array.Empty<string>();
```

**Issue:** Alternative URLs in `appsettings.json` are for the API server itself (ports 80/443), not for the web app (port 7001).

### 2. Meta Tag Not Set in All Pages ??

**Problem:** `ViewData["ApiBaseUrl"]` is only set in `Index.cshtml.cs`:

```csharp
// Set API base URL for SignalR connection
ViewData["ApiBaseUrl"] = _apiSettings.BaseUrl;
```

**Impact:** Other pages may not have the correct API URL, causing SignalR to connect to the wrong endpoint.

### 3. SignalR Negotiation Endpoint Not Accessible ??

**Problem:** SignalR requires several endpoints to be accessible:
- `/dashboardHub/negotiate` (initial handshake)
- `/dashboardHub` (WebSocket connection)

**CORS Preflight:** Browser sends OPTIONS request before connecting, which must be allowed.

### 4. SSL Certificate Issues (Development) ??

**Problem:** Self-signed certificates in development may cause connection failures.

**Browser Console Errors:**
- `NET::ERR_CERT_AUTHORITY_INVALID`
- `Mixed Content` warnings

## Diagnostic Steps

### Step 1: Check Browser Console

Open browser developer tools (F12) and check for errors:

**Expected Logs (Success):**
```
[SignalR] Initializing connection to: https://localhost:5001/dashboardHub
[SignalR] Connected successfully with ID: abc123...
[SignalR] Subscribed to dashboard updates
```

**Common Errors:**
```
? CORS policy: No 'Access-Control-Allow-Origin' header
? Failed to fetch
? WebSocket connection failed
? 404 Not Found: /dashboardHub/negotiate
```

### Step 2: Test API Endpoint Directly

**PowerShell:**
```powershell
# Test API health
Invoke-RestMethod -Uri "https://localhost:5001/health"

# Test SignalR negotiate endpoint (should return JSON)
Invoke-WebRequest -Uri "https://localhost:5001/dashboardHub/negotiate" -Method POST
```

**Expected Response:**
```json
{
  "connectionId": "...",
  "availableTransports": [...]
}
```

### Step 3: Test CORS Configuration

**Browser Console:**
```javascript
fetch('https://localhost:5001/health', {
  method: 'GET',
  headers: {
    'Origin': 'https://localhost:7001'
  }
})
.then(response => console.log('CORS OK:', response))
.catch(error => console.error('CORS Error:', error));
```

**Expected:** Status 200 with CORS headers in response.

### Step 4: Check SignalR Hub Registration

**API Logs (should contain):**
```
[INF] SignalR DashboardHub mapped at: /dashboardHub
[INF] CORS configured for origins: https://localhost:7001, https://srvcm00.msintune.lab, ...
```

### Step 5: Verify SSL Certificates

**Chrome:** Navigate to `https://localhost:5001` and check if certificate is trusted.

**PowerShell (trust self-signed cert):**
```powershell
# Add localhost certificate to trusted root
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -like "*localhost*"
if ($cert) {
    Export-Certificate -Cert $cert -FilePath "C:\temp\localhost.cer"
    Import-Certificate -FilePath "C:\temp\localhost.cer" -CertStoreLocation Cert:\LocalMachine\Root
}
```

## Solutions

### Solution 1: Add Web App Alternative URLs to CORS

**Problem:** Web app might be accessed via alternative hostnames.

**Fix:** Update `appsettings.json`:

```json
{
  "WebAppUrl": "https://localhost:7001",
  "AlternativeWebUrls": [
    "https://srvcm00.msintune.lab",
    "https://srvcm00.msintune.lab:7001",
    "http://srvcm00.msintune.lab:7001",
    "http://localhost:7001"
  ]
}
```

**Or:** Use wildcard CORS (development only):
```csharp
policy.SetIsOriginAllowed(origin => true)
      .AllowAnyMethod()
      .AllowAnyHeader()
      .AllowCredentials();
```

### Solution 2: Set ApiBaseUrl in _Layout.cshtml

**Problem:** `ViewData["ApiBaseUrl"]` not available in all pages.

**Fix 1 - Base Page Model:** Create base page model:

```csharp
// Pages/BasePageModel.cs
public abstract class BasePageModel : PageModel
{
    protected readonly ApiSettings _apiSettings;

    protected BasePageModel(IOptions<ApiSettings> apiSettings)
    {
        _apiSettings = apiSettings.Value;
    }

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        ViewData["ApiBaseUrl"] = _apiSettings.BaseUrl;
        base.OnPageHandlerExecuting(context);
    }
}
```

**Fix 2 - _Layout.cshtml:** Inject directly:

```razor
@inject IOptions<ApiSettings> ApiSettings
<meta name="api-base-url" content="@ApiSettings.Value.BaseUrl" />
```

### Solution 3: Enable Detailed SignalR Errors (Development)

**API Program.cs:**
```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Show detailed errors
});
```

### Solution 4: Add SignalR Logging in Client

**dashboard-realtime.js:**
```javascript
.configureLogging(signalR.LogLevel.Debug) // Change from Information to Debug
```

### Solution 5: Fix SSL Certificate Trust

**Development (Windows):**
```powershell
# Trust ASP.NET Core development certificates
dotnet dev-certs https --trust
```

**Browser:** Navigate to `https://localhost:5001` and manually accept certificate.

## Quick Fix Checklist

For immediate troubleshooting, apply these fixes:

- [ ] **Set ApiBaseUrl in _Layout.cshtml** (inject `IOptions<ApiSettings>`)
- [ ] **Enable detailed SignalR errors** in API
- [ ] **Change SignalR logging to Debug** in client
- [ ] **Test CORS** with browser console
- [ ] **Trust development certificates** (`dotnet dev-certs https --trust`)
- [ ] **Check API logs** for SignalR hub mapping confirmation
- [ ] **Check browser console** for connection errors
- [ ] **Test negotiate endpoint** manually

## Testing After Fix

### 1. Start API
```powershell
cd SecureBootDashboard.Api
dotnet run
```

**Expected log:**
```
[INF] SignalR DashboardHub mapped at: /dashboardHub
[INF] CORS configured for origins: https://localhost:7001
[INF] Now listening on: https://localhost:5001
```

### 2. Start Web App
```powershell
cd SecureBootDashboard.Web
dotnet run
```

**Expected log:**
```
[INF] Now listening on: https://localhost:7001
```

### 3. Open Browser

Navigate to: `https://localhost:7001`

**Browser Console (F12) - Expected:**
```
[SignalR] Initializing connection to: https://localhost:5001/dashboardHub
[SignalR] Connected successfully with ID: xyz123
[SignalR] Subscribed to dashboard updates
```

**Navbar Indicator:** Should show "Real-time Attivo" (green checkmark).

## Common Error Messages & Solutions

| Error | Cause | Solution |
|-------|-------|----------|
| `CORS policy: No 'Access-Control-Allow-Origin'` | CORS not configured or wrong origin | Add web app URL to `AlternativeWebUrls` |
| `404 Not Found: /dashboardHub/negotiate` | Hub not mapped or wrong URL | Verify `app.MapHub<DashboardHub>("/dashboardHub")` |
| `WebSocket connection failed` | SSL certificate issue | Trust development certificate |
| `Connection timeout` | Firewall or wrong port | Check port 5001 is open and API is running |
| `Failed to fetch` | API not running | Start API with `dotnet run` |
| `NET::ERR_CERT_AUTHORITY_INVALID` | Self-signed certificate | Trust certificate or use `http://` for testing |

## Production Deployment Notes

### HTTPS & Certificates

**Production:** Use valid SSL certificates from a trusted CA.

**Azure App Service:** Certificates managed automatically.

**IIS:** Bind certificate to site in IIS Manager.

### CORS Configuration

**Production:** Restrict to actual web app URLs only.

```json
{
  "WebAppUrl": "https://dashboard.contoso.com",
  "AlternativeWebUrls": []
}
```

### SignalR Scaling

For multiple API instances, use Redis backplane:

```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis("redis-connection-string", options => {
        options.Configuration.ChannelPrefix = "SecureBootDashboard";
    });
```

## References

- **SignalR Documentation:** https://learn.microsoft.com/aspnet/core/signalr
- **CORS in ASP.NET Core:** https://learn.microsoft.com/aspnet/core/security/cors
- **SignalR JavaScript Client:** https://learn.microsoft.com/aspnet/core/signalr/javascript-client

---

**Last Updated:** 2025-01-10  
**Status:** Troubleshooting in progress
