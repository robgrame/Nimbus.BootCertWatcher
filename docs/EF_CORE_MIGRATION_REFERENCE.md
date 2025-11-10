# Entity Framework Core Migration Quick Reference

## Common Commands

### Prerequisites

```powershell
# Install EF Core tools (one-time setup)
dotnet tool install --global dotnet-ef

# Update EF Core tools
dotnet tool update --global dotnet-ef

# Verify installation
dotnet ef --version
```

---

## Creating Migrations

### 1. Add New Migration

```powershell
# Navigate to project directory
cd SecureBootDashboard.Api

# Create migration
dotnet ef migrations add MigrationName

# Create migration with startup project (if different)
dotnet ef migrations add MigrationName --startup-project ../SecureBootDashboard.Web
```

**Naming conventions:**
- `AddColumnNameToTable` - e.g., `AddClientVersionToDevice`
- `CreateTableName` - e.g., `CreateAuditLog`
- `UpdateTableNameStructure` - e.g., `UpdateDeviceStructure`

### 2. Remove Last Migration (if not applied)

```powershell
# Remove last migration (only if not applied to database)
dotnet ef migrations remove

# Force remove (use with caution)
dotnet ef migrations remove --force
```

?? **Warning**: Only remove migrations that haven't been applied to the database!

---

## Applying Migrations

### 1. Apply All Pending Migrations

```powershell
# Apply to database specified in appsettings.json
dotnet ef database update

# Apply with custom connection string
dotnet ef database update --connection "Server=...;Database=...;Trusted_Connection=True"
```

### 2. Apply to Specific Migration

```powershell
# Apply up to specific migration
dotnet ef database update MigrationName

# Rollback to specific migration
dotnet ef database update PreviousMigrationName
```

### 3. Rollback All Migrations

```powershell
# Remove all migrations from database (keeps data)
dotnet ef database update 0
```

?? **Warning**: This will drop all tables!

---

## Inspecting Migrations

### 1. List All Migrations

```powershell
dotnet ef migrations list
```

**Output:**
```
20251105093532_InitialCreate (Applied)
20251105101836_AddCertificateCollection (Applied)
20251109202217_AddClientVersionToDevice (Pending)
```

### 2. View Migration SQL

```powershell
# Generate SQL for next migration
dotnet ef migrations script

# Generate SQL for specific migration
dotnet ef migrations script PreviousMigration TargetMigration

# Generate SQL for all pending migrations
dotnet ef migrations script --idempotent

# Output to file
dotnet ef migrations script --output migration.sql
```

### 3. Check Database Schema

```powershell
# Generate SQL to create database from scratch
dotnet ef dbcontext script

# Output to file
dotnet ef dbcontext script --output schema.sql
```

---

## Database Operations

### 1. Drop Database

```powershell
# Drop database (all data lost!)
dotnet ef database drop

# Drop without confirmation
dotnet ef database drop --force
```

### 2. Rebuild Database

```powershell
# Drop and recreate
dotnet ef database drop --force
dotnet ef database update
```

---

## Troubleshooting Commands

### 1. Verify DbContext

```powershell
# List all DbContexts in project
dotnet ef dbcontext list

# View DbContext info
dotnet ef dbcontext info
```

### 2. Generate Migration Bundle

```powershell
# Create self-contained migration executable
dotnet ef migrations bundle

# Specify output
dotnet ef migrations bundle --output migrations.exe

# Run bundle
.\migrations.exe --connection "Server=...;Database=...;Trusted_Connection=True"
```

### 3. Debug Build Issues

```powershell
# Clean build
dotnet clean
dotnet build

# Verbose output
dotnet ef database update --verbose
```

---

## Configuration Files

### appsettings.json

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=SecureBootDashboard_Dev;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

---

## Migration File Structure

### Example Migration

```csharp
public partial class AddClientVersionToDevice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClientVersion",
            table: "Devices",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClientVersion",
            table: "Devices");
    }
}
```

---

## Common Scenarios

### Scenario 1: Add New Column

```powershell
# 1. Update entity model (DeviceEntity.cs)
# 2. Create migration
dotnet ef migrations add AddNewColumnToDevice

# 3. Review generated migration
code Data/Migrations/*_AddNewColumnToDevice.cs

# 4. Apply migration
dotnet ef database update
```

### Scenario 2: Rename Column

