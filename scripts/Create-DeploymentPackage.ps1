# ===============================================================================
# Create-DeploymentPackage.ps1
# 
# Creates a complete deployment package for SecureBootDashboard
# including binaries, scripts, configuration templates, and documentation.
#
# Requirements:
# - .NET 10 SDK
# - .NET Framework 4.8 Developer Pack
# - dotnet-ef tools (for database migrations)
#
# Usage:
#   # Standard deployment (with tests)
#   .\Create-DeploymentPackage.ps1 -Version "1.5.0" -Configuration "Release"
#
#   # Skip test execution (compile test projects but don't run)
#   .\Create-DeploymentPackage.ps1 -Version "1.5.0" -Configuration "Release" -SkipTests
#
#   # Exclude test projects entirely (don't build/run tests - FASTEST)
#   .\Create-DeploymentPackage.ps1 -Version "1.5.0" -Configuration "Release" -ExcludeTestProjects
#
# Parameters:
#   -Version                    Package version (default: 1.5.2)
#   -Configuration              Release or Debug (default: Release)
#   -OutputPath                 Where to save package (default: .\deploy\packages)
#   -GenerateAzureCertificate   Generate Azure App Reg certificate
#   -AzureCertificatePassword   Password for Azure cert (default: AzureAppReg!Cert123)
#   -SkipTests                  Don't run tests (still compiles them)
#   -ExcludeTestProjects        Don't build test projects at all (FASTEST)
#   -SkipDatabaseScripts        Don't generate database migration scripts
#
# ===============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Version = "1.5.2",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = ".\deploy\packages",
    
    [Parameter(Mandatory = $false)]
    [switch]$GenerateAzureCertificate,
    
    [Parameter(Mandatory = $false)]
    [string]$AzureCertificatePassword = "AzureAppReg!Cert123",
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipTests,
    
    [Parameter(Mandatory = $false)]
    [switch]$ExcludeTestProjects,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipDatabaseScripts
)

# ===============================================================================
# Variables
# ===============================================================================

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$solutionRoot = Split-Path -Parent $PSScriptRoot

# Convert OutputPath to absolute path based on current location
# This ensures the path remains valid even when the script changes directories
if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path (Get-Location) $OutputPath
}

$packageName = "SecureBootDashboard-Deploy-v$Version"
$packagePath = Join-Path $OutputPath $packageName
$zipFile = "$packagePath.zip"

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $OutputPath "deployment-package-$timestamp.log"

# ===============================================================================
# Functions
# ===============================================================================

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet("Info", "Success", "Warning", "Error")]
        [string]$Level = "Info"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    # Console output with colors
    switch ($Level) {
        "Success" { Write-Host $Message -ForegroundColor Green }
        "Warning" { Write-Host $Message -ForegroundColor Yellow }
        "Error" { Write-Host $Message -ForegroundColor Red }
        default { Write-Host $Message }
    }
    
    # File output
    Add-Content -Path $logFile -Value $logMessage
}

function Test-Prerequisites {
    Write-Log "Checking prerequisites..." -Level Info
    
    # Check .NET SDK
    try {
        $dotnetVersion = dotnet --version
        Write-Log ".NET SDK version: $dotnetVersion" -Level Success
    } catch {
        Write-Log ".NET SDK not found. Install from https://dotnet.microsoft.com/download" -Level Error
        throw
    }
    
    # Check dotnet-ef tools (if not skipping database scripts)
    if (-not $SkipDatabaseScripts) {
        try {
            $efVersion = dotnet ef --version 2>&1 | Select-Object -First 1
            Write-Log "Entity Framework Core tools: $efVersion" -Level Success
        } catch {
            Write-Log "EF Core tools not found. Installing..." -Level Warning
            dotnet tool install --global dotnet-ef
            Write-Log "EF Core tools installed" -Level Success
        }
    }
    
    # Check solution file
    $solutionFile = Get-ChildItem -Path $solutionRoot -Filter "*.sln" | Select-Object -First 1
    if (-not $solutionFile) {
        Write-Log "Solution file not found in $solutionRoot" -Level Error
        throw "Solution file not found"
    }
    Write-Log "Solution file: $($solutionFile.Name)" -Level Success
    
    return $solutionFile.FullName
}

function Initialize-PackageStructure {
    Write-Log "Creating package structure..." -Level Info
    
    # Remove existing package if present
    if (Test-Path $packagePath) {
        Write-Log "Removing existing package directory..." -Level Warning
        Remove-Item -Path $packagePath -Recurse -Force
    }
    
    # Create directory structure
    $directories = @(
        "binaries\api",
        "binaries\web",
        "binaries\client",
        "database",
        "certificates",
        "config\api",
        "config\web",
        "config\client",
        "scripts\database",
        "scripts\deployment",
        "scripts\maintenance",
        "scripts\certificates",
        "docs"
    )
    
    foreach ($dir in $directories) {
        $fullPath = Join-Path $packagePath $dir
        New-Item -Path $fullPath -ItemType Directory -Force | Out-Null
        Write-Log "  Created: $dir"
    }
    
    Write-Log "Package structure created" -Level Success
}

