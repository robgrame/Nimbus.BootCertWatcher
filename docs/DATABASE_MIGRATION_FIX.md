# Database Migration Fix - ClientVersion Column Missing

## Problem

The application is throwing the following error:

```
Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid column name 'ClientVersion'.
```

This occurs because the `ClientVersion` column was added to the `DeviceEntity` model, but the database migration hasn't been applied to the SQL Server database.

## Root Cause

1. **Migration Created**: A migration file `20251109202217_AddClientVersionToDevice.cs` was created to add the `ClientVersion` column to the `Devices` table
2. **Migration Not Applied**: The migration exists in the codebase but hasn't been applied to the database
3. **Duplicate Migration**: There's also a duplicate migration `20251109202105_AddClientVersionToDevice.cs` that should be removed

## Quick Fix (Recommended)

### Option 1: Using PowerShell Script (Recommended)

Run the automated migration script:

```powershell
# From repository root
.\scripts\Apply-DatabaseMigrations.ps1 -Force
```

This script will:
- ? Check for dotnet-ef tools (install if missing)
- ? Remove duplicate migrations
- ? Apply all pending migrations
- ? Verify success

### Option 2: Manual Steps

If you prefer manual control:

#### Step 1: Install EF Core Tools (if not installed)

```powershell
dotnet tool install --global dotnet-ef
```

#### Step 2: Navigate to API Project

```powershell
cd SecureBootDashboard.Api
```

#### Step 3: Check Pending Migrations

```powershell
dotnet ef migrations list
```

**Expected output:**
```
Build succeeded.
20251105093532_InitialCreate (Applied)
20251105101836_AddCertificateCollection (Applied)
20251107164106_AddFirmwareReleaseDateToDevice (Applied)
20251107233431_AddUEFISecureBootEnabledToDevice (Applied)
20251109202105_AddClientVersionToDevice (Pending) <-- Duplicate
20251109202217_AddClientVersionToDevice (Pending) <-- Keep this one
```

#### Step 4: Remove Duplicate Migration (Optional but Recommended)

```powershell
# Delete duplicate migration files manually
Remove-Item "Data\Migrations\20251109202105_AddClientVersionToDevice.cs"
Remove-Item "Data\Migrations\20251109202105_AddClientVersionToDevice.Designer.cs"
```

#### Step 5: Apply Migration

```powershell
dotnet ef database update
```

**Expected output:**
```
Build succeeded.
Applying migration '20251109202217_AddClientVersionToDevice'.
Done.
```

#### Step 6: Verify Migration Applied

```powershell
dotnet ef migrations list
```

All migrations should now show `(Applied)`.

## Verify Fix

### 1. Check Database Schema

Connect to SQL Server and verify the column exists:

```sql
-- Check if ClientVersion column exists in Devices table
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Devices'
  AND COLUMN_NAME = 'ClientVersion';
```

**Expected result:**
```
COLUMN_NAME      DATA_TYPE    IS_NULLABLE
ClientVersion    nvarchar     YES
```

### 2. Test API Endpoint

Restart the API and test the `/api/Devices` endpoint:

```powershell
# Restart API
# Then test:
Invoke-RestMethod -Uri "https://localhost:5000/api/Devices" -Method Get
```

Should return device list without errors.

### 3. Check Dashboard

Navigate to the web dashboard:
- Homepage should load without errors
- Device list should display
- Client version should appear in device details

## Migration Details

### Migration File: `20251109202217_AddClientVersionToDevice.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>(
        name: "ClientVersion",
        table: "Devices",
        type: "nvarchar(max)",
        nullable: true);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "ClientVersion",
        table: "Devices");
}
```

**What it does:**
- Adds a new `ClientVersion` column to the `Devices` table
- Column type: `nvarchar(max)` (unlimited length string)
- Nullable: Yes (allows NULL values for existing records)

### Why Two Migrations Exist

The duplicate migration (`20251109202105`) was likely created first, but then another migration with the same name was generated. This can happen when:

1. Running `dotnet ef migrations add` multiple times
2. Migrations being generated on different machines
3. Git merge conflicts

**Best practice:** Remove the older duplicate to avoid confusion.

## Connection String

The migration uses the connection string from `appsettings.json`:

```json
"ConnectionStrings": {
  "SqlServer": "Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True"
}
```

### Using Custom Connection String

If you need to apply the migration to a different database:

```powershell
dotnet ef database update --connection "Server=YOUR_SERVER;Database=YOUR_DB;Trusted_Connection=True;TrustServerCertificate=True"
```

## Troubleshooting

### Issue: "dotnet ef: command not found"

**Solution:**
```powershell
dotnet tool install --global dotnet-ef
```

### Issue: "Build failed"

**Solution:**
```powershell
# Clean and rebuild
dotnet clean
dotnet build
dotnet ef database update
```

### Issue: "Unable to create migration"

**Solution:**
The migration already exists - just apply it:
```powershell
dotnet ef database update
```

### Issue: "A network-related or instance-specific error occurred"

**Causes:**
- SQL Server not running
- Incorrect server name in connection string
- Firewall blocking connection
- SQL Server service stopped

**Solution:**
```powershell
# Check SQL Server service
Get-Service -Name "MSSQL*"

# If stopped, start it:
Start-Service -Name "MSSQLSERVER"
```

### Issue: "Login failed for user"

**Causes:**
- Windows Authentication not enabled
- User doesn't have permissions

**Solution:**
Grant user permissions in SQL Server Management Studio or use SQL Authentication:
```json
"SqlServer": "Server=SRVSQL;Database=SecureBootDashboard;User Id=sa;Password=YourPassword;TrustServerCertificate=True"
```

## Preventing Future Issues

### 1. Always Apply Migrations After Creating Them

```powershell
# Create migration
dotnet ef migrations add MyMigrationName

# Immediately apply it
dotnet ef database update
```

### 2. Use CI/CD Pipeline

Add migration application to your deployment pipeline:

```yaml
# Example Azure DevOps task
- task: DotNetCoreCLI@2
  displayName: 'Apply EF Migrations'
  inputs:
    command: 'custom'
    custom: 'ef'
    arguments: 'database update --project SecureBootDashboard.Api'
```

### 3. Check Migration Status Before Deployment

```powershell
# List migrations and check for pending ones
dotnet ef migrations list
```

### 4. Use Database Health Checks

The API already includes health checks. Monitor them:

```
GET https://localhost:5000/health
```

## Related Documentation

- **Entity Framework Core Migrations**: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- **Client Version Tracking**: `docs\CLIENT_VERSION_TRACKING.md`
- **Database Schema**: `docs\DATABASE_SCHEMA.md` (if exists)

## Summary

? **Problem**: `ClientVersion` column missing from database  
? **Cause**: Migration not applied  
? **Solution**: Run `.\scripts\Apply-DatabaseMigrations.ps1 -Force`  
? **Verification**: Query database, test API, check dashboard  

After applying the migration, the application should work correctly with client version tracking enabled.

---

**Last Updated**: 2025-01-10  
**Related Issue**: Database schema out of sync with entity model
