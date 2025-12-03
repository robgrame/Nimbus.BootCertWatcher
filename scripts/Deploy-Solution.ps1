<#
.SYNOPSIS
    Deploys SecureBootDashboard API and Web projects using their publishing profiles.

.DESCRIPTION
    This script automates the deployment of both SecureBootDashboard.Api and 
    SecureBootDashboard.Web projects to their configured network locations using
    the FolderProfile publishing profiles. It can optionally stop/start IIS 
    Application Pools during deployment.

.PARAMETER Configuration
    The build configuration to use (Debug or Release). Default is Release.

.PARAMETER ProjectFilter
    Deploy only specific project: 'Api', 'Web', or 'All'. Default is All.

.PARAMETER Clean
    Performs a clean build before publishing.

.PARAMETER Verbose
    Shows detailed output from dotnet publish command.

.PARAMETER ManageIIS
    If specified, stops IIS Application Pools before deployment and starts them after.

.PARAMETER IISServer
    The IIS server name where application pools are running. Default is 'srvcm00'.

.PARAMETER ApiAppPool
    Name of the API Application Pool. Default is 'SecureBootDashboard.Api'.

.PARAMETER WebAppPool
    Name of the Web Application Pool. Default is 'SecureBootDashboard.Web'.

.EXAMPLE
    .\Deploy-Solution.ps1
    Deploys both projects in Release configuration without managing IIS.

.EXAMPLE
    .\Deploy-Solution.ps1 -ManageIIS
    Deploys both projects and manages IIS Application Pools on default server.

.EXAMPLE
    .\Deploy-Solution.ps1 -ManageIIS -IISServer "MyServer" -ApiAppPool "MyApiPool"
    Deploys with custom IIS server and application pool names.

.EXAMPLE
    .\Deploy-Solution.ps1 -Configuration Debug -ProjectFilter Api -Clean -ManageIIS
    Deploys only API with clean build and IIS management.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [ValidateSet('Api', 'Web', 'All')]
    [string]$ProjectFilter = 'All',

    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [switch]$VerboseMode,

    [Parameter()]
    [switch]$ManageIIS,

    [Parameter()]
    [string]$IISServer = 'srvcm00',

    [Parameter()]
    [string]$ApiAppPool = 'SecureBootDashboard.Api',

    [Parameter()]
    [string]$WebAppPool = 'SecureBootDashboard.Web'
)

# Configuration
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

$projects = @(
    @{
        Name = 'SecureBootDashboard.Api'
        Path = 'SecureBootDashboard.Api\SecureBootDashboard.Api.csproj'
        Profile = 'FolderProfile'
        Destination = '\\srvcm00\r$\Nimbus.SecureBootCert\SecureBootDashboard.Api'
        Filter = 'Api'
        AppPool = $ApiAppPool
    },
    @{
        Name = 'SecureBootDashboard.Web'
        Path = 'SecureBootDashboard.Web\SecureBootDashboard.Web.csproj'
        Profile = 'FolderProfile'
        Destination = '\\Srvcm00\r$\Nimbus.SecureBootCert\SecureBootDashboard.Web'
        Filter = 'Web'
        AppPool = $WebAppPool
    }
)

# Functions
function Write-Header {
    param([string]$Message)
    Write-Host "`n$('=' * 80)" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "$('=' * 80)" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n► $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor DarkYellow
}

function Test-ProjectFile {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        Write-Error "Project file not found: $Path"
        return $false
    }
    return $true
}

function Test-IISManagement {
    Write-Step "Checking IIS Management capabilities..."
    
    try {
        # Check if WebAdministration module is available
        if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
            Write-Warning "WebAdministration module not available. IIS management will be performed using Invoke-Command."
            return $false
        }
        
        Import-Module WebAdministration -ErrorAction Stop
        Write-Success "WebAdministration module loaded successfully"
        return $true
    } catch {
        Write-Warning "Could not load WebAdministration module: $_"
        return $false
    }
}

function Get-AppPoolState {
    param(
        [string]$ServerName,
        [string]$AppPoolName
    )
    
    try {
        $scriptBlock = {
            param($poolName)
            Import-Module WebAdministration -ErrorAction Stop
            $pool = Get-Item "IIS:\AppPools\$poolName" -ErrorAction Stop
            return $pool.State
        }
        
        if ($ServerName -eq $env:COMPUTERNAME -or $ServerName -eq 'localhost') {
            $state = & $scriptBlock -poolName $AppPoolName
        } else {
            $state = Invoke-Command -ComputerName $ServerName -ScriptBlock $scriptBlock -ArgumentList $AppPoolName -ErrorAction Stop
        }
        
        return $state
    } catch {
        Write-Warning "Could not get state for Application Pool '$AppPoolName' on '$ServerName': $_"
        return $null
    }
}