function Build-Solution {
    param([string]$SolutionPath)
    
    Write-Log "Building solution..." -Level Info
    
    # Restore NuGet packages
    Write-Log "Restoring NuGet packages..."
    dotnet restore $SolutionPath
    
    # If ExcludeTestProjects is set, build only non-test projects
    if ($ExcludeTestProjects) {
        Write-Log "Building non-test projects (test projects excluded)..."
        
        # Get the solution directory
        $slnDir = Split-Path -Parent $SolutionPath
        
        # Build only non-test projects
        $projectPatterns = @(
            "**/SecureBootDashboard.Api.csproj",
            "**/SecureBootDashboard.Web.csproj",
            "**/SecureBootWatcher.Client.csproj",
            "**/SecureBootWatcher.Shared.csproj"
        )
        
        foreach ($pattern in $projectPatterns) {
            $projects = Get-ChildItem -Path $slnDir -Filter (Split-Path -Leaf $pattern) -Recurse
            foreach ($project in $projects) {
                # Skip test projects
                if ($project.FullName -notmatch "\.Tests\.csproj$") {
                    Write-Log "  Building: $($project.Name)"
                    dotnet build $project.FullName --configuration $Configuration --no-restore
                    
                    if ($LASTEXITCODE -ne 0) {
                        Write-Log "Build failed for $($project.Name)" -Level Error
                        throw "Build failed"
                    }
                }
            }
        }
        
        Write-Log "Non-test projects built successfully" -Level Success
    } else {
        # Run tests if not skipped
        if (-not $SkipTests) {
            Write-Log "Running tests..."
            dotnet test $SolutionPath --configuration $Configuration --no-restore --verbosity minimal
            if ($LASTEXITCODE -ne 0) {
                Write-Log "Some tests failed, but continuing..." -Level Warning
            } else {
                Write-Log "All tests passed" -Level Success
            }
        }
        
        # Build solution
        Write-Log "Building solution in $Configuration mode..."
        dotnet build $SolutionPath --configuration $Configuration --no-restore
        
        if ($LASTEXITCODE -ne 0) {
            Write-Log "Build failed" -Level Error
            throw "Build failed"
        }
        
        Write-Log "Solution built successfully" -Level Success
    }
}

function Publish-ApiProject {
Write-Log "Publishing API project..." -Level Info
    
$apiProjectPath = Join-Path $solutionRoot "SecureBootDashboard.Api"
$apiOutputPath = Join-Path $packagePath "binaries\api"
    
dotnet publish "$apiProjectPath\SecureBootDashboard.Api.csproj" `
    --configuration $Configuration `
    --output $apiOutputPath `
    --self-contained false `
    --runtime win-x64
    
if ($LASTEXITCODE -ne 0) {
    Write-Log "API publish failed" -Level Error
    throw
}
    
    # Remove appsettings.Development.json from output
    $devSettings = Join-Path $apiOutputPath "appsettings.Development.json"
    if (Test-Path $devSettings) {
        Remove-Item $devSettings -Force
    }
    
    Write-Log "API published to binaries\api" -Level Success
}

function Publish-WebProject {
Write-Log "Publishing Web project..." -Level Info
    
$webProjectPath = Join-Path $solutionRoot "SecureBootDashboard.Web"
$webOutputPath = Join-Path $packagePath "binaries\web"
    
dotnet publish "$webProjectPath\SecureBootDashboard.Web.csproj" `
    --configuration $Configuration `
    --output $webOutputPath `
    --self-contained false `
    --runtime win-x64
    
if ($LASTEXITCODE -ne 0) {
    Write-Log "Web publish failed" -Level Error
    throw
}
    
    # Remove appsettings.Development.json from output
    $devSettings = Join-Path $webOutputPath "appsettings.Development.json"
    if (Test-Path $devSettings) {
        Remove-Item $devSettings -Force
    }
    
    Write-Log "Web published to binaries\web" -Level Success
}

function Publish-ClientProject {
    Write-Log "Publishing Client project..." -Level Info
    
    $clientProjectPath = Join-Path $solutionRoot "SecureBootWatcher.Client"
    $clientOutputPath = Join-Path $packagePath "binaries\client"
    
    dotnet publish "$clientProjectPath\SecureBootWatcher.Client.csproj" `
        --configuration $Configuration `
        --output $clientOutputPath `
        --self-contained false `
        --runtime win-x64 `
        --framework net48
    
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Client publish failed" -Level Error
        throw
    }
    
    # Remove appsettings.local.json from output
    $localSettings = Join-Path $clientOutputPath "appsettings.local.json"
    if (Test-Path $localSettings) {
        Remove-Item $localSettings -Force
    }
    
    Write-Log "Client published to binaries\client" -Level Success
}

function Create-ClientPackageZip {
    Write-Log "Creating standalone client package ZIP..." -Level Info
    
    $clientBinariesPath = Join-Path $packagePath "binaries\client"
    $clientPackageZip = Join-Path $OutputPath "SecureBootWatcher-Client-v$Version.zip"
    
    # Validate client binaries exist
    if (-not (Test-Path $clientBinariesPath)) {
        Write-Log "Client binaries not found at: $clientBinariesPath" -Level Error
        throw "Client binaries directory not found"
    }
    
    try {
        # Remove existing client package if present
        if (Test-Path $clientPackageZip) {
            Remove-Item $clientPackageZip -Force
        }
        
        # Create temporary staging directory for packaging
        $tempStagingPath = Join-Path $env:TEMP "SecureBootWatcher-ClientPackage-$(Get-Date -Format 'yyyyMMddHHmmss')"
        New-Item -ItemType Directory -Path $tempStagingPath -Force | Out-Null
        
        try {
            # Copy client binaries to staging
            Write-Log "Copying client binaries to staging directory..."
            Copy-Item -Path "$clientBinariesPath\*" -Destination $tempStagingPath -Recurse -Force
            
            # Include Azure certificate if it was generated
            if ($GenerateAzureCertificate) {
                $certSourcePath = Join-Path $packagePath "certificates\AzureAppRegistration.pfx"
                
                Write-Log "  Checking for Azure certificate at: $certSourcePath"
                
                if (Test-Path $certSourcePath) {
                    # Create certificates subfolder in staging
                    $certStagingPath = Join-Path $tempStagingPath "certificates"
                    New-Item -ItemType Directory -Path $certStagingPath -Force | Out-Null
                    
                    # Copy Azure certificate to staging
                    Copy-Item -Path $certSourcePath -Destination $certStagingPath -Force
                    Write-Log "  Azure certificate included in client package" -Level Success
                    Write-Log "  Certificate will be installed by Deploy-Client.ps1"
                    
                    # Create certificate installation instructions file
                    $certInstructionsPath = Join-Path $certStagingPath "INSTALL-CERTIFICATE.txt"
                    $certInstructions = @"
Azure Storage Account Authentication Certificate
================================================

This package includes the Azure App Registration certificate required
for authenticating to Azure Storage Queue.

Certificate File: AzureAppRegistration.pfx
Password: $AzureCertificatePassword

AUTOMATIC INSTALLATION
======================

The Deploy-Client.ps1 script will automatically install this certificate
when you use the -CreateScheduledTask parameter.

The certificate will be installed to:
- Store Location: LocalMachine\My
- Accessible by: SYSTEM account (for scheduled task)

MANUAL INSTALLATION (if needed)
================================

If you need to install the certificate manually:

1. Open PowerShell as Administrator

2. Run the following commands:

   `$password = ConvertTo-SecureString -String "$AzureCertificatePassword" -Force -AsPlainText
   `$pfxPath = ".\certificates\AzureAppRegistration.pfx"
   
   Import-PfxCertificate ``
       -FilePath `$pfxPath ``
       -CertStoreLocation "Cert:\LocalMachine\My" ``
       -Password `$password ``
       -Exportable

3. Verify installation:

   Get-ChildItem Cert:\LocalMachine\My | Where-Object { `$_.Subject -like "*SecureBootDashboard*" }