```powershell
# 1. Update entity model
# 2. Create migration
dotnet ef migrations add RenameOldColumnToNewColumn

# 3. Edit migration to use RenameColumn (not Add/Drop)
# 4. Apply migration
dotnet ef database update
```

**Manual edit example:**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.RenameColumn(
        name: "OldName",
        table: "Devices",
        newName: "NewName");
}
```

### Scenario 3: Change Column Type

```powershell
# 1. Update entity model
# 2. Create migration
dotnet ef migrations add ChangeColumnTypeForDevice

# 3. Apply migration (may require data migration)
dotnet ef database update
```

### Scenario 4: Fix Broken Migration

```powershell
# Option 1: Remove last migration (if not applied)
dotnet ef migrations remove

# Option 2: Rollback to previous migration
dotnet ef database update PreviousMigrationName

# Option 3: Create new migration to fix
dotnet ef migrations add FixBrokenMigration
```

---

## Best Practices

### ? DO

1. **Always review generated migrations** before applying
2. **Test migrations on development database** first
3. **Use meaningful migration names**
4. **Commit migrations to source control**
5. **Generate SQL scripts** for production deployments
6. **Back up database** before major migrations
7. **Use idempotent scripts** for production

### ? DON'T

1. **Don't remove applied migrations** from source control
2. **Don't modify applied migrations** (create new one instead)
3. **Don't skip testing** migrations
4. **Don't apply migrations directly** to production (use scripts)
5. **Don't forget to update** model snapshot
6. **Don't use `database drop`** in production

---

## Production Deployment Workflow

### Step 1: Generate SQL Script

```powershell
# Development environment
dotnet ef migrations script --idempotent --output migration.sql
```

### Step 2: Review Script

```powershell
# Review SQL before applying
code migration.sql
```

### Step 3: Backup Production Database

```sql
-- SQL Server Management Studio
BACKUP DATABASE SecureBootDashboard
TO DISK = 'C:\Backups\SecureBootDashboard_PreMigration.bak'
WITH INIT, COMPRESSION;
```

### Step 4: Apply Script

```sql
-- Execute migration.sql in SSMS
-- Or use sqlcmd:
sqlcmd -S SRVSQL -d SecureBootDashboard -i migration.sql
```

### Step 5: Verify

```sql
-- Check applied migrations
SELECT * FROM __EFMigrationsHistory
ORDER BY MigrationId DESC;
```

---

## Environment-Specific Migrations

### Development

```powershell
# Use Development connection string
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update
```

### Production

```powershell
# Use Production connection string
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef migrations script --idempotent --output production_migration.sql
```

---

## Automated Scripts

### Check and Apply Migrations

```powershell
# Use provided script
.\scripts\Apply-DatabaseMigrations.ps1 -Force
```

### Custom Automation

```powershell
# Check if migrations needed
$pendingMigrations = dotnet ef migrations list 2>&1 | Select-String "Pending"

if ($pendingMigrations) {
    Write-Host "Pending migrations found. Applying..."
    dotnet ef database update
} else {
    Write-Host "Database is up to date."
}
```

---

## Quick Troubleshooting

| Error | Solution |
|-------|----------|
| `dotnet ef: command not found` | `dotnet tool install --global dotnet-ef` |
| `Build failed` | `dotnet clean && dotnet build` |
| `Unable to create migration` | Check entity model changes |
| `Login failed` | Verify connection string |
| `Network-related error` | Check SQL Server service is running |
| `Duplicate migration` | Remove duplicate with `migrations remove` |
| `Column already exists` | Check if migration already applied |
| `Foreign key constraint` | Review migration order |

---

## Useful Queries

### Check Pending Migrations

```powershell
dotnet ef migrations list | Select-String "Pending"
```

### Check Applied Migrations

```sql
-- Direct database query
SELECT * FROM __EFMigrationsHistory
ORDER BY MigrationId DESC;
```

### Compare Model vs Database

```powershell
# Generate current model script
dotnet ef dbcontext script --output current_model.sql

# Compare with database
# Use SQL Server Schema Compare tool
```

---

## Related Resources

- **Official Documentation**: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- **Migration Tutorial**: https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app
- **Database Providers**: https://learn.microsoft.com/en-us/ef/core/providers/

---

**Quick Command Reference Card**

```powershell
# Create migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# List migrations
dotnet ef migrations list

# Generate SQL
dotnet ef migrations script --idempotent

# Remove last migration (if not applied)
dotnet ef migrations remove

# Drop database
dotnet ef database drop --force
```

---

**Last Updated**: 2025-01-10
