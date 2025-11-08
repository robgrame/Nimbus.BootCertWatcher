# Certificate Update API

## Overview

The Certificate Update API allows administrators to remotely trigger Secure Boot certificate updates for groups of devices. This feature enables centralized management of certificate deployment across Windows fleets.

## Architecture

The certificate update feature uses a command queue pattern:

```
┌─────────────────────────────────────────────┐
│  Dashboard API (ASP.NET Core 8)             │
│  ┌───────────────────────────────────────┐  │
│  │ POST /api/CertificateUpdate/trigger  │  │
│  │  • Validates request                 │  │
│  │  • Queries target devices            │  │
│  │  • Sends command to Azure Queue      │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
                    │
                    ▼
    ┌───────────────────────────────┐
    │  Azure Queue Storage          │
    │  secureboot-update-commands   │
    └───────────────────────────────┘
                    │
                    ▼ (clients poll)
┌─────────────────────────────────────────────┐
│  Windows Devices (.NET Framework 4.8)       │
│  ┌───────────────────────────────────────┐  │
│  │ SecureBootWatcher.Client              │  │
│  │  • Polls command queue                │  │
│  │  • Processes update commands          │  │
│  │  • Applies certificate updates        │  │
│  │  • Reports status                     │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

## API Endpoints

### Trigger Certificate Update

Sends a certificate update command for a fleet or specific devices.

**Endpoint**: `POST /api/CertificateUpdate/trigger`

**Request Body**:
```json
{
  "fleetId": "fleet-01",
  "targetDevices": ["DEVICE-01", "DEVICE-02"],
  "updateFlags": 23876,
  "issuedBy": "admin@example.com",
  "notes": "Deploying UEFI CA 2023 certificates",
  "expiresAtUtc": "2025-12-31T23:59:59Z"
}
```

**Request Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `fleetId` | string | No | The fleet ID to target. If null, targets all devices. |
| `targetDevices` | string[] | No | Specific device machine names. If empty, targets all devices in fleet. |
| `updateFlags` | uint | No | The update flags to apply. If null, uses Windows defaults. See [Update Flags](#update-flags) below. |
| `issuedBy` | string | Yes | User or system that issued this command. |
| `notes` | string | No | Additional notes or reason for this update. |
| `expiresAtUtc` | DateTimeOffset | No | When this command expires (optional). |

**Response** (200 OK):
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetDeviceCount": 10,
  "message": "Update command sent successfully to 10 device(s)"
}
```

**Response** (500 Internal Server Error):
```json
{
  "error": "Failed to trigger certificate update"
}
```

### Get Command Status

Retrieves the status of a certificate update command.

**Endpoint**: `GET /api/CertificateUpdate/status/{commandId}`