4. Note the certificate thumbprint for appsettings.json configuration

SECURITY NOTES
==============

⚠️  The certificate password is: $AzureCertificatePassword

⚠️  This certificate provides access to Azure Storage Queue
⚠️  Protect this file and password appropriately
⚠️  Consider using Azure Key Vault in production environments
⚠️  Rotate certificates before expiration (check NotAfter date)

For more information, see:
- docs/AZURE_QUEUE_CONFIGURATION.md
- docs/CLIENT_DEPLOYMENT.md

"@
                    Set-Content -Path $certInstructionsPath -Value $certInstructions
                    Write-Log "  Certificate installation instructions created"
                } else {
                    Write-Log "  Warning: Azure certificate was requested but not found at: $certSourcePath" -Level Warning
                    Write-Log "  Verify that Generate-AzureAppRegistrationCertificate ran before Create-ClientPackageZip" -Level Warning
                }
            } else {
                Write-Log "  Azure certificate not included (-GenerateAzureCertificate not specified)"
            }
            
            # Create ZIP from staging directory
            Compress-Archive -Path "$tempStagingPath\*" -DestinationPath $clientPackageZip -Force
            
            # Calculate checksum
            $checksum = Get-FileHash -Path $clientPackageZip -Algorithm SHA256
            
            # Create checksum file
            $checksumFile = "$clientPackageZip.sha256"
            Set-Content -Path $checksumFile -Value "$($checksum.Hash)  $(Split-Path -Leaf $clientPackageZip)"
            
            # Get file size
            $fileSize = (Get-Item $clientPackageZip).Length
            $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
            
            Write-Log "Client package ZIP created" -Level Success
            Write-Log "  Path: $clientPackageZip"
            Write-Log "  Size: $fileSizeMB MB"
            Write-Log "  SHA256: $($checksum.Hash)"
            
            # Verify certificate inclusion by checking staging directory
            $certIncluded = Test-Path (Join-Path $tempStagingPath "certificates\AzureAppRegistration.pfx")
            if ($certIncluded) {
                Write-Log "  Includes: Azure certificate for Storage Account authentication" -Level Success
            } elseif ($GenerateAzureCertificate) {
                Write-Log "  Warning: Certificate was NOT included in package" -Level Warning
            }
            
            # Create README for client package
            $clientReadmePath = Join-Path $OutputPath "SecureBootWatcher-Client-v$Version-README.txt"
        $clientReadmeContent = @"
SecureBootWatcher Client Package v$Version
==========================================

Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Configuration: $Configuration
Package: $(Split-Path -Leaf $clientPackageZip)

This is a standalone client deployment package that can be used with Deploy-Client.ps1

## Quick Deployment

### Option 1: Install locally with default settings
``````powershell
.\Deploy-Client.ps1 -PackageZipPath "$clientPackageZip" -CreateScheduledTask
``````

### Option 2: Install with custom API URL
``````powershell
.\Deploy-Client.ps1 ``
    -PackageZipPath "$clientPackageZip" ``
    -ApiBaseUrl "https://api.yourdomain.com" ``
    -CreateScheduledTask
``````

### Option 3: Install with custom schedule (every 4 hours)
``````powershell
.\Deploy-Client.ps1 ``
    -PackageZipPath "$clientPackageZip" ``
    -ApiBaseUrl "https://api.yourdomain.com" ``
    -FleetId "production-fleet" ``
    -CreateScheduledTask ``
    -ScheduleType Custom ``
    -RepeatEveryHours 4
``````

## Package Contents

- SecureBootWatcher.Client.exe
- appsettings.json (configuration template)
- All required dependencies
$(if ($GenerateAzureCertificate) {
"- certificates/AzureAppRegistration.pfx (Azure Storage authentication)
- certificates/INSTALL-CERTIFICATE.txt (Installation instructions)"
})