function Stop-AppPool {
    param(
        [string]$ServerName,
        [string]$AppPoolName
    )
    
    Write-Step "Stopping Application Pool '$AppPoolName' on '$ServerName'..."
    
    try {
        $currentState = Get-AppPoolState -ServerName $ServerName -AppPoolName $AppPoolName
        
        if ($null -eq $currentState) {
            Write-Warning "Application Pool '$AppPoolName' not found or not accessible"
            return $false
        }
        
        if ($currentState -eq 'Stopped') {
            Write-Host "   Application Pool is already stopped" -ForegroundColor Gray
            return $true
        }
        
        $scriptBlock = {
            param($poolName)
            Import-Module WebAdministration -ErrorAction Stop
            Stop-WebAppPool -Name $poolName -ErrorAction Stop
            
            # Wait for pool to stop (max 30 seconds)
            $timeout = 30
            $elapsed = 0
            while ($elapsed -lt $timeout) {
                $pool = Get-Item "IIS:\AppPools\$poolName"
                if ($pool.State -eq 'Stopped') {
                    return $true
                }
                Start-Sleep -Seconds 1
                $elapsed++
            }
            return $false
        }
        
        if ($ServerName -eq $env:COMPUTERNAME -or $ServerName -eq 'localhost') {
            $result = & $scriptBlock -poolName $AppPoolName
        } else {
            $result = Invoke-Command -ComputerName $ServerName -ScriptBlock $scriptBlock -ArgumentList $AppPoolName -ErrorAction Stop
        }
        
        if ($result) {
            Write-Success "Application Pool '$AppPoolName' stopped successfully"
            return $true
        } else {
            Write-Warning "Application Pool '$AppPoolName' did not stop within timeout"
            return $false
        }
    } catch {
        Write-Error "Failed to stop Application Pool '$AppPoolName': $_"
        return $false
    }
}

function Start-AppPool {
    param(
        [string]$ServerName,
        [string]$AppPoolName
    )
    
    Write-Step "Starting Application Pool '$AppPoolName' on '$ServerName'..."
    
    try {
        $currentState = Get-AppPoolState -ServerName $ServerName -AppPoolName $AppPoolName
        
        if ($null -eq $currentState) {
            Write-Warning "Application Pool '$AppPoolName' not found or not accessible"
            return $false
        }
        
        if ($currentState -eq 'Started') {
            Write-Host "   Application Pool is already started" -ForegroundColor Gray
            return $true
        }
        
        $scriptBlock = {
            param($poolName)
            Import-Module WebAdministration -ErrorAction Stop
            Start-WebAppPool -Name $poolName -ErrorAction Stop
            
            # Wait for pool to start (max 30 seconds)
            $timeout = 30
            $elapsed = 0
            while ($elapsed -lt $timeout) {
                $pool = Get-Item "IIS:\AppPools\$poolName"
                if ($pool.State -eq 'Started') {
                    return $true
                }
                Start-Sleep -Seconds 1
                $elapsed++
            }
            return $false
        }
        
        if ($ServerName -eq $env:COMPUTERNAME -or $ServerName -eq 'localhost') {
            $result = & $scriptBlock -poolName $AppPoolName
        } else {
            $result = Invoke-Command -ComputerName $ServerName -ScriptBlock $scriptBlock -ArgumentList $AppPoolName -ErrorAction Stop
        }
        
        if ($result) {
            Write-Success "Application Pool '$AppPoolName' started successfully"
            return $true
        } else {
            Write-Warning "Application Pool '$AppPoolName' did not start within timeout"
            return $false
        }
    } catch {
        Write-Error "Failed to start Application Pool '$AppPoolName': $_"
        return $false
    }
}

function Invoke-CleanBuild {
    param([string]$ProjectPath, [string]$ProjectName)
    
    Write-Step "Cleaning $ProjectName..."
    
    $cleanArgs = @(
        'clean',
        $ProjectPath,
        '-c', $Configuration
    )
    
    $result = & dotnet @cleanArgs 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Clean completed for $ProjectName"
        return $true
    } else {
        Write-Error "Clean failed for $ProjectName"
        Write-Host $result -ForegroundColor Red
        return $false
    }
}

function Invoke-ProjectPublish {
    param(
        [string]$ProjectPath,
        [string]$ProjectName,
        [string]$ProfileName,
        [string]$Destination
    )
    
    Write-Step "Publishing $ProjectName..."
    Write-Host "   Configuration: $Configuration" -ForegroundColor Gray
    Write-Host "   Profile: $ProfileName" -ForegroundColor Gray
    Write-Host "   Destination: $Destination" -ForegroundColor Gray
    
    $publishArgs = @(
        'publish',
        $ProjectPath,
        "/p:PublishProfile=$ProfileName",
        "/p:Configuration=$Configuration"
    )
    
    if ($VerboseMode) {
        $publishArgs += '-v', 'detailed'
    }
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $result = & dotnet @publishArgs 2>&1
    $stopwatch.Stop()
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "$ProjectName published successfully in $($stopwatch.Elapsed.TotalSeconds.ToString('F2'))s"
        Write-Host "   Published to: $Destination" -ForegroundColor Gray
        return $true
    } else {
        Write-Error "$ProjectName publish failed"
        Write-Host $result -ForegroundColor Red
        return $false
    }
}

