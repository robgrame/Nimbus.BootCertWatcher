# ===============================================================================
# Ultimate-Diagnosis.ps1
#
# Ultimate diagnostic script - check EVERYTHING
# ===============================================================================

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host "ULTIMATE DIAGNOSIS - Find the Real Problem" -ForegroundColor Red
Write-Host "===============================================================================" -ForegroundColor Red
Write-Host ""

$webPath = "C:\inetpub\SecureBootDashboard.Web"
$logsPath = "C:\Logs\SecureBootDashboard"

# 1. Check files exist
Write-Host "1. Checking files..." -ForegroundColor Yellow
$dll = Test-Path "$webPath\SecureBootDashboard.Web.dll"
$runtimeConfig = Test-Path "$webPath\SecureBootDashboard.Web.runtimeconfig.json"
$depsJson = Test-Path "$webPath\SecureBootDashboard.Web.deps.json"
$appsettings = Test-Path "$webPath\appsettings.json"
$appsettingsProd = Test-Path "$webPath\appsettings.Production.json"

Write-Host "  DLL: $dll" -ForegroundColor $(if ($dll) { "Green" } else { "Red" })
Write-Host "  runtimeconfig.json: $runtimeConfig" -ForegroundColor $(if ($runtimeConfig) { "Green" } else { "Red" })
Write-Host "  deps.json: $depsJson" -ForegroundColor $(if ($depsJson) { "Green" } else { "Red" })
Write-Host "  appsettings.json: $appsettings" -ForegroundColor $(if ($appsettings) { "Green" } else { "Red" })
Write-Host "  appsettings.Production.json: $appsettingsProd" -ForegroundColor $(if ($appsettingsProd) { "Green" } else { "Red" })

# 2. Check .NET runtime
Write-Host "`n2. Checking .NET 10 runtime..." -ForegroundColor Yellow
$runtimes = dotnet --list-runtimes | Select-String "10.0"
if ($runtimes) {
    Write-Host "? .NET 10 runtimes found:" -ForegroundColor Green
    $runtimes | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
} else {
    Write-Host "? .NET 10 runtime NOT FOUND!" -ForegroundColor Red
}

# 3. Check web.config
Write-Host "`n3. Checking web.config..." -ForegroundColor Yellow
$webConfigPath = "$webPath\web.config"
if (Test-Path $webConfigPath) {
    [xml]$webConfig = Get-Content $webConfigPath
    $aspNetCore = $webConfig.configuration.location.'system.webServer'.aspNetCore
    
    Write-Host "  processPath: $($aspNetCore.processPath)" -ForegroundColor Gray
    Write-Host "  arguments: $($aspNetCore.arguments)" -ForegroundColor Gray
    Write-Host "  hostingModel: $($aspNetCore.hostingModel)" -ForegroundColor $(if ($aspNetCore.hostingModel -eq "outofprocess") { "Green" } else { "Yellow" })
    Write-Host "  stdoutLogEnabled: $($aspNetCore.stdoutLogEnabled)" -ForegroundColor Gray
    Write-Host "  stdoutLogFile: $($aspNetCore.stdoutLogFile)" -ForegroundColor Gray
    Write-Host "  forwardWindowsAuthToken: $($aspNetCore.forwardWindowsAuthToken)" -ForegroundColor Gray
} else {
    Write-Host "? web.config not found!" -ForegroundColor Red
}

# 4. Check permissions
Write-Host "`n4. Checking permissions..." -ForegroundColor Yellow
$identity = "IIS AppPool\SecureBootDashboard.Web"

try {
    $acl = Get-Acl $webPath
    $hasPermission = $acl.Access | Where-Object {
        $_.IdentityReference -eq $identity -and
        ($_.FileSystemRights -match "Read|Execute")
    }
    
    if ($hasPermission) {
        Write-Host "? App Pool has read/execute permissions" -ForegroundColor Green
    } else {
        Write-Host "? App Pool does NOT have permissions!" -ForegroundColor Red
        Write-Host "  Granting permissions..." -ForegroundColor Yellow
        
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $identity, "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
        )
        $acl.AddAccessRule($rule)
        Set-Acl $webPath $acl
        Write-Host "  ? Permissions granted" -ForegroundColor Green
    }
} catch {
    Write-Host "  Could not check permissions: $_" -ForegroundColor Yellow
}

# 5. Check App Pool settings
Write-Host "`n5. Checking App Pool..." -ForegroundColor Yellow
Import-Module WebAdministration
$appPool = Get-Item "IIS:\AppPools\SecureBootDashboard.Web"