## Configuration

The appsettings.json file will be automatically configured during deployment
if you provide -ApiBaseUrl and -FleetId parameters to Deploy-Client.ps1.

For manual configuration after deployment, edit:
C:\Program Files\SecureBootWatcher\appsettings.json

## Schedule Options

- Once: Run once at specified time
- Daily: Run daily at specified time (default)
- Hourly: Run every hour
- Custom: Run every N hours (1-24)

## File Integrity

SHA256: $($checksum.Hash)

Verify with:
``````powershell
`$hash = Get-FileHash -Path "$(Split-Path -Leaf $clientPackageZip)" -Algorithm SHA256
if (`$hash.Hash -eq "$($checksum.Hash)") {
    Write-Host "Package verified successfully" -ForegroundColor Green
} else {
    Write-Host "Package verification FAILED" -ForegroundColor Red
}
``````

## Documentation

For complete deployment documentation, see:
- docs/CLIENT_DEPLOYMENT.md
- docs/CLIENT_DEPLOYMENT_SCRIPTS.md

"@
        
        Set-Content -Path $clientReadmePath -Value $clientReadmeContent
        Write-Log "Client README created: $(Split-Path -Leaf $clientReadmePath)"
        
        # Cleanup staging directory
        } finally {
            if (Test-Path $tempStagingPath) {
                Remove-Item -Path $tempStagingPath -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        
    } catch {
        Write-Log " Failed to create client package ZIP: $_" -Level Error
        throw
    }
}

function Generate-DatabaseScripts {
    if ($SkipDatabaseScripts) {
        Write-Log "Skipping database script generation" -Level Warning
        return
    }
    
    Write-Log "Generating database scripts..." -Level Info
    
    $apiProjectPath = Join-Path $solutionRoot "SecureBootDashboard.Api"
    $databaseOutputPath = Join-Path $packagePath "database"
    
    # Generate migrations script
    Push-Location $apiProjectPath
    try {
        Write-Log "Generating EF Core migrations script..."
        dotnet ef migrations script --idempotent --output "$databaseOutputPath\migrations.sql"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Log " Failed to generate migrations script" -Level Error
            throw
        }
        
        Write-Log " Migrations script generated" -Level Success
    } finally {
        Pop-Location
    }
    
    # Copy database creation script
    $createDbScript = Join-Path $solutionRoot "scripts\database\Create-Database.sql"
    if (Test-Path $createDbScript) {
        Copy-Item $createDbScript -Destination $databaseOutputPath -Force
        Write-Log " Create-Database.sql copied" -Level Success
    } else {
        Write-Log "  Create-Database.sql not found, creating template..." -Level Warning
        
        $createDbTemplate = @"
-- =============================================================================
-- Secure Boot Dashboard - Database Creation Script
-- =============================================================================
USE master;
GO

-- Verifica se database esiste
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'SecureBootDashboard')
BEGIN
    PRINT 'Database SecureBootDashboard already exists';
END
ELSE
BEGIN
    -- Crea database
    CREATE DATABASE [SecureBootDashboard]
    ON PRIMARY (
        NAME = N'SecureBootDashboard_Data',
        FILENAME = N'D:\SQLData\SecureBootDashboard.mdf',
        SIZE = 100MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 50MB
    )
    LOG ON (
        NAME = N'SecureBootDashboard_Log',
        FILENAME = N'D:\SQLLogs\SecureBootDashboard_log.ldf',
        SIZE = 50MB,
        MAXSIZE = 10GB,
        FILEGROWTH = 10MB
    );
    
    PRINT 'Database SecureBootDashboard created successfully';
END
GO

-- Imposta database recovery model
ALTER DATABASE [SecureBootDashboard] SET RECOVERY SIMPLE;
GO

-- Imposta compatibilità SQL Server 2019+
ALTER DATABASE [SecureBootDashboard] SET COMPATIBILITY_LEVEL = 150;
GO

-- Abilita query store per performance monitoring
ALTER DATABASE [SecureBootDashboard] SET QUERY_STORE = ON;
ALTER DATABASE [SecureBootDashboard] SET QUERY_STORE (
    OPERATION_MODE = READ_WRITE,
    DATA_FLUSH_INTERVAL_SECONDS = 900,
    INTERVAL_LENGTH_MINUTES = 60,
    MAX_STORAGE_SIZE_MB = 1024,
    QUERY_CAPTURE_MODE = AUTO
);
GO

USE [SecureBootDashboard];
GO

PRINT 'Database setup complete';
GO
"@
        
        Set-Content -Path "$databaseOutputPath\Create-Database.sql" -Value $createDbTemplate
        Write-Log "  Create-Database.sql template created" -Level Success
    }
}

