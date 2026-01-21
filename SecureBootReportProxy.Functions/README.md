# Secure Boot Report Proxy - Azure Function

An Azure Function that acts as a secure proxy for Secure Boot certificate reports from on-premises clients. This function eliminates the need to distribute Azure Storage credentials or certificates to client machines.

## Architecture

```
Client (on-prem)
    ↓ HTTP POST with API Key (+ optional certificate)
Azure Function (Proxy)
    ↓ Managed Identity / DefaultAzureCredential
Azure Storage Queue
    ↓
Dashboard API (QueueProcessor)
```

## Features

- **API Key Authentication**: Simple header-based or query parameter authentication
- **Optional Certificate Authentication**: Mutual TLS for enhanced security
- **Certificate Validation**: Validates client certificates (expiration, thumbprint allowlist)
- **Managed Identity**: Uses Azure Managed Identity to access Queue Storage (no credentials in code)
- **Auto-scaling**: Azure Functions automatically scale based on load
- **Built-in Monitoring**: Application Insights integration for telemetry and logging
- **Defense in Depth**: Supports both API key and certificate authentication simultaneously

## Configuration

### Environment Variables / Application Settings

The function requires the following configuration (set in `local.settings.json` for local development or Application Settings in Azure):

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| `QueueStorageUri` | Azure Storage Queue service URI | Yes | `https://mystorageaccount.queue.core.windows.net` |
| `QueueName` | Name of the queue to send reports to | Yes | `secureboot-reports` |
| `ApiKey` | Secret API key for authenticating client requests | Yes | `your-secure-api-key-here` |
| `RequireCertificateAuthentication` | Whether to require client certificates | No (default: `false`) | `true` or `false` |
| `CertificateThumbprints` | Comma-separated list of allowed certificate thumbprints | No (optional) | `ABC123...,DEF456...` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights connection string for telemetry | No (optional) | From Azure Portal |

### Authentication Methods

#### 1. API Key Only (Default)

Clients send the API key in one of two ways:

**Option A: Header (Recommended)**
```http
POST /api/reports HTTP/1.1
Host: your-function-app.azurewebsites.net
X-API-Key: your-api-key-here
Content-Type: application/json
```

**Option B: Query Parameter**
```http
POST /api/reports?code=your-api-key-here HTTP/1.1
Host: your-function-app.azurewebsites.net
Content-Type: application/json
```

#### 2. API Key + Certificate (Enhanced Security)

Set `RequireCertificateAuthentication=true` to require mutual TLS. Azure App Service validates the client certificate and passes it to the function via the `X-ARR-ClientCert` header.

**Steps to enable:**
1. Configure mutual TLS in Azure App Service
2. Set `RequireCertificateAuthentication=true`
3. Optionally set `CertificateThumbprints` to restrict to specific certificates
4. Clients must present a valid certificate during TLS handshake

## Deployment

### Prerequisites

- Azure subscription
- Azure Functions Core Tools (for local development)
- .NET 10 SDK
- Azure CLI (optional, for command-line deployment)

### Local Development

1. **Install Azure Functions Core Tools**
   ```bash
   npm install -g azure-functions-core-tools@4
   ```

2. **Configure local settings**
   
   Edit `local.settings.json`:
   ```json
   {
     "Values": {
       "QueueStorageUri": "https://yourstorageaccount.queue.core.windows.net",
       "QueueName": "secureboot-reports",
       "ApiKey": "your-local-test-api-key",
       "RequireCertificateAuthentication": "false"
     }
   }
   ```

3. **Run locally**
   ```bash
   func start
   ```

4. **Test with curl**
   ```bash
   curl -X POST http://localhost:7071/api/reports \
     -H "X-API-Key: your-local-test-api-key" \
     -H "Content-Type: application/json" \
     -d @sample-report.json
   ```

### Azure Deployment

#### Option 1: Visual Studio Code

1. Install Azure Functions extension
2. Right-click project folder
3. Select "Deploy to Function App..."
4. Follow prompts to create or select Function App

#### Option 2: Azure CLI

