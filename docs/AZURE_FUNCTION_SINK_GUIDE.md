# Azure Function Proxy Sink - Configuration Guide

## Overview

The Azure Function Proxy Sink provides a simplified and secure way to send Secure Boot reports from on-premises clients to the Azure-based dashboard without distributing Azure Storage credentials or certificates to client machines.

## Architecture

```
┌─────────────────────┐
│  On-Premises Client │
│  (SecureBootWatcher)│
└──────────┬──────────┘
           │ HTTP POST
           │ • API Key (required)
           │ • Client Certificate (optional)
           │
           ▼
┌─────────────────────┐
│  Azure Function     │
│  (Proxy)            │
│  • API Key Auth     │
│  • Cert Validation  │
└──────────┬──────────┘
           │ Managed Identity
           │ (no credentials)
           │
           ▼
┌─────────────────────┐
│  Azure Queue        │
│  Storage            │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  Dashboard API      │
│  (QueueProcessor)   │
└─────────────────────┘
```

## Benefits

### Security
- **No credential distribution**: Clients only need an API key
- **Centralized authentication**: Azure Function handles all queue access
- **Optional certificate auth**: Defense-in-depth with mutual TLS
- **Certificate validation**: Full chain validation and CRL checking support
- **Managed Identity**: Function uses Azure-managed authentication

### Deployment
- **Simple configuration**: Single JSON file on client
- **Intune-friendly**: Easy to deploy via Intune or GPO
- **No certificates required**: Optional, not mandatory
- **Backward compatible**: Existing sinks continue to work

### Operations
- **Auto-scaling**: Azure Function scales automatically
- **High availability**: Azure platform reliability
- **Built-in monitoring**: Application Insights integration
- **Cost-effective**: Pay-per-use model

## Configuration

### 1. API Key Only (Simplest)

**Client configuration** (`appsettings.json`):
```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableAzureFunction": true,
      "AzureFunction": {
        "FunctionUrl": "https://your-function-app.azurewebsites.net/api/reports",
        "ApiKey": "your-secure-api-key-here",
        "HttpTimeout": "00:00:30"
      }
    }
  }
}
```

**Azure Function settings**:
```
ApiKey=your-secure-api-key-here
QueueStorageUri=https://yourstorageaccount.queue.core.windows.net
QueueName=secureboot-reports
RequireCertificateAuthentication=false
```

### 2. API Key + Certificate (Most Secure)

**Client configuration**:
```json
{
  "SecureBootWatcher": {
    "Sinks": {
      "EnableAzureFunction": true,
      "AzureFunction": {
        "FunctionUrl": "https://your-function-app.azurewebsites.net/api/reports",
        "ApiKey": "your-secure-api-key-here",
        "HttpTimeout": "00:00:30",
        "UseCertificateAuth": true,
        "CertificateThumbprint": "ABC123DEF456...",
        "CertificateStoreLocation": "LocalMachine",
        "CertificateStoreName": "My",
        "ValidateCertificateChain": true,
        "CheckCertificateRevocation": false
      }
    }
  }
}
```

**Azure Function settings**:
```
ApiKey=your-secure-api-key-here
QueueStorageUri=https://yourstorageaccount.queue.core.windows.net
QueueName=secureboot-reports
RequireCertificateAuthentication=true
CertificateThumbprints=ABC123DEF456...,789GHI012JKL...
```

**Azure App Service settings** (for mutual TLS):
1. Go to Azure Portal → App Service → Configuration
2. Set `WEBSITE_CLIENT_CERT_MODE` to `Required` or `Optional`
3. Optionally set `WEBSITE_CLIENT_CERT_ISSUER` for CA validation

## Certificate Setup

### Client-Side Certificate