function Generate-AzureAppRegistrationCertificate {
    if (-not $GenerateAzureCertificate) {
        Write-Log "Skipping Azure App Registration certificate generation" -Level Warning
        return
    }
    
    Write-Log "Generating Azure App Registration certificate..." -Level Info
    
    $certOutputPath = Join-Path $packagePath "certificates"
    
    # Generate self-signed certificate for Azure App Registration
    $certSubject = "CN=SecureBootDashboard, O=MSLABS, C=IT"
    
    $cert = New-SelfSignedCertificate `
        -Subject $certSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyExportPolicy Exportable `
        -KeySpec Signature `
        -KeyLength 2048 `
        -KeyAlgorithm RSA `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(2) `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2")
    
    Write-Log "Certificate created:"
    Write-Log "  Thumbprint: $($cert.Thumbprint)"
    Write-Log "  Subject: $($cert.Subject)"
    Write-Log "  Valid Until: $($cert.NotAfter)"
    
    # Export certificate with private key (.pfx)
    $pfxPath = Join-Path $certOutputPath "AzureAppRegistration.pfx"
    $pfxPassword = ConvertTo-SecureString -String $AzureCertificatePassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword -Force | Out-Null
    
    # Export public certificate (.cer) for uploading to Azure
    $cerPath = Join-Path $certOutputPath "AzureAppRegistration.cer"
    Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
    
    # Create certificate info file
    $certInfoPath = Join-Path $certOutputPath "azure-certificate-info.txt"
    $certInfo = @"
Azure App Registration Certificate Information
===============================================

Certificate Subject: $($cert.Subject)
Thumbprint: $($cert.Thumbprint)
Valid From: $($cert.NotBefore.ToString('yyyy-MM-dd HH:mm:ss'))
Valid Until: $($cert.NotAfter.ToString('yyyy-MM-dd HH:mm:ss'))

Files Generated:
- AzureAppRegistration.pfx (with private key)
  Password: $AzureCertificatePassword
  
- AzureAppRegistration.cer (public key only)
  Upload this to Azure App Registration

Installation Instructions:
==========================

1. Upload .cer file to Azure App Registration:
   - Azure Portal ? Entra ID ? App registrations
   - Select your app registration
   - Certificates & secrets ? Certificates ? Upload certificate
   - Upload AzureAppRegistration.cer

2. Install .pfx on API server:
   PS> `$password = ConvertTo-SecureString -String "$AzureCertificatePassword" -Force -AsPlainText
   PS> Import-PfxCertificate -FilePath "AzureAppRegistration.pfx" ``
       -CertStoreLocation "Cert:\LocalMachine\My" ``
       -Password `$password -Exportable

3. Update appsettings.Production.json with thumbprint:
   "QueueProcessor": {
     "CertificateThumbprint": "$($cert.Thumbprint)",
     "CertificateStoreLocation": "LocalMachine",
     "CertificateStoreName": "My"
   }

??  Security Notes:
- Store .pfx file and password securely
- Use Azure Key Vault in production
- Rotate certificate before expiration
- Monitor certificate expiration dates

"@
    
    Set-Content -Path $certInfoPath -Value $certInfo
    
    # Remove certificate from CurrentUser store (keeping only exported files)
    Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force
    
    Write-Log "  Azure certificate generated:" -Level Success
    Write-Log "  .pfx: $pfxPath"
    Write-Log "  .cer: $cerPath"
    Write-Log "  Info: $certInfoPath"
}

function Copy-ConfigurationTemplates {
    Write-Log "Creating configuration templates..." -Level Info
    
    # API Configuration Template
    $apiConfigPath = Join-Path $packagePath "config\api"
    $apiConfigTemplate = @"
{
  "ConnectionStrings": {
    "SqlServer": "Server=YOUR_SQL_SERVER;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True"
  },
  "Urls": "https://0.0.0.0:5001;http://0.0.0.0:5000",
  "WebAppUrl": "https://dashboard.yourdomain.com",
  "MutualTls": {
    "Enabled": true,
    "AllowSelfSignedCertificates": false,
    "AllowedIssuers": ["Your Enterprise CA"],
    "CheckCertificateRevocation": true,
    "ValidateCertificateChain": true
  },
  "QueueProcessor": {
    "Enabled": true,
    "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "Certificate",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "CertificateThumbprint": "YOUR_AZURE_CERT_THUMBPRINT",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My",
    "MaxMessages": 10,
    "ProcessingInterval": "00:00:05",
    "EmptyQueuePollInterval": "00:00:30"
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
          "path": "C:\\Logs\\SecureBootDashboard\\api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
"@
    
    Set-Content -Path "$apiConfigPath\appsettings.Production.json" -Value $apiConfigTemplate
    Write-Log "  API configuration template created"
    
    # Web Configuration Template
    $webConfigPath = Join-Path $packagePath "config\web"
    $webConfigTemplate = @"
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
        "Microsoft": "Warning"
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
    
    Set-Content -Path "$webConfigPath\appsettings.Production.json" -Value $webConfigTemplate
    Write-Log "Web configuration template created"
    
    # Client Configuration Template
    $clientConfigPath = Join-Path $packagePath "config\client"
    $clientConfigTemplate = @"
{
  "SecureBootWatcher": {
    "FleetId": "",
    "RunMode": "Once",
    "RegistryPollInterval": "00:30:00",
    "EventQueryInterval": "00:30:00",
    "EventLookbackPeriod": "1.00:00:00",
    "Sinks": {
      "EnableFileShare": false,
      "EnableAzureQueue": false,
      "EnableWebApi": true,
      "ExecutionStrategy": "StopOnFirstSuccess",
      "SinkPriority": "WebApi,AzureQueue,FileShare",
      "MaxRetryAttempts": 3,
      "RetryDelay": "00:05:00",
      "UseExponentialBackoff": false,
      "WebApi": {
        "BaseAddress": "https://api.yourdomain.com",
        "IngestionRoute": "/api/SecureBootReports",
        "HttpTimeout": "00:00:30",
        "UseCertificateAuth": true,
        "CertificateThumbprint": "YOUR_CLIENT_CERT_THUMBPRINT",
        "CertificateStoreLocation": "LocalMachine",
        "CertificateStoreName": "My"
      }
    },
    "Commands": {
      "EnableCommandProcessing": true,
      "ProcessBeforeInventory": true,
      "MaxCommandsPerCycle": 10,
      "CommandExecutionDelay": "00:00:02",
      "ContinueOnCommandFailure": true
    }
  }
}
"@
    
    Set-Content -Path "$clientConfigPath\appsettings.json" -Value $clientConfigTemplate
    Write-Log "Client configuration template created"
}

function Copy-DeploymentScripts {
    Write-Log "Copying deployment scripts..." -Level Info
    
    $scriptsSourcePath = Join-Path $solutionRoot "scripts"
    $scriptsDestPath = Join-Path $packagePath "scripts"
    
    # Database scripts
    $databaseScriptsPath = Join-Path $scriptsDestPath "database"
    @(
        "Create-Database.ps1",
        "Apply-DatabaseMigrations.ps1",
        "Backup-Database.ps1"
    ) | ForEach-Object {
        $sourcePath = Join-Path $scriptsSourcePath $_
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath -Destination $databaseScriptsPath -Force
            Write-Log "  Copied: $_"
        }
    }
    
    # Deployment scripts (IIS deployment)
    $deploymentScriptsPath = Join-Path $scriptsDestPath "deployment"
    @(
        "Deploy-ApiServer.ps1",
        "Deploy-WebDashboard.ps1",
        "Deploy-API.ps1",
        "Deploy-Web.ps1",
        "Deploy-Client.ps1"
    ) | ForEach-Object {
        $sourcePath = Join-Path $scriptsSourcePath $_
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath -Destination $deploymentScriptsPath -Force
            Write-Log "  Copied: $_"
        } else {
            Write-Log "  Warning: $_ not found, skipping" -Level Warning
        }
    }
    
    # Certificate scripts
    $certScriptsPath = Join-Path $scriptsDestPath "certificates"
    @(
        "Deploy-ClientCertificate.ps1",
        "Grant-CertificatePermissions.ps1"
    ) | ForEach-Object {
        $sourcePath = Join-Path $scriptsSourcePath $_
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath -Destination $certScriptsPath -Force
            Write-Log "  Copied: $_"
        }
    }
    
    Write-Log "Deployment scripts copied" -Level Success
}

