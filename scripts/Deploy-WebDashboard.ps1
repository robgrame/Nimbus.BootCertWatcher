# ===============================================================================
# Deploy-WebDashboard.ps1
#
# Deploys SecureBootDashboard Web Dashboard to IIS
# - Creates Application Pool
# - Creates Website
# - Configures SSL/TLS
# - Sets up Mutual TLS (optional)
# - Configures logging
#
# Requirements:
# - Windows Server with IIS installed
# - .NET 10 Hosting Bundle installed
# - SSL certificate for HTTPS
# - Web Dashboard binaries published
#
# Usage:
#   .\Deploy-WebDashboard.ps1 -PhysicalPath "C:\inetpub\SecureBootDashboard.Web"
#
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SiteName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$AppPoolName = "SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$PhysicalPath = "C:\inetpub\SecureBootDashboard.Web",
    
    [Parameter(Mandatory = $false)]
    [string]$HostHeader = "dashboard.yourdomain.com",
    
    [Parameter(Mandatory = $false)]
    [int]$HttpsPort = 443,
    
    [Parameter(Mandatory = $false)]
    [int]$HttpPort = 80,
    
    [Parameter(Mandatory = $false)]
    [string]$SslCertificateThumbprint,
    
    [Parameter(Mandatory = $false)]
    [string]$SourcePath = ".\SecureBootDashboard.Web\bin\Release\net10.0\publish",
    
    [Parameter(Mandatory = $false)]
    [switch]$EnableHttpRedirect,
    
    [Parameter(Mandatory = $false)]
    [switch]$CreateHttpBinding,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# Set console encoding to UTF-8 for proper icon display
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# Guard against re-execution
if ($global:DeployWebDashboardRunning) {
    Write-Host "Script is already running in another instance" -ForegroundColor Red
    exit 1
}
$global:DeployWebDashboardRunning = $true

# ===============================================================================
# Functions
# ===============================================================================

function Assert-Admin {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host "This script must be run as Administrator" -ForegroundColor Red
        Write-Host "  Right-click PowerShell and choose 'Run as administrator'" -ForegroundColor Yellow
        throw "Administrator privileges required"
    }
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "$Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