1. **Generate or obtain certificate**:
   ```powershell
   # Option 1: Self-signed for testing
   $cert = New-SelfSignedCertificate `
       -Subject "CN=SecureBootClient" `
       -CertStoreLocation "Cert:\LocalMachine\My" `
       -KeyExportPolicy Exportable `
       -KeyUsage DigitalSignature,KeyEncipherment `
       -KeyAlgorithm RSA `
       -KeyLength 2048 `
       -NotAfter (Get-Date).AddYears(2)
   
   # Option 2: Use enterprise CA certificate
   # Request certificate from your PKI
   ```

2. **Install certificate on client** (if using file):
   ```powershell
   $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
   $cert.Import("C:\Certs\client-cert.pfx", "password", "MachineKeySet")
   
   $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("My", "LocalMachine")
   $store.Open("ReadWrite")
   $store.Add($cert)
   $store.Close()
   ```

3. **Get certificate thumbprint**:
   ```powershell
   Get-ChildItem Cert:\LocalMachine\My | Where-Object Subject -like "*SecureBootClient*"
   ```

4. **Configure client** with thumbprint from step 3

### Azure Function Certificate Validation

1. **Enable mutual TLS** in Azure App Service
2. **Configure allowed thumbprints** in function settings
3. **Test connection** from client

## Sink Priority

The default sink priority includes Azure Function first:

```json
{
  "Sinks": {
    "SinkPriority": "AzureFunction,AzureQueue,WebApi,FileShare",
    "ExecutionStrategy": "StopOnFirstSuccess"
  }
}
```

This means:
1. Try Azure Function first (fastest, simplest)
2. If Azure Function fails, try Azure Queue (direct access)
3. If Azure Queue fails, try Web API (alternative HTTP)
4. If Web API fails, try File Share (last resort)

## Certificate Validation Options

### Client-Side Validation

```json
{
  "AzureFunction": {
    "ValidateCertificateChain": true,
    "CheckCertificateRevocation": false
  }
}
```

**ValidateCertificateChain** (default: `true`):
- Validates certificate is signed by trusted CA
- Checks all certificates in the chain
- Verifies trust relationships
- **Recommended**: Always `true` for production

**CheckCertificateRevocation** (default: `false`):
- Checks Certificate Revocation List (CRL)
- Verifies certificate hasn't been revoked
- **Warning**: Can cause delays if CRL server is unavailable
- **Recommended**: `false` unless required by security policy

### Function-Side Validation

The Azure Function validates:
- Certificate expiration dates
- Certificate thumbprint (if allowlist configured)
- Certificate can be parsed
- API key is still required (defense in depth)

## Troubleshooting

### Client Cannot Connect

**Symptoms**: DNS resolution errors, connection timeout

**Solutions**:
1. Verify function URL is correct
2. Check firewall allows outbound HTTPS
3. Test DNS resolution: `nslookup your-function-app.azurewebsites.net`
4. Test connectivity: `Test-NetConnection your-function-app.azurewebsites.net -Port 443`

### 401 Unauthorized

**Symptoms**: HTTP 401 error in client logs

**Solutions**:
1. Verify API key is correct (no extra spaces)
2. Check API key is configured in function settings
3. Ensure `X-API-Key` header is being sent
4. Check function logs for authentication failures

### 403 Forbidden

**Symptoms**: HTTP 403 error when certificate auth is enabled

**Solutions**:
1. Verify certificate is installed in correct store
2. Check certificate hasn't expired
3. Verify certificate thumbprint is in allowlist (if configured)
4. Check certificate has private key
5. Enable mutual TLS in Azure App Service
6. Review function logs for certificate validation failures

### Certificate Chain Validation Fails

**Symptoms**: Client logs show chain validation errors

**Solutions**:
1. Check intermediate CA certificates are installed
2. Verify root CA is trusted
3. Consider setting `ValidateCertificateChain: false` for testing (not recommended for production)
4. Check certificate chain: `certutil -verify -urlfetch cert.cer`

### Reports Not Appearing in Dashboard

**Symptoms**: Client succeeds, but reports don't show in dashboard

**Solutions**:
1. Check function logs for queue errors
2. Verify queue name is correct
3. Check managed identity has permissions
4. Verify queue processor is running
5. Check Application Insights for function failures

## Monitoring

### Client-Side Logs

Located in `logs/client-YYYYMMDD.log`:

```
[INFO] SecureBootWatcher Client Starting
[INFO] Azure Function Sink: Enabled
[INFO] Function URL: https://your-function-app.azurewebsites.net/api/reports
[INFO] Certificate Auth: Enabled
[INFO] Client certificate loaded from store: Subject=CN=SecureBootClient
[INFO] Certificate chain validation passed successfully
[INFO] AzureFunctionReportSink: Successfully submitted report
```

### Function-Side Logs (Application Insights)

Query for authentication attempts:
```kusto
traces
| where message has "authentication"
| project timestamp, severityLevel, message
| order by timestamp desc
```

Query for queue operations:
```kusto
dependencies
| where type == "Azure queue"
| project timestamp, name, success, duration, resultCode
| order by timestamp desc
```

## Performance

### Expected Latency

- **Client to Function**: 50-200ms (typically)
- **Function to Queue**: 10-50ms (typically)
- **Total End-to-End**: 100-300ms (typically)

### Throughput

- **Function**: Auto-scales based on load
- **Queue**: 2,000 messages/second per queue
- **Client**: Single-threaded (one report per execution)

## Security Best Practices

1. **API Key Management**:
   - Store in Azure Key Vault
   - Rotate quarterly
   - Use different keys per environment
   - Never commit keys to source control

2. **Certificate Management**:
   - Use enterprise PKI when available
   - Set appropriate validity periods (1-2 years)
   - Implement certificate rotation process
   - Monitor expiration dates

3. **Network Security**:
   - Use HTTPS only (enforced by default)
   - Consider IP restrictions for function
   - Enable Azure DDoS Protection
   - Use private endpoints if required

4. **Monitoring**:
   - Set up alerts for authentication failures
   - Monitor function execution failures
   - Track unusual usage patterns
   - Review logs regularly

## Migration from Other Sinks

### From Azure Queue Sink

1. Deploy Azure Function
2. Update client configuration to enable AzureFunction sink
3. Keep AzureQueue sink enabled as fallback
4. Monitor both sinks for a period
5. Disable AzureQueue sink once confidence is established

### From Web API Sink

1. Deploy Azure Function
2. Update sink priority: `"SinkPriority": "AzureFunction,WebApi"`
3. Both sinks can run in parallel
4. Gradually transition clients to Azure Function

### From File Share Sink

1. Deploy Azure Function
2. Enable AzureFunction sink
3. Keep FileShare as fallback
4. Monitor and validate Azure Function reliability
5. Consider disabling FileShare once migrated

## Cost Estimation

Azure Function with consumption plan:

| Resource | Monthly Cost (estimate) |
|----------|-------------------------|
| Executions (100,000) | $20 |
| Execution time | Included |
| Storage Queue (1M operations) | $0.05 |
| Application Insights (5 GB) | $10 |
| **Total** | **~$30/month** |

*Note: Actual costs depend on usage patterns and region*

## Support

For issues or questions:
1. Check client logs first
2. Check function logs in Application Insights
3. Review this documentation
4. Open GitHub issue with detailed logs
