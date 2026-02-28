# EMERGENCY: Stop Infinite Loop - Deploy-ApiServer.ps1

## If Script Is Still Running in Infinite Loop

### IMMEDIATE ACTION - Press `Ctrl+C` to kill the script

If that doesn't work, follow these steps:

## Step 1: Kill PowerShell Process

```powershell
# In a NEW PowerShell window (Run as Administrator):

# Find PowerShell processes
Get-Process powershell* | Select-Object Id, ProcessName, StartTime, CPU

# Kill the specific PowerShell process running the script
# Replace <PID> with the Process ID from above
Stop-Process -Id <PID> -Force

# OR kill all PowerShell processes (WARNING: closes all PowerShell windows)
Get-Process powershell* | Stop-Process -Force
```

## Step 2: Clean Up Global Variable

```powershell
# Remove the guard variable
Remove-Variable -Name DeployApiServerRunning -Scope Global -ErrorAction SilentlyContinue
```

## Step 3: Check IIS State

```powershell
# Check if app pool and website exist and their states
Get-WebAppPool "SecureBootDashboard.Api" -ErrorAction SilentlyContinue
Get-Website "SecureBootDashboard.Api" -ErrorAction SilentlyContinue

# Stop them if needed
Stop-WebAppPool "SecureBootDashboard.Api" -ErrorAction SilentlyContinue
Stop-Website "SecureBootDashboard.Api" -ErrorAction SilentlyContinue
```

## Step 4: Investigate Root Cause

The infinite loop might be caused by:

### Possibility #1: Script Profile Auto-Execution
Check if your PowerShell profile is executing the script:
```powershell
# Check profile path
$PROFILE

# View profile contents
Get-Content $PROFILE -ErrorAction SilentlyContinue

# Temporarily rename profile to disable
Rename-Item $PROFILE "$PROFILE.backup" -ErrorAction SilentlyContinue
```

### Possibility #2: Scheduled Task or Service
Check if the script is being run by a scheduled task:
```powershell
Get-ScheduledTask | Where-Object {$_.Actions.Execute -like "*Deploy-ApiServer*"}
```

### Possibility #3: File Watcher or Auto-Deployment Tool
Check if any file watcher or deployment tool is triggering the script.

### Possibility #4: Start-WebAppPool Triggering Re-execution
The `Start-WebAppPool` cmdlet might be configured to run a script on start.

Check application pool settings:
```powershell
# In IIS Manager:
# Application Pools ? SecureBootDashboard.Api ? Advanced Settings
# Check "Start Mode" and any startup scripts
```

## Step 5: Safe Re-Execution

Before running the script again:

1. **Verify you have the latest version** with guards:
   ```powershell
   # Check for guard variable at top of script
   Get-Content scripts\Deploy-ApiServer.ps1 -Head 70 | Select-String "DeployApiServerRunning"
   ```

2. **Run with verbose output to see where it loops**:
   ```powershell
   $VerbosePreference = "Continue"
   .\scripts\Deploy-ApiServer.ps1 -Verbose
   ```

3. **Use WhatIf mode first**:
   ```powershell
   .\scripts\Deploy-ApiServer.ps1 -WhatIf
   ```

## Diagnostic: Check What's Calling the Script

```powershell
# Check current call stack
Get-PSCallStack

# Check if script is in a loop
$Error[0] | Format-List * -Force

# Check event logs for clues
Get-EventLog -LogName Application -Newest 50 | 
    Where-Object {$_.Message -like "*Deploy-ApiServer*"}
```

## Alternative: Manual IIS Configuration

If the script continues to fail, configure manually:

### Manual Steps:
1. Open IIS Manager
2. Create Application Pool:
   - Name: SecureBootDashboard.Api
   - .NET CLR Version: No Managed Code
   - Identity: ApplicationPoolIdentity
3. Create Website:
   - Name: SecureBootDashboard.Api
   - Physical Path: C:\inetpub\SecureBootDashboard.Api
   - Binding: https, port 443, hostname api.yourdomain.com
   - App Pool: SecureBootDashboard.Api
4. Bind SSL Certificate:
   - Right-click website ? Edit Bindings
   - Select HTTPS binding ? Edit
   - Choose your SSL certificate
5. Start Application Pool and Website

## Contact Information

If issue persists:
1. Check `$Error[0]` for detailed error
2. Review all output carefully for patterns
3. Check if script is somehow calling itself
4. Consider debugging with:
   ```powershell
   Set-PSDebug -Trace 2
   ```

## Version Check

Ensure you have the latest fixed version:
- Should have `$global:DeployApiServerRunning` guard at top
- Should have `$script:WebSiteStartAttempted` guard in Start-WebSite function
- Version should be 1.2 or higher

