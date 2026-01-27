# Client Sink Capabilities Comparison

This document compares the sink implementations between the .NET Framework client and PowerShell client for SecureBootWatcher.

## Overview

SecureBootWatcher supports multiple sink types for sending Secure Boot certificate reports. However, the implementation capabilities differ between the .NET client and PowerShell client.

## Sink Comparison Matrix

| Sink Type | .NET Client | PowerShell Client | Recommended For |
|-----------|-------------|-------------------|-----------------|
| **AzureFunction** | ✅ Full Support | ✅ Full Support | Cloud-based deployments with simple authentication |
| **AzureQueue** | ✅ Full Support | ❌ Not Supported | Enterprise Azure deployments with complex authentication |
| **WebApi** | ✅ Full Support | ✅ Full Support | Direct API ingestion, custom endpoints |
| **FileShare** | ✅ Full Support | ✅ Full Support | Local/network file storage, offline scenarios |

## Detailed Sink Capabilities

### 1. AzureFunction Sink

**Purpose:** HTTP-based ingestion via Azure Function endpoints

#### .NET Client Features
- ✅ API key authentication (header or query parameter)
- ✅ Optional client certificate authentication (mutual TLS)
- ✅ Certificate chain validation
- ✅ DNS pre-flight checks
- ✅ Configurable timeout
- ✅ Exponential backoff retry

#### PowerShell Client Features
- ✅ API key authentication (header or query parameter)
- ✅ Configurable timeout
- ✅ Simple error handling
- ❌ No certificate authentication support
- ❌ No retry logic

**Configuration Example:**
```json
{
  "Sinks": {
    "EnableAzureFunction": true,
    "AzureFunction": {
      "FunctionUrl": "https://your-function-app.azurewebsites.net/api/reports",
      "ApiKey": "your-api-key-here",
      "HttpTimeout": "00:00:30",
      "UseApiKeyAsQueryParameter": false
    }
  }
}
```

**When to Use:**
- ✅ Simplified deployment (no certificate distribution required)
- ✅ Lightweight authentication needs
- ✅ Cost-effective for small to medium fleets
- ✅ Both .NET and PowerShell clients

### 2. AzureQueue Sink

**Purpose:** Message queue-based delivery to Azure Storage Queue

#### .NET Client Features
- ✅ 5 authentication methods:
  - **ManagedIdentity** (system or user-assigned)
  - **AppRegistration** (client secret)
  - **Certificate** (file or Windows Certificate Store)
  - **DefaultAzureCredential** (automatic fallback)
  - **ConnectionString** (not recommended for production)
- ✅ Automatic queue creation
- ✅ Built-in Polly retry (3 attempts, exponential backoff)
- ✅ Certificate thumbprint validation
- ✅ Detailed logging for authentication

#### PowerShell Client Features
- ❌ **NOT IMPLEMENTED**
- ❌ Requires Azure.Storage.Queues SDK (not available in PowerShell)
- ❌ Complex authentication not feasible in pure PowerShell

**Why Not Supported in PowerShell:**
1. PowerShell lacks the Azure.Storage.Queues SDK
2. Complex authentication mechanisms (Managed Identity, Certificate) require .NET libraries
3. Token refresh and credential management is difficult in PowerShell
4. Maintenance burden for equivalent functionality

**Recommendation:** Use AzureFunction or WebApi sink for PowerShell clients instead.

### 3. WebApi Sink

**Purpose:** Direct HTTP POST to custom Web API endpoints

#### .NET Client Features
- ✅ Certificate authentication support (optional)
- ✅ DNS pre-flight checks
- ✅ Customizable ingestion route
- ✅ Configurable timeout
- ✅ Certificate chain validation
- ✅ CRL checking (optional)

#### PowerShell Client Features
- ✅ Basic HTTP POST
- ✅ Customizable headers
- ✅ Configurable timeout
- ✅ Simple error handling
- ❌ No certificate authentication
- ❌ No advanced validation

**Configuration Example:**
```json
{
  "Sinks": {
    "EnableWebApi": true,
    "WebApi": {
      "BaseAddress": "https://your-api.azurewebsites.net",
      "IngestionRoute": "/api/SecureBootReports",
      "HttpTimeout": "00:02:00"
    }
  }
}
```

**When to Use:**
- ✅ Direct ingestion to custom APIs
- ✅ On-premises deployments
- ✅ Custom authentication mechanisms
- ✅ Both .NET and PowerShell clients

### 4. FileShare Sink

**Purpose:** Local or network file system persistence

#### .NET Client Features
- ✅ UNC path support
- ✅ Automatic directory creation
- ✅ Customizable file extension
- ✅ Optional timestamp in filename
- ✅ Correlation ID in filename

#### PowerShell Client Features
- ✅ UNC path support
- ✅ Automatic directory creation
- ✅ Customizable file extension
- ✅ Timestamp in filename (always)
- ✅ Domain and computer name in filename