function Copy-Documentation {
    Write-Log "Copying documentation..." -Level Info
    
    $docsSourcePath = Join-Path $solutionRoot "docs"
    $docsDestPath = Join-Path $packagePath "docs"
    
    # Copy all markdown files
    Get-ChildItem -Path $docsSourcePath -Filter "*.md" | ForEach-Object {
        Copy-Item $_.FullName -Destination $docsDestPath -Force
        Write-Log "  Copied: $($_.Name)"
    }
    
    Write-Log "Documentation copied" -Level Success
}

function Create-ReadmeFile {
    Write-Log "Creating README.md..." -Level Info
    
    $readmePath = Join-Path $packagePath "README.md"
    $readmeContent = @"
# SecureBootDashboard Deployment Package v$Version

Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Configuration: $Configuration

## Package Contents

### Binaries
- **API**: ASP.NET Core 10 API Server
- **Web**: ASP.NET Core 10 Razor Pages Dashboard
- **Client**: .NET Framework 4.8 Client Agent

### Database
- `migrations.sql`: EF Core migrations (idempotent)
- `Create-Database.sql`: Database creation script

### Certificates
$(if ($GenerateAzureCertificate) {
"- `AzureAppRegistration.pfx`: App Registration certificate (with private key)
- `AzureAppRegistration.cer`: Public certificate for Azure upload
- `azure-certificate-info.txt`: Installation instructions

??  **Client and server certificates must be obtained from your Enterprise CA**"
} else {
"??  **Certificates not included - obtain from your Enterprise CA:**
- Client certificates for workstations
- Server certificates for API and Web (SSL/TLS)
- Azure App Registration certificate (optional, can be generated separately)"
})

### Configuration Templates
- `config/api/appsettings.Production.json`: API configuration
- `config/web/appsettings.Production.json`: Web configuration
- `config/client/appsettings.json`: Client configuration

### Scripts
- `scripts/database/`: Database setup and migrations
- `scripts/deployment/`: Server deployment scripts
- `scripts/certificates/`: Certificate deployment scripts

### Documentation
- Complete deployment guides
- Configuration references
- Troubleshooting guides

## Quick Start

### 1. Prerequisites
- Windows Server 2019+ (API/Web)
- SQL Server 2019+ or Azure SQL Database
- IIS 10.0+
- .NET 10 Runtime
- Enterprise CA-issued certificates

### 2. Database Setup
``````powershell
# Create database
.\scripts\database\Create-Database.ps1 -ServerInstance "YOUR_SQL_SERVER"

# Apply migrations
sqlcmd -S YOUR_SQL_SERVER -d SecureBootDashboard -i database\migrations.sql
``````

### 3. Certificates
$(if ($GenerateAzureCertificate) {
"**Azure App Registration:**
``````powershell
# Upload certificates/AzureAppRegistration.cer to Azure Portal
# Install .pfx on API server
`$password = ConvertTo-SecureString -String ""$AzureCertificatePassword"" -Force -AsPlainText
Import-PfxCertificate -FilePath ""certificates\AzureAppRegistration.pfx"" ``
    -CertStoreLocation ""Cert:\LocalMachine\My"" ``
    -Password `$password
``````"
} else {
"Obtain certificates from your Enterprise CA."
})

**Client/Server Certificates:**
- Request from your Enterprise CA
- Install on appropriate servers/workstations
- See `docs/SERVER_INFRASTRUCTURE_DEPLOYMENT.md` for details

### 4. API Deployment
``````powershell
# Update configuration
# Edit config/api/appsettings.Production.json

# Deploy to IIS (recommended - automated script)
.\scripts\deployment\Deploy-ApiServer.ps1 `
    -HostHeader "api.yourdomain.com" `
    -SslCertificateThumbprint "YOUR_SSL_CERT_THUMBPRINT" `
    -SourcePath "binaries\api"

# Alternative: Manual deployment
.\scripts\deployment\Deploy-API.ps1 -PhysicalPath "C:\inetpub\SecureBootDashboard.Api"
``````

### 5. Web Deployment
``````powershell
# Update configuration
# Edit config/web/appsettings.Production.json

# Deploy to IIS (recommended - automated script)
.\scripts\deployment\Deploy-WebDashboard.ps1 `
    -HostHeader "dashboard.yourdomain.com" `
    -SslCertificateThumbprint "YOUR_SSL_CERT_THUMBPRINT" `
    -SourcePath "binaries\web"

# Alternative: Manual deployment
.\scripts\deployment\Deploy-Web.ps1 -PhysicalPath "C:\inetpub\SecureBootDashboard.Web"
``````

### 6. Client Deployment

**Option A: Using standalone client ZIP (recommended)**
``````powershell
# The package includes a standalone client ZIP: SecureBootWatcher-Client-v$Version.zip
# This can be deployed using the Deploy-Client.ps1 script

# Install locally with default settings
.\scripts\deployment\Deploy-Client.ps1 ``
    -PackageZipPath "SecureBootWatcher-Client-v$Version.zip" ``
    -CreateScheduledTask

# Install with custom API and schedule
.\scripts\deployment\Deploy-Client.ps1 ``
    -PackageZipPath "SecureBootWatcher-Client-v$Version.zip" ``
    -ApiBaseUrl "https://api.yourdomain.com" ``
    -FleetId "production-fleet" ``
    -CreateScheduledTask ``
    -ScheduleType Custom ``
    -RepeatEveryHours 4
``````

**Option B: Deploy from binaries folder**
``````powershell
# Extract client from binaries\client folder
# Update config/client/appsettings.json
# Deploy via SCCM/Intune
# See docs/CLIENT_DEPLOYMENT.md
``````

## Configuration Checklist

### API (appsettings.Production.json)
- [ ] SQL Server connection string
- [ ] WebAppUrl (Web Dashboard URL)
- [ ] MutualTls.AllowedIssuers (your CA name)
- [ ] QueueProcessor.QueueServiceUri (Azure Storage)
- [ ] QueueProcessor.TenantId and ClientId
- [ ] QueueProcessor.CertificateThumbprint (Azure cert)

### Web (appsettings.Production.json)
- [ ] ApiSettings.BaseUrl (API URL)
- [ ] ApiSettings.CertificateThumbprint (Web client cert)

### Client (appsettings.json)
- [ ] Sinks.WebApi.BaseAddress (API URL)
- [ ] Sinks.WebApi.CertificateThumbprint (Client cert)
- [ ] FleetId (optional, for grouping)

## Verification

### Database
``````sql
-- Check tables created
USE SecureBootDashboard;
SELECT name FROM sys.tables ORDER BY name;

-- Check migrations applied
SELECT * FROM __EFMigrationsHistory;
``````

### API
``````powershell
# Health check
Invoke-RestMethod -Uri "https://api.yourdomain.com/health"
``````

### Web
``````powershell
# Open browser
Start-Process "https://dashboard.yourdomain.com"
``````

## Documentation

For detailed deployment instructions, see:
- `docs/SERVER_INFRASTRUCTURE_DEPLOYMENT.md` - Complete server setup
- `docs/CLIENT_DEPLOYMENT.md` - Client deployment guide
- `docs/MUTUAL_TLS_CONFIGURATION.md` - Certificate configuration
- `docs/TROUBLESHOOTING.md` - Common issues and solutions

## Support

For issues or questions:
1. Check documentation in `docs/` folder
2. Review logs in `C:\Logs\SecureBootDashboard\`
3. Check Event Viewer (Application log)

## Version Information

- Package Version: $Version
- Build Configuration: $Configuration
- Build Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
- .NET Version: $(dotnet --version)

---

**??  Security Notes:**
- Store certificate passwords securely (use Azure Key Vault in production)
- Update SQL connection strings with actual credentials
- Change default passwords before deployment
- Enable HTTPS/TLS for all endpoints
- Review security settings in appsettings.Production.json

**?? Enterprise CA Requirements:**
- Client certificates: Client Authentication EKU (1.3.6.1.5.5.7.3.2)
- Server certificates: Server Authentication EKU (1.3.6.1.5.5.7.3.1)
- All certificates: Valid chain to trusted root CA

"@
    
    Set-Content -Path $readmePath -Value $readmeContent
    Write-Log "README.md created" -Level Success
}

function Create-VersionFile {
    Write-Log "Creating VERSION.txt..." -Level Info
    
    $versionPath = Join-Path $packagePath "VERSION.txt"
    $versionContent = @"
SecureBootDashboard Deployment Package
======================================

Version: $Version
Configuration: $Configuration
Build Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Build Machine: $env:COMPUTERNAME
Build User: $env:USERNAME

.NET SDK Version: $(dotnet --version)

Project Versions:
-----------------
API: $Version
Web: $Version
Client: $Version

Package Contents:
-----------------
- Binaries (API, Web, Client)
- Database Scripts
- Configuration Templates
- Deployment Scripts
- Documentation

$(if ($GenerateAzureCertificate) {
"Azure Certificate Generated: Yes
Certificate Password: $AzureCertificatePassword
??  Store password securely!"
} else {
"Azure Certificate Generated: No
Obtain certificate from Enterprise CA or generate manually."
})

Tests Run: $(if ($SkipTests) { "No (skipped)" } else { "Yes" })
Database Scripts: $(if ($SkipDatabaseScripts) { "No (skipped)" } else { "Yes" })

Checksum (SHA256):
------------------
(Will be calculated after packaging)

"@
    
    Set-Content -Path $versionPath -Value $versionContent
    Write-Log "VERSION.txt created" -Level Success
}

function Create-ZipPackage {
    Write-Log "Creating ZIP package..." -Level Info
    
    # Compress package
    Compress-Archive -Path "$packagePath\*" -DestinationPath $zipFile -Force
    
    # Calculate checksum
    $checksum = Get-FileHash -Path $zipFile -Algorithm SHA256
    
    # Update VERSION.txt with checksum
    $versionPath = Join-Path $packagePath "VERSION.txt"
    $versionContent = Get-Content $versionPath -Raw
    $versionContent += "`nPackage SHA256: $($checksum.Hash)"
    Set-Content -Path $versionPath -Value $versionContent
    
    # Create checksum file
    $checksumFile = "$zipFile.sha256"
    Set-Content -Path $checksumFile -Value "$($checksum.Hash)  $(Split-Path -Leaf $zipFile)"
    
    # Get file size
    $fileSize = (Get-Item $zipFile).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
    
    Write-Log "ZIP package created: $zipFile" -Level Success
    Write-Log "  Size: $fileSizeMB MB"
    Write-Log "  SHA256: $($checksum.Hash)"
}

