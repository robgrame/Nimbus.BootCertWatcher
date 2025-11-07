# Automated Remediation Workflows

## Overview

The Automated Remediation Workflows feature enables administrators to define and execute automated actions in response to specific device conditions and alerts. This feature was implemented as part of the v1.2 roadmap.

## Key Features

- **Flexible Triggers**: Define conditions based on deployment state, fleet ID, manufacturer, certificate expiration, alerts, and more
- **Multiple Actions**: Execute log entries, update device tags, send notifications, or call webhooks
- **Priority-based Execution**: Control the order in which workflows are evaluated and executed
- **Execution History**: Track all workflow executions with detailed results
- **Enable/Disable**: Easily enable or disable workflows without deletion

## Architecture

### Data Models

#### RemediationWorkflow
Defines the workflow configuration including:
- Name and description
- Enabled status
- Priority (lower numbers execute first)
- Trigger conditions
- List of actions to execute

#### WorkflowTrigger
Specifies conditions that must be met for the workflow to execute:
- `DeploymentState`: Match specific deployment states (NotStarted, InProgress, Updated, Error)
- `FleetIdMatches`: Filter by fleet ID (comma-separated for multiple)
- `ManufacturerMatches`: Filter by device manufacturer
- `NoReportForDays`: Trigger when device hasn't reported in N days
- `AlertContains`: Match alerts containing specific text
- `HasExpiredCertificates`: Trigger on expired certificates
- `CertificateExpiringWithinDays`: Trigger on certificates expiring soon

#### WorkflowAction
Defines an action to execute:
- `ActionType`: Type of action (LogEntry, EmailNotification, Webhook, UpdateDeviceTags)
- `ConfigurationJson`: JSON configuration for the action
- `Order`: Execution order

#### WorkflowExecution
Records each execution instance:
- Workflow and device references
- Start and completion times
- Status (Running, Completed, Failed, PartialSuccess)
- Detailed results for each action

### Components

#### WorkflowEngine Service
Core engine that:
1. Evaluates enabled workflows against device/report data
2. Checks if trigger conditions are met
3. Executes actions in order
4. Records execution results in the database

#### RemediationWorkflowsController
REST API endpoints for workflow management:
- `GET /api/RemediationWorkflows` - List all workflows
- `GET /api/RemediationWorkflows/{id}` - Get workflow details
- `POST /api/RemediationWorkflows` - Create new workflow
- `PUT /api/RemediationWorkflows/{id}` - Update workflow
- `DELETE /api/RemediationWorkflows/{id}` - Delete workflow
- `GET /api/RemediationWorkflows/{id}/executions` - Get execution history

#### Web UI Pages
- **Workflows List**: View and manage all workflows
- **Create Workflow**: Define new automated workflows
- **Workflow Details**: View workflow configuration and execution history

## Action Types

### 1. LogEntry
Logs a message to the application log.

**Configuration Example:**
```json
{
  "message": "Device requires attention - expired certificates detected"
}
```

### 2. UpdateDeviceTags
Updates or adds tags to the device metadata.

**Configuration Example:**
```json
{
  "remediation_required": "true",
  "last_alert_date": "2025-01-07"
}
```

### 3. EmailNotification (Coming Soon)
Sends an email notification to specified recipients.

**Configuration Example:**
```json
{
  "to": "admin@example.com",
  "subject": "Device Alert: {MachineName}",
  "body": "Device {MachineName} has {AlertCount} alerts"
}
```

### 4. Webhook (Coming Soon)
Calls an HTTP endpoint with device and alert information.

**Configuration Example:**
```json
{
  "url": "https://example.com/api/webhook",
  "method": "POST",
  "headers": {
    "Authorization": "Bearer {token}"
  }
}
```

## Usage Examples

### Example 1: Alert on Expired Certificates

**Trigger:**
- `HasExpiredCertificates`: true
- `FleetIdMatches`: "production"

**Action:**
- Type: LogEntry
- Configuration: `{"message": "CRITICAL: Production device has expired certificates"}`

### Example 2: Tag Devices with Expiring Certificates

**Trigger:**
- `CertificateExpiringWithinDays`: 30
- `DeploymentState`: "Updated"

**Action:**
- Type: UpdateDeviceTags
- Configuration: `{"cert_expiring_soon": "true", "review_required": "true"}`

### Example 3: Monitor Stale Devices

**Trigger:**
- `NoReportForDays`: 7

**Action:**
- Type: LogEntry
- Configuration: `{"message": "Device has not reported in over 7 days"}`

## Best Practices

1. **Start Disabled**: Create workflows in disabled state initially to test configuration
2. **Use Priority**: Assign lower priority numbers to critical workflows
3. **Specific Triggers**: Make triggers as specific as possible to avoid false positives
4. **Test Actions**: Test action configurations with a single device before enabling globally
5. **Monitor Executions**: Regularly review execution history to ensure workflows are working as expected
6. **Document Purpose**: Use the description field to explain what each workflow does