```bash
# Create resource group
az group create --name rg-secureboot --location eastus

# Create storage account
az storage account create \
  --name stsecureboot \
  --resource-group rg-secureboot \
  --sku Standard_LRS

# Create Function App
az functionapp create \
  --resource-group rg-secureboot \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 10 \
  --functions-version 4 \
  --name func-secureboot-proxy \
  --storage-account stsecureboot

# Deploy function code
func azure functionapp publish func-secureboot-proxy

# Configure settings
az functionapp config appsettings set \
  --name func-secureboot-proxy \
  --resource-group rg-secureboot \
  --settings \
    QueueStorageUri=https://yourstorageaccount.queue.core.windows.net \
    QueueName=secureboot-reports \
    ApiKey=your-production-api-key-here
```

### Configure Managed Identity

The function uses Managed Identity to access Azure Queue Storage. Configure permissions:

```bash
# Enable system-assigned managed identity
az functionapp identity assign \
  --name func-secureboot-proxy \
  --resource-group rg-secureboot

# Grant Storage Queue Data Contributor role
PRINCIPAL_ID=$(az functionapp identity show \
  --name func-secureboot-proxy \
  --resource-group rg-secureboot \
  --query principalId -o tsv)

STORAGE_ID=$(az storage account show \
  --name yourstorageaccount \
  --resource-group rg-secureboot \
  --query id -o tsv)

az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID
```

## Security Best Practices

### API Key Management

1. **Use Azure Key Vault**: Store API keys in Key Vault, reference as `@Microsoft.KeyVault(SecretUri=...)`
2. **Rotate keys regularly**: Implement key rotation policy
3. **Use different keys per environment**: Dev, staging, production should have unique keys
4. **Monitor usage**: Set up alerts for unusual API key usage patterns

### Certificate Authentication

1. **Enable mutual TLS**: Configure in Azure App Service settings
2. **Validate thumbprints**: Use `CertificateThumbprints` to restrict to known certificates
3. **Monitor certificate expiration**: Set up alerts before certificates expire
4. **Use strong certificates**: 2048-bit RSA or 256-bit ECC minimum

### Network Security

1. **Enable HTTPS only**: Disable HTTP in Azure App Service
2. **Consider Private Endpoints**: For enhanced network isolation
3. **Use IP restrictions**: Limit access to known IP ranges if possible
4. **Enable DDoS protection**: If using dedicated infrastructure

## Monitoring

### Application Insights

The function automatically logs to Application Insights:

- **Traces**: Function execution logs
- **Requests**: HTTP request telemetry
- **Dependencies**: Queue Storage operations
- **Exceptions**: Error tracking

### Key Metrics to Monitor

- **Function invocations**: Track usage patterns
- **Failures**: Monitor authentication and queue failures
- **Latency**: P50, P95, P99 response times
- **Queue depth**: Monitor downstream processing

### Sample KQL Queries

```kusto
// Authentication failures
traces
| where message has "authentication failed"
| summarize count() by bin(timestamp, 1h)

// Average processing time
requests
| where name == "SecureBootReportIngestion"
| summarize avg(duration) by bin(timestamp, 5m)

// Queue operation failures
dependencies
| where type == "Azure queue"
| where success == false
| project timestamp, operation_Name, resultCode, duration
```

## Troubleshooting

### Common Issues

**401 Unauthorized**
- Check API key is correct
- Verify header name is `X-API-Key` (case-sensitive)
- Ensure no extra spaces in API key value

**403 Forbidden**
- Certificate authentication is enabled but certificate is invalid
- Certificate has expired
- Certificate thumbprint not in allowlist

**503 Service Unavailable**
- Queue Storage is unreachable
- Managed Identity lacks permissions
- Queue name is incorrect

### Debug Locally

1. Enable verbose logging in `host.json`:
   ```json
   {
     "logging": {
       "logLevel": {
         "default": "Debug"
       }
     }
   }
   ```

2. View detailed logs:
   ```bash
   func start --verbose
   ```

3. Use Application Insights Live Metrics for real-time monitoring in Azure

## Performance

- **Cold Start**: ~1-3 seconds (isolated worker model)
- **Warm Execution**: ~50-200ms typical
- **Throughput**: Scales automatically based on load
- **Concurrency**: Default 200 concurrent requests per instance

## License

This function is part of the Secure Boot Certificate Watcher project.