Write-Host "  .NET CLR Version: $($appPool.managedRuntimeVersion)" -ForegroundColor Gray
Write-Host "  Pipeline Mode: $($appPool.managedPipelineMode)" -ForegroundColor Gray
Write-Host "  Identity: $($appPool.processModel.identityType)" -ForegroundColor Gray
Write-Host "  Enable 32-bit: $($appPool.enable32BitAppOnWin64)" -ForegroundColor Gray
Write-Host "  Start Mode: $($appPool.startMode)" -ForegroundColor Gray

# 6. Check latest log
Write-Host "`n6. Checking LATEST log..." -ForegroundColor Yellow
$latestLog = Get-ChildItem $logsPath -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($latestLog) {
    $age = (Get-Date) - $latestLog.LastWriteTime
    Write-Host "  Latest log: $($latestLog.Name)" -ForegroundColor Cyan
    Write-Host "  Age: $([Math]::Round($age.TotalMinutes, 1)) minutes" -ForegroundColor Gray
    Write-Host "  Size: $($latestLog.Length) bytes" -ForegroundColor Gray
    
    Write-Host "`n  Last 30 lines:" -ForegroundColor Cyan
    Write-Host "  " + ("-" * 77) -ForegroundColor DarkGray
    Get-Content $latestLog.FullName -Tail 30 | ForEach-Object {
        $color = "Gray"
        if ($_ -match "\[FTL\]|\[ERR\]") { $color = "Red" }
        elseif ($_ -match "\[WRN\]") { $color = "Yellow" }
        Write-Host "  $_" -ForegroundColor $color
    }
    Write-Host "  " + ("-" * 77) -ForegroundColor DarkGray
} else {
    Write-Host "  ? NO LOG FILES FOUND!" -ForegroundColor Red
    Write-Host "  This means the app is NOT starting at all" -ForegroundColor Red
}

# 7. Check Event Viewer
Write-Host "`n7. Checking Event Viewer (last 3 events)..." -ForegroundColor Yellow
$events = Get-EventLog -LogName Application -Source "*AspNetCore*" -Newest 3 -ErrorAction SilentlyContinue
if ($events) {
    foreach ($event in $events) {
        Write-Host "`n  [$($event.TimeGenerated)] $($event.EntryType)" -ForegroundColor $(if ($event.EntryType -eq "Error") { "Red" } else { "Yellow" })
        Write-Host "  $($event.Message.Substring(0, [Math]::Min(200, $event.Message.Length)))..." -ForegroundColor Gray
    }
} else {
    Write-Host "  No recent ASP.NET Core events" -ForegroundColor Gray
}

# 8. Test direct execution
Write-Host "`n8. Testing direct execution..." -ForegroundColor Yellow
Write-Host "  Attempting to run: dotnet SecureBootDashboard.Web.dll" -ForegroundColor Gray

Push-Location $webPath
try {
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList "SecureBootDashboard.Web.dll" `
        -NoNewWindow `
        -PassThru `
        -RedirectStandardOutput "$env:TEMP\stdout.txt" `
        -RedirectStandardError "$env:TEMP\stderr.txt"
    
    Start-Sleep -Seconds 5
    
    if ($process.HasExited) {
        Write-Host "  ? Process exited immediately (Exit code: $($process.ExitCode))" -ForegroundColor Red
        
        if (Test-Path "$env:TEMP\stdout.txt") {
            $stdout = Get-Content "$env:TEMP\stdout.txt" -Raw
            if ($stdout) {
                Write-Host "`n  STDOUT:" -ForegroundColor Yellow
                Write-Host $stdout -ForegroundColor Gray
            }
        }
        
        if (Test-Path "$env:TEMP\stderr.txt") {
            $stderr = Get-Content "$env:TEMP\stderr.txt" -Raw
            if ($stderr) {
                Write-Host "`n  STDERR:" -ForegroundColor Red
                Write-Host $stderr -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "  ? Process is running (PID: $($process.Id))" -ForegroundColor Green
        $process.Kill()
    }
} catch {
    Write-Host "  ? Could not start process: $_" -ForegroundColor Red
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "DIAGNOSIS COMPLETE" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "ANALYSIS:" -ForegroundColor Yellow
if (-not $dll) {
    Write-Host "  • Missing DLL - need to redeploy" -ForegroundColor Red
}
if (-not $runtimes) {
    Write-Host "  • .NET 10 runtime missing - need to install" -ForegroundColor Red
}
if ($latestLog -and (Get-Content $latestLog.FullName -Tail 20 | Select-String "Negotiate Authentication handler")) {
    Write-Host "  • Windows Auth conflict STILL present!" -ForegroundColor Red
}
if (-not $latestLog) {
    Write-Host "  • No logs - app not starting at all" -ForegroundColor Red
}

Write-Host ""