function Test-Prerequisites {
    Write-Step "Checking prerequisites"
    
    # Check if IIS is installed
    $iisFeature = Get-WindowsFeature -Name Web-Server -ErrorAction SilentlyContinue
    if (-not $iisFeature -or -not $iisFeature.Installed) {
        Write-Host "IIS is not installed" -ForegroundColor Red
        Write-Host "  Run: Install-WindowsFeature -Name Web-Server -IncludeManagementTools" -ForegroundColor Yellow
        throw "IIS not installed"
    }
    Write-Success "IIS is installed"
    
    # Check if WebAdministration or IISAdministration module is available
    $webAdminModule = Get-Module -ListAvailable -Name WebAdministration
    $iisAdminModule = Get-Module -ListAvailable -Name IISAdministration

    # Try auto-install of WebAdministration if missing
    if (-not $webAdminModule) {
        Write-Info "WebAdministration module not found. Attempting installation (Web-Scripting-Tools)..."
        try {
            Install-WindowsFeature -Name Web-Scripting-Tools -IncludeManagementTools -ErrorAction Stop | Out-Null
            $webAdminModule = Get-Module -ListAvailable -Name WebAdministration
        } catch {
            Write-Host "  Warning: Failed to install Web-Scripting-Tools: $_" -ForegroundColor Yellow
        }
    }
    
    if ($webAdminModule) {
        Import-Module WebAdministration -ErrorAction Stop
        Write-Success "WebAdministration module loaded"
        $script:UseWebAdministration = $true
    } elseif ($iisAdminModule) {
        Import-Module IISAdministration -ErrorAction Stop
        Write-Success "IISAdministration module loaded"
        $script:UseWebAdministration = $false
        if (-not (Get-Command Get-WebAppPoolState -ErrorAction SilentlyContinue)) {
            Write-Host "Using IISAdministration module (some commands may differ)" -ForegroundColor Yellow
            Write-Host "  Tip: Install WebAdministration with: Install-WindowsFeature Web-Scripting-Tools -IncludeManagementTools" -ForegroundColor Yellow
        }
    } else {
        Write-Host "WebAdministration or IISAdministration module not found" -ForegroundColor Red
        Write-Host "  Install IIS Management Tools:" -ForegroundColor Yellow
        Write-Host "  Install-WindowsFeature Web-Scripting-Tools -IncludeManagementTools" -ForegroundColor Cyan
        Write-Host "  OR" -ForegroundColor Yellow
        Write-Host "  Install-WindowsFeature Web-Mgmt-Tools" -ForegroundColor Cyan
        throw "IIS Management modules not available"
    }
    
    # Check if .NET 10 Hosting Bundle is installed
    $aspNetCoreModule = $null
    
    # Try WebAdministration method first
    if (Get-Command Get-WebGlobalModule -ErrorAction SilentlyContinue) {
        $aspNetCoreModule = Get-WebGlobalModule -ErrorAction SilentlyContinue | 
            Where-Object { $_.Name -eq "AspNetCoreModuleV2" }
        
        if ($aspNetCoreModule) {
            Write-Success "ASP.NET Core Module V2 is installed (via WebAdministration)"
        }
    }
    
    # Fallback: Check IIS applicationHost.config directly
    if (-not $aspNetCoreModule) {
        $applicationHostConfig = "$env:SystemRoot\System32\inetsrv\config\applicationHost.config"
        if (Test-Path $applicationHostConfig) {
            try {
                [xml]$config = Get-Content $applicationHostConfig
                $aspNetCoreModule = $config.configuration.'system.webServer'.globalModules.add | 
                    Where-Object { $_.name -eq "AspNetCoreModuleV2" }
                
                if ($aspNetCoreModule) {
                    Write-Success "ASP.NET Core Module V2 is installed (found in IIS config)"
                    $aspNetCoreModule = $true  # Set to true to pass check
                }
            } catch {
                Write-Host "  Warning: Could not parse applicationHost.config: $_" -ForegroundColor Yellow
            }
        }
    }
    
    # Final fallback: Check if DLL exists (multiple possible locations)
    if (-not $aspNetCoreModule) {
        $possiblePaths = @(
            "$env:SystemRoot\System32\inetsrv\aspnetcorev2.dll",
            "$env:SystemRoot\SysWOW64\inetsrv\aspnetcorev2.dll",
            "${env:ProgramFiles}\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll",
            "${env:ProgramFiles(x86)}\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
        )
        
        foreach ($modulePath in $possiblePaths) {
            if (Test-Path $modulePath) {
                $fileInfo = Get-Item $modulePath
                Write-Success "ASP.NET Core Module V2 is installed"
                Write-Info "DLL found at: $modulePath"
                Write-Info "Version: $($fileInfo.VersionInfo.FileVersion)"
                $aspNetCoreModule = $true
                break
            }
        }
    }
    
    if (-not $aspNetCoreModule) {
        Write-Host "ASP.NET Core Module V2 not found" -ForegroundColor Red
        Write-Host "" -ForegroundColor Yellow
        Write-Host "  The ASP.NET Core Module is required to host .NET Core applications in IIS." -ForegroundColor Yellow
        Write-Host "" -ForegroundColor Yellow
        Write-Host "  To install the .NET 10 Hosting Bundle:" -ForegroundColor Yellow
        Write-Host "  1. Download from: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Cyan
        Write-Host "     OR direct link: https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe" -ForegroundColor Cyan
        Write-Host "" -ForegroundColor Yellow
        Write-Host "  2. Run the installer: dotnet-hosting-10.0.x-win.exe" -ForegroundColor Cyan
        Write-Host "" -ForegroundColor Yellow
        Write-Host "  3. After installation, restart IIS:" -ForegroundColor Cyan
        Write-Host "     net stop was /y" -ForegroundColor Gray
        Write-Host "     net start w3svc" -ForegroundColor Gray
        Write-Host "" -ForegroundColor Yellow
        Write-Host "  4. Verify installation:" -ForegroundColor Cyan
        Write-Host "     Test-Path `"$env:SystemRoot\System32\inetsrv\aspnetcorev2.dll`"" -ForegroundColor Gray
        Write-Host "" -ForegroundColor Yellow
        
        throw "ASP.NET Core Module not installed"
    }
    
    if ($aspNetCoreModule -ne $true) {
        Write-Success "ASP.NET Core Module V2 is installed"
    }
    
    # Check if source files exist
    if (-not $WhatIf) {
        if (-not (Test-Path $SourcePath)) {
            Write-Host "Source files not found at: $SourcePath" -ForegroundColor Red
            Write-Host "  Publish the Web Dashboard first using:" -ForegroundColor Yellow
            Write-Host "  dotnet publish SecureBootDashboard.Web --configuration Release" -ForegroundColor Cyan
            throw "Source files not found"
        }
        Write-Success "Source files found: $SourcePath"
    }
    
    # Check SSL certificate if thumbprint provided
    if ($SslCertificateThumbprint) {
        $cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | 
            Where-Object { $_.Thumbprint -eq $SslCertificateThumbprint }
        
        if (-not $cert) {
            Write-Host "SSL certificate not found with thumbprint: $SslCertificateThumbprint" -ForegroundColor Red
            throw "SSL certificate not found"
        }
        Write-Success "SSL certificate found: $($cert.Subject)"
        
        # Check if certificate is valid
        if ($cert.NotAfter -lt (Get-Date)) {
            Write-Host "SSL certificate has expired: $($cert.NotAfter)" -ForegroundColor Yellow
        }
        if ($cert.NotBefore -gt (Get-Date)) {
            Write-Host "SSL certificate is not yet valid: $($cert.NotBefore)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "No SSL certificate thumbprint provided" -ForegroundColor Yellow
        Write-Host "  HTTPS binding will need to be configured manually" -ForegroundColor Yellow
    }
}

function Has-Command {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function New-ApplicationPool {
    param(
        [string]$Name
    )
    
    Write-Step "Creating Application Pool"
    
    if ($WhatIf) {
        Write-Info "Would create Application Pool: $Name"
        return
    }
    
    # Check if WebAdministration module is available with IIS PSDrive support
    $hasWebAdminDrive = $false
    if (Has-Command 'New-WebAppPool') {
        $hasWebAdminDrive = Test-Path "IIS:" -ErrorAction SilentlyContinue
    }
    
    if ($hasWebAdminDrive) {
        # WebAdministration path
        if (Test-Path "IIS:\AppPools\$Name") {
            Write-Host "Application Pool '$Name' already exists" -ForegroundColor Yellow
        } else {
            New-WebAppPool -Name $Name -Force -ErrorAction SilentlyContinue | Out-Null
            Write-Success "Application Pool created: $Name"
        }
        
        Set-ItemProperty "IIS:\AppPools\$Name" -Name "managedRuntimeVersion" -Value "v4.0"
        Write-Info "Runtime version: .NET CLR v4.0 (required for Windows Auth compatibility)"
        Set-ItemProperty "IIS:\AppPools\$Name" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
        Write-Info "Identity: ApplicationPoolIdentity"
        Set-ItemProperty "IIS:\AppPools\$Name" -Name "startMode" -Value "AlwaysRunning"
        Write-Info "Start Mode: AlwaysRunning"
        Set-ItemProperty "IIS:\AppPools\$Name" -Name "autoStart" -Value $true
        Write-Info "Auto Start: Enabled"
        Set-ItemProperty "IIS:\AppPools\$Name" -Name "recycling.periodicRestart.time" -Value "00:00:00"
        Write-Info "Periodic Restart: Disabled"
        Set-ItemProperty "IIS:\AppPools\$Name" -Name "recycling.periodicRestart.privateMemory" -Value 0
        Write-Info "Memory limit: No limit"
        Set-ItemProperty "IIS:\AppPools\$Name" -Name "processModel.idleTimeout" -Value "00:00:00"
        Write-Info "Idle Timeout: Disabled"
        
        Write-Success "Application Pool configured"
        return
    }

    # Fallback: IISAdministration / ServerManager API
    $sm = $null
    if (Has-Command 'Get-IISServerManager') { $sm = Get-IISServerManager } else { throw "IIS ServerManager not available" }
    
    $pool = $sm.ApplicationPools[$Name]
    if (-not $pool) {
        $pool = $sm.ApplicationPools.Add($Name)
        Write-Success "Application Pool created: $Name"
    } else {
        Write-Host "Application Pool '$Name' already exists" -ForegroundColor Yellow
    }
    
    $pool.ManagedRuntimeVersion = "v4.0"
    $pool.ProcessModel.IdentityType = [Microsoft.Web.Administration.ProcessModelIdentityType]::ApplicationPoolIdentity
    if ($pool.PSObject.Properties["StartMode"]) { $pool.StartMode = [Microsoft.Web.Administration.StartMode]::AlwaysRunning }
    $pool.AutoStart = $true
    $pool.Recycling.PeriodicRestart.Time = [TimeSpan]::Zero
    $pool.Recycling.PeriodicRestart.PrivateMemory = 0
    $pool.ProcessModel.IdleTimeout = [TimeSpan]::Zero
    
    $sm.CommitChanges()
    Write-Success "Application Pool configured"
}

function Copy-WebFiles {
    param(
        [string]$Source,
        [string]$Destination
    )
    
    Write-Step "Copying Web Dashboard files"
    
    if ($WhatIf) {
        Write-Info "Would copy files from: $Source"
        Write-Info "Would copy files to: $Destination"
        return
    }
    
    if (-not (Test-Path $Destination)) {
        New-Item -Path $Destination -ItemType Directory -Force | Out-Null
        Write-Info "Created directory: $Destination"
    }
    
    # Stop AppPool if running
    try {
        if (Has-Command 'Get-WebAppPoolState') {
            $appPoolState = (Get-WebAppPoolState -Name $AppPoolName).Value
            if ($appPoolState -eq "Started") { 
                Stop-WebAppPool -Name $AppPoolName
                Write-Info "Stopped Application Pool: $AppPoolName"
                Start-Sleep -Milliseconds 500 # Give it time to stop
            }
        } elseif (Has-Command 'Get-IISServerManager') {
            $sm = Get-IISServerManager
            $pool = $sm.ApplicationPools[$AppPoolName]
            if ($pool -and $pool.State -eq [Microsoft.Web.Administration.ObjectState]::Started) { 
                $pool.Stop()
                $sm.CommitChanges()
                Write-Info "Stopped Application Pool: $AppPoolName"
                Start-Sleep -Milliseconds 500 # Give it time to stop
            }
        }
    } catch {
        Write-Info "Note: Could not stop application pool (it may already be stopped): $_"
    }
    
    if (Test-Path "$Destination\SecureBootDashboard.Web.dll") {
        $backupPath = "$Destination.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
        Write-Info "Creating backup: $backupPath"
        Copy-Item -Path $Destination -Destination $backupPath -Recurse -Force
    }
    
    Get-ChildItem -Path $Destination -Exclude "logs" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    
    Write-Info "Copying files..."
    Copy-Item -Path "$Source\*" -Destination $Destination -Recurse -Force
    
    $logsPath = "C:\Logs\SecureBootDashboard"
    if (-not (Test-Path $logsPath)) { New-Item -Path $logsPath -ItemType Directory -Force | Out-Null; Write-Info "Created logs directory: $logsPath" }
    
    Write-Success "Files copied to: $Destination"
}

function Convert-ThumbprintToBytes {
    param([Parameter(Mandatory=$true)][string]$Thumbprint)
    $hex = ($Thumbprint -replace '\s','') -replace '[^0-9A-Fa-f]',''
    $bytes = New-Object byte[] ($hex.Length/2)
    for ($i=0; $i -lt $hex.Length; $i+=2) { $bytes[$i/2] = [Convert]::ToByte($hex.Substring($i,2),16) }
    return ,$bytes
}

function New-Website {
    param(
        [string]$Name,
        [string]$PhysicalPath,
        [string]$AppPool,
        [string]$HostHeader,
        [int]$HttpsPort,
        [int]$HttpPort,
        [string]$CertThumbprint
    )
    
    Write-Step "Creating Website"
    
    if ($WhatIf) {
        Write-Info "Would create website: $Name"
        Write-Info "  Physical Path: $PhysicalPath"
        Write-Info "  App Pool: $AppPool"
        Write-Info "  Host Header: $HostHeader"
        return
    }
    
    if (Has-Command 'WebAdministration\New-WebSite') {
        # Check if IIS PSDrive is available (required for WebAdministration cmdlets)
        $hasWebAdminDrive = Test-Path "IIS:" -ErrorAction SilentlyContinue
        
        if ($hasWebAdminDrive) {
            if (Test-Path "IIS:\Sites\$Name") {
                Write-Host "Website '$Name' already exists" -ForegroundColor Yellow
                $response = Read-Host "Do you want to remove and recreate it? (Y/N)"
                if ($response -eq "Y") { WebAdministration\Remove-WebSite -Name $Name; Write-Info "Removed existing website" } else { Write-Info "Keeping existing website"; return }
            }
            
            if ($CertThumbprint) {
                WebAdministration\New-WebSite -Name $Name -PhysicalPath $PhysicalPath -ApplicationPool $AppPool -HostHeader $HostHeader -Port $HttpsPort -Ssl | Out-Null
                Write-Success "Website created with HTTPS binding"
                
                # Bind SSL certificate to the HTTPS binding
                $bindingPath = "IIS:\Sites\$Name\Bindings\*:$($HttpsPort):$HostHeader"
                if (Test-Path $bindingPath) {
                    Set-ItemProperty -Path $bindingPath -Name "certificateHash" -Value $CertThumbprint
                    Set-ItemProperty -Path $bindingPath -Name "certificateStoreName" -Value "My"
                    Write-Success "SSL certificate bound"
                } else {
                    Write-Host "Could not find binding path: $bindingPath" -ForegroundColor Yellow
                    Write-Host "  Please manually bind the certificate in IIS Manager" -ForegroundColor Yellow
                }
            } else {
                WebAdministration\New-WebSite -Name $Name -PhysicalPath $PhysicalPath -ApplicationPool $AppPool -HostHeader $HostHeader -Port $HttpsPort | Out-Null
                Write-Success "Website created (SSL needs manual configuration)"
            }
            
            if ($CreateHttpBinding) {
                WebAdministration\New-WebBinding -Name $Name -Protocol "http" -Port $HttpPort -HostHeader $HostHeader | Out-Null
                Write-Success "HTTP binding added"
            }
            return
        }
    }

    # Fallback: IISAdministration / ServerManager API
    $sm = $null
    if (Has-Command 'Get-IISServerManager') { $sm = Get-IISServerManager } else { throw "IIS ServerManager not available" }

    $existing = $sm.Sites[$Name]
    if ($existing) {
        Write-Host "Website '$Name' already exists" -ForegroundColor Yellow
        $response = Read-Host "Do you want to remove and recreate it? (Y/N)"
        if ($response -eq "Y") { $sm.Sites.Remove($existing); $sm.CommitChanges(); Write-Info "Removed existing website" } else { Write-Info "Keeping existing website"; return }
    }

    $bindingInfo = "*:$($HttpsPort):$HostHeader"
    $protocol = if ($CertThumbprint) { "https" } else { "http" }
    $site = $sm.Sites.Add($Name, $protocol, $bindingInfo, $PhysicalPath)
    $site.Applications["/"].ApplicationPoolName = $AppPool

    if ($CertThumbprint -and $protocol -eq "https") {
        $binding = $site.Bindings | Where-Object { $_.Protocol -eq "https" -and $_.BindingInformation -eq $bindingInfo } | Select-Object -First 1
        if ($binding) {
            $binding.CertificateStoreName = "My"
            $binding.CertificateHash = (Convert-ThumbprintToBytes -Thumbprint $CertThumbprint)
            Write-Success "SSL certificate bound"
        }
    }

    if ($CreateHttpBinding) {
        $site.Bindings.Add("*:$($HttpPort):$HostHeader", $null, "http") | Out-Null
        Write-Success "HTTP binding added"
    }

    $sm.CommitChanges()
    Write-Success "Website created"
}

function Set-WebConfiguration {
param(
    [string]$SiteName
)
    
Write-Step "Configuring Website settings"
    
if ($WhatIf) { Write-Info "Would configure website settings"; return }
    
$hasWebAdminDrive = Test-Path "IIS:" -ErrorAction SilentlyContinue
    
if ($hasWebAdminDrive -and (Has-Command 'Set-WebConfigurationProperty')) {
    # Configure Windows Authentication (ENABLE for ASP.NET Core Negotiate handler)
    Write-Info "Configuring authentication..."
        
    try {
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
            -Filter "system.webServer/security/authentication/windowsAuthentication" `
            -Name "enabled" -Value $true
        Write-Success "Windows Authentication: ENABLED"
    } catch {
        Write-Host "Could not enable Windows Authentication: $_" -ForegroundColor Yellow
    }
        
    try {
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
            -Filter "system.webServer/security/authentication/anonymousAuthentication" `
            -Name "enabled" -Value $false
        Write-Success "Anonymous Authentication: DISABLED"
    } catch {
        Write-Host "Could not disable Anonymous Authentication: $_" -ForegroundColor Yellow
    }
        # Enable HTTP to HTTPS redirect if requested
        if ($EnableHttpRedirect) {
            # Install URL Rewrite module first (if not already installed)
            $urlRewrite = Get-WindowsFeature -Name "Web-Http-Redirect"
            if (-not $urlRewrite.Installed) {
                Write-Host "URL Rewrite module not installed" -ForegroundColor Red
                Write-Host "  Install manually from: https://www.iis.net/downloads/microsoft/url-rewrite" -ForegroundColor Yellow
            } else {
                # Add redirect rule
                Add-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
                    -Filter "system.webServer/rewrite/rules" `
                    -Name "." `
                    -Value @{
                        name = "HTTP to HTTPS redirect"
                        patternSyntax = "Wildcard"
                        stopProcessing = "True"
                    }
                
                Write-Success "HTTP to HTTPS redirect enabled"
            }
        }
        
        # Configure request limits
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
            -Filter "system.webServer/security/requestFiltering/requestLimits" `
            -Name "maxAllowedContentLength" -Value 52428800  # 50 MB
    
        Write-Info "Max request size: 50 MB"
    
        # Configure static content caching
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
            -Filter "system.webServer/staticContent/clientCache" `
            -Name "cacheControlMode" -Value "UseMaxAge"
    
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
            -Filter "system.webServer/staticContent/clientCache" `
            -Name "cacheControlMaxAge" -Value "7.00:00:00"  # 7 days
    
        Write-Info "Static content caching: 7 days"
    
        # Enable compression
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
            -Filter "system.webServer/httpCompression" `
            -Name "doDynamicCompression" -Value $true
    
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
            -Filter "system.webServer/httpCompression" `
            -Name "doStaticCompression" -Value $true
    
        Write-Info "Compression: Enabled"
    
        # Configure logging
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
            -Filter "system.webServer/httpLogging" `
            -Name "dontLog" -Value $false
    
        Write-Info "HTTP logging: Enabled"
        
        Write-Success "Website settings configured"
    } else {
        Write-Host "Skipping site configuration tweaks (WebAdministration with IIS PSDrive not available)" -ForegroundColor Yellow
        Write-Host "  Note: Basic configuration has been applied. Advanced settings can be configured manually in IIS Manager." -ForegroundColor Gray
        
        # Try appcmd.exe as fallback for authentication
        Write-Info "Attempting authentication configuration via appcmd.exe..."
        $appcmd = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
        
        if (Test-Path $appcmd) {
            try {
                & $appcmd set config "$SiteName" `
                    -section:system.webServer/security/authentication/windowsAuthentication `
                    /enabled:true `
                    /commit:apphost | Out-Null
                Write-Success "Windows Authentication: ENABLED (via appcmd)"
                
                & $appcmd set config "$SiteName" `
                    -section:system.webServer/security/authentication/anonymousAuthentication `
                    /enabled:false `
                    /commit:apphost | Out-Null
                Write-Success "Anonymous Authentication: DISABLED (via appcmd)"
            } catch {
                Write-Host "Could not configure authentication via appcmd: $_" -ForegroundColor Yellow
                Write-Host "  You'll need to configure authentication manually in IIS Manager" -ForegroundColor Yellow
            }
        }
    }
}

function Set-ApplicationConfiguration {
    param(
        [string]$PhysicalPath
    )
    
    Write-Step "Configuring application settings"
    
    if ($WhatIf) {
        Write-Info "Would configure application settings"
        return
    }
    
    # Check if appsettings.Production.json exists
    $prodSettings = Join-Path $PhysicalPath "appsettings.Production.json"
    if (-not (Test-Path $prodSettings)) {
        Write-Host "appsettings.Production.json not found" -ForegroundColor Yellow
        Write-Host "  Creating template..." -ForegroundColor Yellow
        
        $template = @"
{
  "ApiSettings": {
    "BaseUrl": "https://api.yourdomain.com",
    "UseCertificateAuth": true,
    "CertificateThumbprint": "YOUR_WEB_CLIENT_CERT_THUMBPRINT",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "C:\\Logs\\SecureBootDashboard\\web-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
"@
        
        Set-Content -Path $prodSettings -Value $template
        Write-Success "Template appsettings.Production.json created"
        Write-Host "  ? Edit this file to configure API connection" -ForegroundColor Yellow
    } else {
        Write-Success "appsettings.Production.json found"
    }
    
    # Create or update web.config with correct settings
    $webConfig = Join-Path $PhysicalPath "web.config"
    
    if (-not (Test-Path $webConfig)) {
        Write-Host "web.config not found - creating with correct OutOfProcess configuration..." -ForegroundColor Yellow
        
        $webConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SecureBootDashboard.Web.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile="C:\Logs\SecureBootDashboard\stdout"
                  hostingModel="outofprocess"
                  forwardWindowsAuthToken="false">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
        
        Set-Content -Path $webConfig -Value $webConfigContent -Encoding UTF8
        Write-Success "web.config created with OutOfProcess configuration"
        Write-Info "  hostingModel: outofprocess"
        Write-Info "  forwardWindowsAuthToken: false"
        Write-Info "  stdoutLogEnabled: true"
    } else {
        # Update existing web.config
        Write-Info "Updating existing web.config..."
        
        try {
            [xml]$config = Get-Content $webConfig
            
            # Ensure configuration element exists
            if (-not $config.configuration) {
                $config.AppendChild($config.CreateElement("configuration")) | Out-Null
            }
            
            # Ensure location element exists
            $location = $config.configuration.location
            if (-not $location) {
                $location = $config.CreateElement("location")
                $location.SetAttribute("path", ".")
                $location.SetAttribute("inheritInChildApplications", "false")
                $config.configuration.AppendChild($location) | Out-Null
            }
            
            # Ensure system.webServer element exists
            $systemWebServer = $location.'system.webServer'
            if (-not $systemWebServer) {
                $systemWebServer = $config.CreateElement("system.webServer")
                $location.AppendChild($systemWebServer) | Out-Null
            }
            
            # Ensure handlers element exists
            $handlers = $systemWebServer.handlers
            if (-not $handlers) {
                $handlers = $config.CreateElement("handlers")
                $systemWebServer.AppendChild($handlers) | Out-Null
            }
            
            # Ensure aspNetCore handler exists
            $aspNetCoreHandler = $handlers.add | Where-Object { $_.name -eq "aspNetCore" }
            if (-not $aspNetCoreHandler) {
                $aspNetCoreHandler = $config.CreateElement("add")
                $aspNetCoreHandler.SetAttribute("name", "aspNetCore")
                $aspNetCoreHandler.SetAttribute("path", "*")
                $aspNetCoreHandler.SetAttribute("verb", "*")
                $aspNetCoreHandler.SetAttribute("modules", "AspNetCoreModuleV2")
                $aspNetCoreHandler.SetAttribute("resourceType", "Unspecified")
                $handlers.AppendChild($aspNetCoreHandler) | Out-Null
            }
            
            # Ensure aspNetCore element exists
            $aspNetCore = $systemWebServer.aspNetCore
            if (-not $aspNetCore) {
                $aspNetCore = $config.CreateElement("aspNetCore")
                $systemWebServer.AppendChild($aspNetCore) | Out-Null
            }
            
            # Set critical attributes
            $aspNetCore.SetAttribute("processPath", "dotnet")
            $aspNetCore.SetAttribute("arguments", ".\SecureBootDashboard.Web.dll")
            $aspNetCore.SetAttribute("stdoutLogEnabled", "true")
            $aspNetCore.SetAttribute("stdoutLogFile", "C:\Logs\SecureBootDashboard\stdout")
            $aspNetCore.SetAttribute("hostingModel", "outofprocess")
            $aspNetCore.SetAttribute("forwardWindowsAuthToken", "false")
            
            Write-Success "web.config updated:"
            Write-Info "  hostingModel: outofprocess (process isolation, no Windows Auth conflict)"
            Write-Info "  forwardWindowsAuthToken: false (Negotiate handler manages auth)"
            
            # Ensure environmentVariables element exists
            $envVars = $aspNetCore.environmentVariables
            if (-not $envVars) {
                $envVars = $config.CreateElement("environmentVariables")
                $aspNetCore.AppendChild($envVars) | Out-Null
            }
            
            # Set ASPNETCORE_ENVIRONMENT
            $envVar = $envVars.environmentVariable | Where-Object { $_.name -eq "ASPNETCORE_ENVIRONMENT" }
            if (-not $envVar) {
                $envVar = $config.CreateElement("environmentVariable")
                $envVar.SetAttribute("name", "ASPNETCORE_ENVIRONMENT")
                $envVar.SetAttribute("value", "Production")
                $envVars.AppendChild($envVar) | Out-Null
                Write-Info "  ASPNETCORE_ENVIRONMENT: Production"
            } else {
                $envVar.SetAttribute("value", "Production")
                Write-Info "  ASPNETCORE_ENVIRONMENT: Production (updated)"
            }
            
            # Save configuration
            $config.Save($webConfig)
            Write-Success "web.config saved successfully"
            
        } catch {
            Write-Host "Could not update web.config: $_" -ForegroundColor Yellow
            Write-Host "  Creating new web.config with correct configuration..." -ForegroundColor Yellow
            
            $webConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SecureBootDashboard.Web.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile="C:\Logs\SecureBootDashboard\stdout"
                  hostingModel="outofprocess"
                  forwardWindowsAuthToken="false">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
            
            # Backup old config
            $backup = "$webConfig.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
            Copy-Item $webConfig $backup -ErrorAction SilentlyContinue
            
            Set-Content -Path $webConfig -Value $webConfigContent -Encoding UTF8
            Write-Success "web.config created with OutOfProcess configuration"
        }
    }
}

function Start-WebSite {
    param(
        [string]$Name
    )
    
    # Prevent infinite loop - only allow this function to run once per script execution
    if ($script:WebSiteStartAttempted) {
        Write-Host "Start-WebSite already attempted. Skipping to prevent loop." -ForegroundColor Red
        return
    }
    $script:WebSiteStartAttempted = $true
    
    Write-Step "Starting Website"
    
    if ($WhatIf) { 
        Write-Info "Would start website: $Name"
        return 
    }

    $poolStarted = $false
    $websiteStarted = $false
    
    # Start Application Pool
    try {
        if (Has-Command 'Start-WebAppPool') { 
            $appPoolState = $null
            try {
                $appPoolState = (Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue).Value
            } catch {}
            
            if ($appPoolState -ne "Started") {
                Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
                $poolStarted = $true
            }
        } elseif (Has-Command 'Get-IISServerManager') { 
            $sm = Get-IISServerManager
            $p = $sm.ApplicationPools[$AppPoolName]
            if ($p -and $p.State -ne [Microsoft.Web.Administration.ObjectState]::Started) { 
                $p.Start()
                $sm.CommitChanges()
                $poolStarted = $true 
            }
        }
    } catch {
        Write-Host "Unable to start application pool: $_" -ForegroundColor Yellow
    }
    
    if ($poolStarted) { 
        Write-Info "Started Application Pool: $AppPoolName" 
    } else {
        Write-Info "Application Pool: $AppPoolName (already running or unable to start)"
    }

    # Start Website
    try {
        if (Has-Command 'Start-Website') { 
            $siteState = $null
            try {
                $siteState = (Get-WebSiteState -Name $Name -ErrorAction SilentlyContinue).Value
            } catch {}
            
            if ($siteState -ne "Started") {
                Start-Website -Name $Name -ErrorAction SilentlyContinue
                $websiteStarted = $true
            }
        } elseif (Has-Command 'Start-IISSite') { 
            Start-IISSite -Name $Name -ErrorAction SilentlyContinue
            $websiteStarted = $true
        } elseif (Has-Command 'Get-IISServerManager') { 
            $sm = Get-IISServerManager
            $s = $sm.Sites[$Name]
            if ($s -and $s.State -ne [Microsoft.Web.Administration.ObjectState]::Started) { 
                $s.Start()
                $sm.CommitChanges()
                $websiteStarted = $true 
            }
        }
    } catch {
        Write-Host "Unable to start website automatically: $_" -ForegroundColor Yellow
    }
    
    if ($websiteStarted) { 
        Write-Success "Website started: $Name" 
    } else {
        Write-Info "Website: $Name (already running or unable to start)"
    }
    
    # Explicit return to ensure function completes
    return
}

function Test-WebSite {
    param(
        [string]$Url
    )
    
    Write-Step "Testing Website"
    
    if ($WhatIf) {
        Write-Info "Would test website at: $Url"
        return
    }
    
    try {
        # Test with basic request (ignore SSL errors for self-signed certs)
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10 `
            -SkipCertificateCheck -ErrorAction Stop
        
        if ($response.StatusCode -eq 200) {
            Write-Success "Website is responding correctly (HTTP 200)"
        } else {
            Write-Host "Website returned status: $($response.StatusCode)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Website test failed: $_" -ForegroundColor Red
        Write-Host "  Check IIS logs and Application Event Viewer" -ForegroundColor Yellow
    }
}

function Show-Summary {
    param(
        [string]$SiteName,
        [string]$HostHeader,
        [int]$HttpsPort
    )
    
    Write-Host ""
    Write-Step "Deployment Summary"
    
    Write-Host "Website Details:" -ForegroundColor Green
    Write-Success "Site Name: $SiteName"
    Write-Success "App Pool: $AppPoolName"
    Write-Success "Physical Path: $PhysicalPath"
    Write-Success "Host Header: $HostHeader"
    Write-Success "HTTPS Port: $HttpsPort"
    
    $url = "https://$HostHeader"
    if ($HttpsPort -ne 443) {
        $url += ":$HttpsPort"
    }
    
    Write-Host "`nAccess URL:" -ForegroundColor Yellow
    Write-Host "  $url" -ForegroundColor Cyan
    
    Write-Host "`nAuthentication Configuration:" -ForegroundColor Yellow
    Write-Host "  Windows Authentication: ENABLED (for ASP.NET Core Negotiate handler)" -ForegroundColor White
    Write-Host "  Anonymous Authentication: DISABLED" -ForegroundColor White
    Write-Host "  App Pool .NET CLR: v4.0 (prevents Windows Auth conflict)" -ForegroundColor White
    
    Write-Host "`nNext Steps:" -ForegroundColor Yellow
    Write-Host "  1. Configure DNS to point $HostHeader to this server" -ForegroundColor White
    Write-Host "  2. Edit appsettings.Production.json to configure API connection" -ForegroundColor White
    Write-Host "  3. Configure authorization (see: CONFIGURE_DASHBOARD_AUTHORIZATION.md)" -ForegroundColor White
    Write-Host "  4. Install client certificate if using mutual TLS" -ForegroundColor White
    Write-Host "  5. Test access from browser: $url" -ForegroundColor White
    Write-Host "     Expected: Windows login prompt, then dashboard loads" -ForegroundColor White
    
    Write-Host "`nTroubleshooting:" -ForegroundColor Yellow
    Write-Host "  Logs: C:\Logs\SecureBootDashboard\web-*.log" -ForegroundColor Gray
    Write-Host "  IIS Logs: C:\inetpub\logs\LogFiles\W3SVC*" -ForegroundColor Gray
    Write-Host "  Event Viewer: Application > ASP.NET Core" -ForegroundColor Gray
    Write-Host ""
}

# ===============================================================================
# Main Execution
# ===============================================================================

try {
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host "SecureBootDashboard - Web Dashboard Deployment" -ForegroundColor Cyan
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host ""

    # Require elevation
    Assert-Admin
    
    # Step 1: Check prerequisites
    Test-Prerequisites
    
    # Step 2: Create Application Pool
    New-ApplicationPool -Name $AppPoolName
    
    # Step 3: Copy files
    Copy-WebFiles -Source $SourcePath -Destination $PhysicalPath
    
    # Step 4: Create Website
    New-Website `
        -Name $SiteName `
        -PhysicalPath $PhysicalPath `
        -AppPool $AppPoolName `
        -HostHeader $HostHeader `
        -HttpsPort $HttpsPort `
        -HttpPort $HttpPort `
        -CertThumbprint $SslCertificateThumbprint
    
    # Step 5: Configure website settings
    Set-WebConfiguration -SiteName $SiteName
    
    # Step 6: Configure application
    Set-ApplicationConfiguration -PhysicalPath $PhysicalPath
    
    # Step 7: Start website
    Start-WebSite -Name $SiteName
    
    # Step 8: Test website
    $testUrl = "https://$HostHeader"
    if ($HttpsPort -ne 443) {
        $testUrl += ":$HttpsPort"
    }
    Test-WebSite -Url $testUrl
    
    # Step 9: Show summary
    Show-Summary -SiteName $SiteName -HostHeader $HostHeader -HttpsPort $HttpsPort
    
    # Clear guard variable
    $global:DeployWebDashboardRunning = $false
    
    Write-Host ""
    Write-Host "Deployment completed successfully" -ForegroundColor Green
    Write-Host ""
    
    exit 0
    
} catch {
    Write-Host ""
    Write-Host "Deployment failed: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    
    # Clear guard variable on error
    $global:DeployWebDashboardRunning = $false
    
    exit 1
} finally {
    # Ensure guard is always cleared
    $global:DeployWebDashboardRunning = $false
}
