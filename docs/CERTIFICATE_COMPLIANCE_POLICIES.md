# Certificate Compliance Policies

## Overview

The Certificate Compliance Policies feature allows administrators to define and enforce compliance rules for Secure Boot certificates across their device fleet. This feature provides automated policy evaluation, violation detection, and compliance reporting.

## Features

### Policy Management
- **Create, update, and delete policies** through an intuitive web interface
- **Enable/disable policies** without deleting them
- **Priority-based evaluation** for multiple policies
- **Fleet-scoped policies** to apply rules to specific device groups
- **Multiple rules per policy** for comprehensive compliance checks

### Policy Rules

The system supports 8 types of compliance rules:

1. **Minimum Key Size**: Enforce minimum RSA/ECDSA key sizes (e.g., 2048 bits)
2. **Allowed Signature Algorithms**: Whitelist approved signature algorithms
3. **Disallowed Signature Algorithms**: Blacklist deprecated or insecure algorithms
4. **Maximum Certificate Age**: Limit how old certificates can be from their NotBefore date
5. **Minimum Days Until Expiration**: Warn when certificates are approaching expiration
6. **Require Microsoft Certificate**: Enforce Microsoft-issued certificates only
7. **Disallow Expired Certificates**: Flag devices with expired certificates
8. **Require Subject Pattern**: Enforce certificate subject naming conventions (regex)

### Severity Levels

Each rule can be assigned one of three severity levels:

- **Info**: Informational notices for awareness
- **Warning**: Issues that should be reviewed but don't block compliance
- **Critical**: Serious violations that make a device non-compliant

### Compliance Status

Devices are evaluated against all active policies and assigned one of four statuses:

- **Compliant**: All policies passed
- **Warning**: One or more warning-level violations detected
- **Non-Compliant**: One or more critical violations detected
- **Unknown**: Unable to determine status (e.g., no certificate data available)

## User Interface

### Policy List Page

Navigate to **Policies** in the main menu to access the policy management interface.

**Features:**
- View all policies with status, priority, and rule count
- Create new policies with the "Create Policy" button
- Edit existing policies by clicking the edit icon
- Delete policies with confirmation dialog
- See which policies are enabled or disabled

### Policy Creation Page

Click "Create Policy" to define a new compliance policy.

**Fields:**
- **Policy Name** (required): A descriptive name for the policy
- **Description** (optional): Additional details about the policy's purpose
- **Priority** (required): Evaluation order (lower = higher priority)
- **Fleet ID** (optional): Scope policy to specific fleet (blank = all fleets)
- **Policy Enabled**: Toggle to enable/disable the policy

**Rule Builder:**
- Click "Add Rule" to add compliance rules
- Select rule type from dropdown
- Set severity level (Info, Warning, Critical)
- Enter rule value (e.g., "2048" for minimum key size)
- Optionally filter by database (db, dbx, KEK, PK)
- Remove rules with the trash icon

### Device Compliance View

Compliance status is displayed on the Device Details page.

**Information shown:**
- Overall compliance status with color-coded icon
- List of policy violations (if any)
- Violation severity, policy name, and message
- Certificate details (thumbprint and database)
- Evaluation timestamp
- Link to manage policies

## API Endpoints

### Policy Management

```
GET    /api/Policies           - List all policies
GET    /api/Policies/{id}      - Get specific policy
POST   /api/Policies           - Create new policy
PUT    /api/Policies/{id}      - Update existing policy
DELETE /api/Policies/{id}      - Delete policy
```

### Compliance Evaluation

```
GET    /api/Compliance/devices/{id}  - Evaluate single device
GET    /api/Compliance/devices       - Evaluate all devices
GET    /api/Compliance/summary       - Get compliance statistics
```

## Example Policies

### Example 1: Basic Security Policy

**Policy Name**: Basic Certificate Security
**Description**: Enforce minimum security standards for all certificates
**Priority**: 100
**Fleet ID**: (blank - applies to all fleets)
**Enabled**: Yes