## Database Schema

### RemediationWorkflows Table
- `Id` (Guid): Primary key
- `Name` (string): Workflow name
- `Description` (string): Optional description
- `IsEnabled` (bool): Enable/disable flag
- `Priority` (int): Execution priority
- `TriggerJson` (nvarchar(max)): Serialized trigger conditions
- `ActionsJson` (nvarchar(max)): Serialized action list
- `CreatedAtUtc` (DateTimeOffset): Creation timestamp
- `UpdatedAtUtc` (DateTimeOffset): Last update timestamp
- `CreatedBy` (string): Creator identifier
- `UpdatedBy` (string): Last updater identifier

### WorkflowExecutions Table
- `Id` (Guid): Primary key
- `WorkflowId` (Guid): Foreign key to RemediationWorkflows
- `DeviceId` (Guid): Foreign key to Devices
- `ReportId` (Guid): Foreign key to SecureBootReports (nullable)
- `StartedAtUtc` (DateTimeOffset): Execution start time
- `CompletedAtUtc` (DateTimeOffset): Execution completion time
- `Status` (int): Execution status enum
- `ResultMessage` (string): Summary message
- `ActionsResultJson` (nvarchar(max)): Detailed action results

## API Examples

### Create a Workflow

```http
POST /api/RemediationWorkflows
Content-Type: application/json

{
  "name": "Alert on Certificate Expiration",
  "description": "Log alert when certificates are expiring within 90 days",
  "isEnabled": true,
  "priority": 100,
  "trigger": {
    "certificateExpiringWithinDays": 90,
    "fleetIdMatches": "production"
  },
  "actions": [
    {
      "actionType": 3,
      "configurationJson": "{\"message\":\"Certificates expiring soon\"}",
      "order": 1
    }
  ],
  "createdBy": "admin@example.com"
}
```

### List Workflows

```http
GET /api/RemediationWorkflows
```

### Get Workflow Execution History

```http
GET /api/RemediationWorkflows/{workflowId}/executions?limit=50
```

## Integration Points

### Trigger Evaluation
The workflow engine is designed to be called after report ingestion. Currently, it must be invoked explicitly, but can be integrated into:
- Report ingestion pipeline
- Background service for periodic evaluation
- Manual trigger from UI

### Future Enhancements
- **Real-time Triggers**: Evaluate workflows immediately upon report ingestion
- **Scheduled Workflows**: Execute workflows on a schedule (daily, weekly)
- **Conditional Actions**: Support if/else logic in action execution
- **Action Templates**: Pre-defined action configurations
- **Workflow Testing**: Dry-run mode to test workflows without executing actions
- **Notification Channels**: SMS, Teams, Slack integrations
- **Advanced Triggers**: Complex boolean logic (AND/OR combinations)

## Troubleshooting

### Workflow Not Executing

1. Check if workflow is enabled (`IsEnabled = true`)
2. Verify trigger conditions match device state
3. Review execution history for error messages
4. Check application logs for WorkflowEngine errors

### Action Failures

1. Review `ActionsResultJson` in execution history
2. Validate action configuration JSON format
3. Check application logs for specific error details
4. Verify required services/permissions are available

### Performance Considerations

- Workflows are evaluated sequentially by priority
- Each workflow evaluation queries the database
- Consider batching evaluations for high-volume environments
- Use specific trigger conditions to reduce unnecessary evaluations

## Security Considerations

- Workflow actions execute with API service permissions
- Validate action configurations to prevent injection attacks
- Consider implementing workflow approval process
- Audit workflow creation and modifications
- Restrict workflow management to authorized users
- Sanitize user input in action configurations

## Testing

Unit tests are provided in `SecureBootDashboard.Api.Tests/Services/WorkflowEngineTests.cs`:
- `EvaluateAndExecuteAsync_NoWorkflows_ReturnsEmptyList`
- `EvaluateAndExecuteAsync_WorkflowWithLogAction_ExecutesSuccessfully`
- `EvaluateAndExecuteAsync_WorkflowWithNonMatchingTrigger_DoesNotExecute`
- `EvaluateAndExecuteAsync_DisabledWorkflow_DoesNotExecute`

Run tests with:
```bash
dotnet test SecureBootDashboard.Api.Tests/
```

## Migration

To apply the database migration:
```bash
cd SecureBootDashboard.Api
dotnet ef database update
```

## Support

For issues or questions:
- GitHub Issues: [Report bugs or request features](https://github.com/robgrame/Nimbus.BootCertWatcher/issues)
- Documentation: See `/docs` directory for additional guides

## License

This feature is part of the Secure Boot Certificate Watcher project and follows the MIT License.