function Show-Summary {
    Write-Log ""
    Write-Log "===============================================================================" -Level Info
    Write-Log "Deployment Package Creation Complete!" -Level Success
    Write-Log "===============================================================================" -Level Info
    Write-Log ""
    Write-Log "Package Details:"
    Write-Log "  Name: $packageName"
    Write-Log "  Version: $Version"
    Write-Log "  Configuration: $Configuration"
    Write-Log "  Location: $zipFile"
    Write-Log ""
    Write-Log "Package Contents:"
    Write-Log "  ? API Binaries (ASP.NET Core 10)"
    Write-Log "  ? Web Binaries (ASP.NET Core 10)"
    Write-Log "  ? Client Binaries (.NET Framework 4.8)"
    if (-not $SkipDatabaseScripts) {
        Write-Log "  ? Database Scripts (EF Core migrations)"
    }
    if ($GenerateAzureCertificate) {
        Write-Log "  ? Azure App Registration Certificate"
        Write-Log "    Password: $AzureCertificatePassword"
    }
    Write-Log "  ? Configuration Templates"
    Write-Log "  ? Deployment Scripts"
    Write-Log "  ? Documentation"
    Write-Log "  ? Standalone Client ZIP Package"
    Write-Log ""
    Write-Log "Client Package:"
    Write-Log "  Standalone ZIP: SecureBootWatcher-Client-v$Version.zip"
    Write-Log "  Location: $OutputPath"
    Write-Log "  Use with Deploy-Client.ps1 -PackageZipPath parameter"
    Write-Log ""
    Write-Log "Next Steps:"
    Write-Log "  1. Extract $zipFile on deployment server"
    Write-Log "  2. Review README.md in extracted folder"
    Write-Log "  3. Obtain certificates from Enterprise CA"
    if ($GenerateAzureCertificate) {
        Write-Log "  4. Upload certificates/AzureAppRegistration.cer to Azure Portal"
    }
    Write-Log "  $(if ($GenerateAzureCertificate) { '5' } else { '4' }). Update configuration files with your values"
    Write-Log "  $(if ($GenerateAzureCertificate) { '6' } else { '5' }). Run deployment scripts"
    Write-Log ""
    Write-Log "Log file: $logFile"
    Write-Log ""
}