**Rules:**
1. **Minimum Key Size**: 2048, Severity: Critical
2. **Disallow Expired Certificates**: true, Severity: Critical
3. **Disallowed Signature Algorithms**: "MD5,SHA1", Severity: Critical

### Example 2: Microsoft-Only Policy

**Policy Name**: Microsoft Certificates Required
**Description**: Require Microsoft-issued certificates in signature database
**Priority**: 200
**Fleet ID**: production
**Enabled**: Yes

**Rules:**
1. **Require Microsoft Certificate**: true, Severity: Warning, Database: db

### Example 3: Expiration Warning Policy

**Policy Name**: Certificate Expiration Warnings
**Description**: Alert when certificates are approaching expiration
**Priority**: 300
**Fleet ID**: (blank)
**Enabled**: Yes

**Rules:**
1. **Minimum Days Until Expiration**: 90, Severity: Warning

## Implementation Details

### Database Schema

Policies are stored in the `Policies` table:

```sql
CREATE TABLE Policies (
    Id uniqueidentifier PRIMARY KEY,
    Name nvarchar(256) NOT NULL,
    Description nvarchar(2000),
    IsEnabled bit NOT NULL,
    Priority int NOT NULL,
    FleetId nvarchar(128),
    RulesJson nvarchar(max) NOT NULL,
    CreatedAtUtc datetimeoffset NOT NULL,
    ModifiedAtUtc datetimeoffset
);

CREATE INDEX IX_Policies_IsEnabled ON Policies(IsEnabled);
CREATE INDEX IX_Policies_FleetId ON Policies(FleetId);
```

### Policy Evaluation Logic

1. Load all enabled policies from the database
2. Filter policies by fleet ID (if specified)
3. Sort by priority (ascending)
4. For each policy:
   - Evaluate each rule against device certificates
   - Record violations with policy ID, rule, and details
5. Determine overall status based on highest severity violation
6. Return compliance result with violations list

### Performance Considerations

- Policies are loaded from database only when needed
- Evaluation is performed in-memory for speed
- Results can be cached if needed
- Use pagination for large device fleets

## Best Practices

1. **Start with warnings**: Begin with Warning severity to understand impact before enforcing Critical rules
2. **Test policies**: Create policies with specific fleet IDs first to test before rolling out globally
3. **Document policies**: Use clear names and descriptions to explain policy purpose
4. **Monitor compliance**: Regularly review compliance summary to identify trends
5. **Update policies**: Adjust thresholds and rules as your security posture evolves
6. **Use priority wisely**: Higher priority policies override lower priority ones for the same rule type

## Troubleshooting

### No compliance data shown

**Possible causes:**
- No policies have been created yet
- All policies are disabled
- Device has no certificate data in latest report
- API connectivity issues

**Solutions:**
- Create and enable at least one policy
- Verify device has recent report with certificate data
- Check API logs for errors

### Unexpected violations

**Possible causes:**
- Policy rules may be too strict
- Certificate data may be incomplete
- Fleet ID mismatch

**Solutions:**
- Review policy rules and thresholds
- Check device certificate enumeration logs
- Verify fleet ID matches between device and policy

## Future Enhancements

Potential future improvements:

- Automated remediation workflows
- Email/webhook notifications for violations
- Compliance history and trending
- Policy templates and presets
- Bulk policy operations
- Export compliance reports to CSV/Excel

## Related Documentation

- [Certificate Enumeration Guide](CERTIFICATE_ENUMERATION.md) - Learn about certificate data collection
- [Deployment Guide](DEPLOYMENT.md) - Production deployment instructions
- [Architecture Diagram](ARCHITECTURE_DIAGRAM.md) - System architecture overview
- [API Implementation](WEB_IMPLEMENTATION_SUMMARY.md) - Web dashboard and API details