**Response** (200 OK):
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fleetId": "fleet-01",
  "targetDeviceCount": 10,
  "processedDeviceCount": 5,
  "issuedAtUtc": "2025-11-07T23:00:00Z",
  "completedAtUtc": null,
  "status": "InProgress"
}
```

**Response** (404 Not Found):
```json
{
  "error": "Command not found"
}
```

## Update Flags

The `updateFlags` parameter controls which certificates are deployed. Common values:

| Value (Hex) | Value (Decimal) | Description |
|-------------|-----------------|-------------|
| `0x5944` | 23876 | Initial state - All updates pending |
| `0x5904` | 22788 | Windows UEFI CA 2023 applied |
| `0x5104` | 20740 | Microsoft UEFI CA 2023 applied |
| `0x4104` | 16644 | Microsoft Option ROM CA 2023 applied |
| `0x4100` | 16640 | Microsoft KEK 2023 applied |
| `0x4000` | 16384 | Deployment complete |
| `null` | - | Use Windows defaults |

Individual flags:

| Flag | Value (Hex) | Value (Decimal) | Description |
|------|-------------|-----------------|-------------|
| `WindowsUefiCA2023` | `0x0040` | 64 | Apply Windows UEFI CA 2023 certificate |
| `MicrosoftUefiCA2023` | `0x0800` | 2048 | Apply Microsoft UEFI CA 2023 |
| `MicrosoftOptionRomCA2023` | `0x1000` | 4096 | Apply Microsoft Option ROM CA 2023 |
| `MicrosoftKEK2023` | `0x0004` | 4 | Apply Microsoft KEK 2023 |
| `WindowsBootManager2023` | `0x0100` | 256 | Apply Windows Boot Manager 2023 |
| `ConditionalMicrosoftCAs` | `0x4000` | 16384 | Apply MS CAs only if MS UEFI CA 2011 exists |

## Configuration

### Enable the Service

In `appsettings.json`:

```json
{
  "CertificateUpdateService": {
    "Enabled": true,
    "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
    "CommandQueueName": "secureboot-update-commands",
    "AuthenticationMethod": "DefaultAzureCredential"
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable/disable the certificate update service | `false` |
| `QueueServiceUri` | Azure Storage Queue service URI | - |
| `CommandQueueName` | Queue name for update commands | `secureboot-update-commands` |
| `AuthenticationMethod` | Authentication method (see below) | `DefaultAzureCredential` |

### Authentication Methods

The service supports multiple authentication methods:

#### 1. DefaultAzureCredential (Recommended for Development)
```json
{
  "AuthenticationMethod": "DefaultAzureCredential"
}
```
Tries multiple credential sources automatically (environment variables, managed identity, Visual Studio, Azure CLI, etc.).

#### 2. Managed Identity (Recommended for Production)
```json
{
  "AuthenticationMethod": "ManagedIdentity",
  "ClientId": "optional-client-id-for-user-assigned-identity"
}
```

#### 3. App Registration (Client Secret)
```json
{
  "AuthenticationMethod": "AppRegistration",
  "TenantId": "your-tenant-id",
  "ClientId": "your-client-id",
  "ClientSecret": "your-client-secret"
}
```

#### 4. Certificate-based Authentication
```json
{
  "AuthenticationMethod": "Certificate",
  "TenantId": "your-tenant-id",
  "ClientId": "your-client-id",
  "CertificateThumbprint": "YOUR-CERT-THUMBPRINT",
  "CertificateStoreLocation": "LocalMachine",
  "CertificateStoreName": "My"
}
```

Or with certificate file:
```json
{
  "AuthenticationMethod": "Certificate",
  "TenantId": "your-tenant-id",
  "ClientId": "your-client-id",
  "CertificatePath": "C:\\path\\to\\certificate.pfx",
  "CertificatePassword": "certificate-password"
}
```

## Azure Setup

### 1. Create Storage Account Queue

```bash
# Create queue using Azure CLI
az storage queue create \
  --name secureboot-update-commands \
  --account-name yourstorageaccount
```

### 2. Assign Permissions

For Managed Identity or App Registration:

```bash
# Get the principal ID (managed identity or service principal)
PRINCIPAL_ID="your-principal-id"

# Assign Storage Queue Data Contributor role
az role assignment create \
  --role "Storage Queue Data Contributor" \
  --assignee $PRINCIPAL_ID \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}/queueServices/default/queues/secureboot-update-commands"
```

## Usage Examples

### Example 1: Update All Devices in a Fleet

```bash
curl -X POST https://your-api.azurewebsites.net/api/CertificateUpdate/trigger \
  -H "Content-Type: application/json" \
  -d '{
    "fleetId": "fleet-production",
    "issuedBy": "admin@contoso.com",
    "notes": "Deploying UEFI CA 2023 to production fleet"
  }'
```

### Example 2: Update Specific Devices

```bash
curl -X POST https://your-api.azurewebsites.net/api/CertificateUpdate/trigger \
  -H "Content-Type: application/json" \
  -d '{
    "targetDevices": ["WORKSTATION-01", "WORKSTATION-02", "WORKSTATION-03"],
    "issuedBy": "admin@contoso.com",
    "notes": "Test deployment to pilot group"
  }'
```

### Example 3: Update with Custom Flags

```bash
curl -X POST https://your-api.azurewebsites.net/api/CertificateUpdate/trigger \
  -H "Content-Type: application/json" \
  -d '{
    "fleetId": "fleet-test",
    "updateFlags": 64,
    "issuedBy": "admin@contoso.com",
    "notes": "Deploy only Windows UEFI CA 2023 (0x0040)"
  }'
```

### Example 4: Check Command Status

```bash
curl https://your-api.azurewebsites.net/api/CertificateUpdate/status/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

## Client Implementation (Future)

The client-side implementation to poll and process update commands is not yet implemented. The client will need to:

1. Poll the `secureboot-update-commands` queue periodically
2. Receive and deserialize `CertificateUpdateCommandEnvelope` messages
3. Check if the command targets this device (by fleet ID or machine name)
4. Apply the certificate updates using Windows Update or registry manipulation
5. Send a status report back to the API
6. Delete the message from the queue

**Client polling logic** (pseudocode):
```csharp
while (true)
{
    var messages = await queueClient.ReceiveMessagesAsync();
    
    foreach (var message in messages)
    {
        var envelope = JsonSerializer.Deserialize<CertificateUpdateCommandEnvelope>(message.MessageText);
        var command = envelope.Command;
        
        // Check if this device is targeted
        if (ShouldProcessCommand(command))
        {
            // Apply the update
            await ApplyCertificateUpdateAsync(command);
            
            // Report status
            await ReportUpdateStatusAsync(command.CommandId);
            
            // Delete message only if processed by this device
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
        }
    }
    
    await Task.Delay(TimeSpan.FromMinutes(5));
}
```

## Security Considerations

- **Authentication**: Add `[Authorize]` attribute to controller endpoints in production
- **Authorization**: Implement role-based access control (RBAC) to restrict who can trigger updates
- **Audit Logging**: All update commands are logged with issuer information
- **Command Expiration**: Use `expiresAtUtc` to prevent old commands from being processed
- **Queue Access**: Use managed identity or certificates instead of connection strings
- **TLS/HTTPS**: Ensure all API calls use HTTPS
- **Input Validation**: The API validates all input parameters

## Monitoring

### Application Insights Queries

**Track update commands sent**:
```kusto
traces
| where message contains "Certificate update command"
| where message contains "sent to queue"
| project timestamp, commandId = extract("CommandId: ([a-f0-9-]+)", 1, message), deviceCount = extract("for ([0-9]+) devices", 1, message)
```

**Track update failures**:
```kusto
exceptions
| where outerMessage contains "CertificateUpdate"
| project timestamp, problemId, outerMessage, innermostMessage
```

### Metrics to Monitor

- Number of update commands issued per day
- Success rate of command queue sends
- Average devices targeted per command
- Command processing latency
- Queue message age

## Troubleshooting

### Service is Disabled

**Symptom**: API returns "Certificate update service is disabled"

**Solution**: Set `CertificateUpdateService:Enabled` to `true` in configuration

### Queue Client Creation Failed

**Symptom**: Error "Failed to create queue client"

**Solution**: 
- Verify `QueueServiceUri` is correct
- Check authentication settings (TenantId, ClientId, etc.)
- Ensure queue exists in storage account
- Verify permissions on the queue

### No Devices Found

**Symptom**: Response shows `targetDeviceCount: 0`

**Solution**:
- Verify fleet ID is correct
- Check device names are exact matches
- Ensure devices have reported to the API at least once

### Authentication Failed

**Symptom**: Azure authentication errors in logs

**Solution**:
- For Managed Identity: Ensure identity is assigned to App Service
- For App Registration: Verify client secret hasn't expired
- For Certificate: Check certificate is installed and accessible
- Verify role assignments on storage queue

## API Reference

### Models

#### CertificateUpdateCommand

```csharp
public sealed class CertificateUpdateCommand
{
    public Guid CommandId { get; set; }
    public string? FleetId { get; set; }
    public string[] TargetDevices { get; set; }
    public uint? UpdateFlags { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string? IssuedBy { get; set; }
    public string? Notes { get; set; }
}
```

#### CertificateUpdateCommandEnvelope

```csharp
public sealed class CertificateUpdateCommandEnvelope
{
    public string Version { get; set; } = "1.0";
    public string MessageType { get; set; } = "CertificateUpdateCommand";
    public CertificateUpdateCommand Command { get; set; }
    public DateTimeOffset EnqueuedAtUtc { get; set; }
}
```

## Future Enhancements

1. **Command Tracking**: Store commands in database with full audit trail
2. **Device Acknowledgment**: Track which devices have processed each command
3. **Retry Logic**: Automatic retry for failed updates
4. **Scheduling**: Schedule updates for specific date/time
5. **Staged Rollout**: Deploy to devices in batches over time
6. **Approval Workflow**: Multi-stage approval before sending commands
7. **PowerShell Integration**: PowerShell cmdlets for triggering updates
8. **Web UI**: Dashboard interface for managing update campaigns

## Related Documentation

- [Secure Boot Update Flags](../SecureBootWatcher.Shared/Models/SecureBootUpdateFlags.cs)
- [Azure Queue Entra ID Authentication](AZURE_QUEUE_ENTRA_ID_AUTH.md)
- [Queue Processor Service](QUEUE_PROCESSOR_MONITORING.md)
- [API Architecture](WEB_IMPLEMENTATION_SUMMARY.md)

## Support

For issues or questions:
- GitHub Issues: [Report bugs or request features](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)
- Documentation: See [docs/](.) directory