# ===============================================================================
# Main Execution
# ===============================================================================

try {
    # Create output directory FIRST (before any Write-Log calls)
    if (-not (Test-Path $OutputPath)) {
        New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
    }
    
    Write-Log "===============================================================================" -Level Info
    Write-Log "SecureBootDashboard - Deployment Package Creator v$Version" -Level Info
    Write-Log "===============================================================================" -Level Info
    Write-Log ""
    
    # Step 1: Prerequisites
    $solutionPath = Test-Prerequisites
    
    # Step 2: Initialize package structure
    Initialize-PackageStructure
    
    # Step 3: Build solution
    Build-Solution -SolutionPath $solutionPath
    
    # Step 4: Publish projects
    Publish-ApiProject
    Publish-WebProject
    Publish-ClientProject
    
    # Step 5: Generate database scripts
    Generate-DatabaseScripts
    
    # Step 6: Generate Azure certificate (if requested)
    # NOTE: This must be done BEFORE creating the client ZIP so the certificate can be included
    Generate-AzureAppRegistrationCertificate
    
    # Step 7: Create standalone client package ZIP (after certificate generation)
    Create-ClientPackageZip
    
    # Step 8: Copy configuration templates
    Copy-ConfigurationTemplates
    
    # Step 9: Copy deployment scripts
    Copy-DeploymentScripts
    
    # Step 10: Copy documentation
    Copy-Documentation
    
    # Step 11: Create README and VERSION files
    Create-ReadmeFile
    Create-VersionFile
    
    # Step 12: Create ZIP package
    Create-ZipPackage
    
    # Step 13: Show summary
    Show-Summary
    
    exit 0
    
} catch {
    Write-Log "Deployment package creation failed: $_" -Level Error
    Write-Log "Stack trace: $($_.ScriptStackTrace)" -Level Error
    exit 1
}