# Main execution
try {
    Write-Header "SecureBootDashboard Deployment"
    
    # Verify we're in the correct directory
    if (-not (Test-Path "SecureBootWatcher.sln")) {
        Write-Error "Solution file not found. Please run this script from the solution root directory."
        exit 1
    }
    
    # Filter projects
    $projectsToDeploy = $projects | Where-Object { 
        $ProjectFilter -eq 'All' -or $_.Filter -eq $ProjectFilter 
    }
    
    Write-Host "`nDeployment Configuration:" -ForegroundColor Cyan
    Write-Host "   Build Configuration: $Configuration" -ForegroundColor White
    Write-Host "   Projects to Deploy: $($projectsToDeploy.Count)" -ForegroundColor White
    Write-Host "   Clean Build: $Clean" -ForegroundColor White
    Write-Host "   Manage IIS: $ManageIIS" -ForegroundColor White
    if ($ManageIIS) {
        Write-Host "   IIS Server: $IISServer" -ForegroundColor White
    }
    
    # Verify IIS management if requested
    $canManageIIS = $false
    if ($ManageIIS) {
        Write-Header "IIS Management Check"
        $canManageIIS = Test-IISManagement
        
        if (-not $canManageIIS) {
            Write-Warning "IIS management features may be limited. Continuing with deployment..."
        }
    }
    
    # Stop Application Pools
    $stoppedPools = @()
    if ($ManageIIS) {
        Write-Header "Stopping IIS Application Pools"
        
        foreach ($project in $projectsToDeploy) {
            if (Stop-AppPool -ServerName $IISServer -AppPoolName $project.AppPool) {
                $stoppedPools += $project.AppPool
            }
        }
        
        # Wait a moment for file locks to release
        if ($stoppedPools.Count -gt 0) {
            Write-Host "`n   Waiting 3 seconds for file locks to release..." -ForegroundColor Gray
            Start-Sleep -Seconds 3
        }
    }
    
    # Deployment
    Write-Header "Building and Publishing Projects"
    
    $totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $successCount = 0
    $failCount = 0
    
    foreach ($project in $projectsToDeploy) {
        Write-Header "Deploying $($project.Name)"
        
        # Verify project file exists
        if (-not (Test-ProjectFile -Path $project.Path)) {
            $failCount++
            continue
        }
        
        # Clean if requested
        if ($Clean) {
            if (-not (Invoke-CleanBuild -ProjectPath $project.Path -ProjectName $project.Name)) {
                $failCount++
                continue
            }
        }
        
        # Publish
        if (Invoke-ProjectPublish -ProjectPath $project.Path `
                                  -ProjectName $project.Name `
                                  -ProfileName $project.Profile `
                                  -Destination $project.Destination) {
            $successCount++
        } else {
            $failCount++
        }
    }
    
    $totalStopwatch.Stop()
    
    # Start Application Pools
    if ($ManageIIS -and $stoppedPools.Count -gt 0) {
        Write-Header "Starting IIS Application Pools"
        
        foreach ($project in $projectsToDeploy) {
            if ($stoppedPools -contains $project.AppPool) {
                Start-AppPool -ServerName $IISServer -AppPoolName $project.AppPool | Out-Null
            }
        }
    }
    
    # Summary
    Write-Header "Deployment Summary"
    Write-Host "`nResults:" -ForegroundColor Cyan
    Write-Host "   Total Projects: $($projectsToDeploy.Count)" -ForegroundColor White
    Write-Host "   Successful: $successCount" -ForegroundColor Green
    Write-Host "   Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { 'Red' } else { 'White' })
    Write-Host "   Total Time: $($totalStopwatch.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor White
    
    if ($ManageIIS) {
        Write-Host "`nIIS Management:" -ForegroundColor Cyan
        Write-Host "   Application Pools Managed: $($stoppedPools.Count)" -ForegroundColor White
        foreach ($pool in $stoppedPools) {
            $state = Get-AppPoolState -ServerName $IISServer -AppPoolName $pool
            $color = if ($state -eq 'Started') { 'Green' } else { 'Red' }
            Write-Host "   - $pool`: $state" -ForegroundColor $color
        }
    }
    
    if ($failCount -eq 0) {
        Write-Host "`n✓ All deployments completed successfully!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "`n✗ Some deployments failed. Please check the output above." -ForegroundColor Red
        exit 1
    }
    
} catch {
    Write-Error "An unexpected error occurred: $_"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    
    # Try to restart pools in case of error
    if ($ManageIIS -and $stoppedPools.Count -gt 0) {
        Write-Header "Emergency: Restarting Application Pools"
        foreach ($poolName in $stoppedPools) {
            try {
                Start-AppPool -ServerName $IISServer -AppPoolName $poolName | Out-Null
            } catch {
                Write-Warning "Could not restart pool '$poolName': $_"
            }
        }
    }
    
    exit 1
}