**Configuration Example:**
```json
{
  "Sinks": {
    "EnableFileShare": true,
    "FileShare": {
      "RootPath": "\\\\server\\share\\secureboot-reports",
      "FileExtension": ".json"
    }
  }
}
```

**When to Use:**
- ✅ Offline scenarios (no network connectivity)
- ✅ Local backup/audit trail
- ✅ Temporary storage before cloud upload
- ✅ Both .NET and PowerShell clients

## Sink Priority and Execution Strategy

Both clients support configurable sink priority with failover:

### .NET Client
- **Strategies:** `StopOnFirstSuccess` (default) or `TryAll`
- **Retry:** Per-sink retry with exponential backoff
- **Default Priority:** `AzureFunction,AzureQueue,WebApi,FileShare`
- **Configuration Source:** Database-first (5-min cache), fallback to appsettings.json

### PowerShell Client
- **Strategies:** `FirstSuccess` (only)
- **Retry:** None (single attempt per sink)
- **Default Priority:** `AzureFunction,WebApi,FileShare`
- **Configuration Source:** appsettings.json only

## Recommended Sink Configurations

### Scenario 1: Cloud-First with Local Fallback (.NET Client)
```json
{
  "ExecutionStrategy": "StopOnFirstSuccess",
  "SinkPriority": "AzureFunction,FileShare",
  "EnableAzureFunction": true,
  "EnableFileShare": true
}
```

**Behavior:**
1. Try AzureFunction → Success → STOP (99% of cases)
2. If fails → Try FileShare → Success (local backup)

### Scenario 2: Simple PowerShell Deployment
```json
{
  "ExecutionStrategy": "FirstSuccess",
  "SinkPriority": "AzureFunction,WebApi",
  "EnableAzureFunction": true,
  "EnableWebApi": true
}
```

**Behavior:**
1. Try AzureFunction → Success → STOP
2. If fails → Try WebApi → Success

### Scenario 3: Enterprise Azure (.NET Client Only)
```json
{
  "ExecutionStrategy": "StopOnFirstSuccess",
  "SinkPriority": "AzureQueue,AzureFunction,FileShare",
  "EnableAzureQueue": true,
  "EnableAzureFunction": true,
  "EnableFileShare": true,
  "AzureQueue": {
    "AuthenticationMethod": "Certificate",
    "TenantId": "...",
    "ClientId": "...",
    "CertificateThumbprint": "..."
  }
}
```

**Behavior:**
1. Try AzureQueue (most secure) → Success → STOP
2. If fails → Try AzureFunction → Success
3. If fails → Try FileShare → Success (last resort)

## Migration Guide

### From .NET AzureQueue to PowerShell-Compatible Sink

If you're using AzureQueue in .NET and need to support PowerShell clients:

**Option 1: Switch to AzureFunction**
- Deploy an Azure Function as a proxy
- Use API key authentication
- Function writes to Queue Storage using Managed Identity
- Both .NET and PowerShell clients use the same endpoint

**Option 2: Use WebApi Directly**
- Point both clients to the Web API endpoint
- API writes to database directly or to Queue Storage
- Consistent behavior across both clients

**Option 3: Hybrid Approach**
- .NET clients: Use AzureQueue (most efficient)
- PowerShell clients: Use AzureFunction or WebApi
- Configure different sink priorities per client type

## Troubleshooting

### PowerShell: "Azure Queue sink is not implemented"

**Cause:** PowerShell client does not support Azure Queue sink.

**Solution:** 
1. Set `EnableAzureQueue: false` in configuration
2. Use `EnableAzureFunction: true` or `EnableWebApi: true` instead
3. Update `SinkPriority` to exclude `AzureQueue`

### .NET: "DNS resolution failed"

**Cause:** Azure Function or WebApi endpoint hostname cannot be resolved.

**Solution:**
1. Verify network connectivity
2. Check DNS settings
3. Test with `nslookup <hostname>`
4. Consider using FileShare as fallback sink

### Both: "All enabled sinks failed"

**Cause:** All configured sinks failed to accept the report.

**Solution:**
1. Enable debug logging: `"Default": "Debug"`
2. Check each sink's configuration
3. Verify network connectivity, credentials, and permissions
4. Test each sink individually
5. Add FileShare as a last-resort fallback

## Summary

| Client Type | Best Sink Choices | Avoid |
|-------------|-------------------|-------|
| **.NET Client (Production)** | AzureQueue, AzureFunction, FileShare | ConnectionString auth |
| **.NET Client (Simple)** | AzureFunction, WebApi, FileShare | - |
| **PowerShell Client** | AzureFunction, WebApi, FileShare | AzureQueue |
| **Mixed Environment** | AzureFunction (common), WebApi (common) | Client-specific queue configs |

**Key Takeaway:** For deployments with both .NET and PowerShell clients, use **AzureFunction** or **WebApi** sinks for consistency and compatibility.
