# Server Infrastructure Deployment Guide

## Guida Completa al Deployment dell'Infrastruttura Server SecureBootDashboard

Questa guida fornisce istruzioni dettagliate per il deployment completo dell'infrastruttura server, inclusi database SQL Server, configurazione certificati per autenticazione client (CBA), e deployment dei componenti server.

---

## ?? Indice

1. [Prerequisiti](#prerequisiti)
2. [Preparazione Infrastruttura](#preparazione-infrastruttura)
3. [Database SQL Server](#database-sql-server)
4. [Certificati per Mutual TLS](#certificati-per-mutual-tls)
5. [API Server Deployment](#api-server-deployment)
6. [Web Dashboard Deployment](#web-dashboard-deployment)
7. [Client Deployment](#client-deployment)
8. [Verifica e Testing](#verifica-e-testing)
9. [Troubleshooting](#troubleshooting)
10. [Backup e Disaster Recovery](#backup-e-disaster-recovery)

---

## Prerequisiti

### Software Required

| Componente | Versione Minima | Note |
|------------|-----------------|------|
| Windows Server | 2019+ | 2022 raccomandato |
| SQL Server | 2019+ | SQL Server 2022 o Azure SQL Database |
| .NET Runtime | 10.0+ | Per API e Web Dashboard |
| IIS | 10.0+ | Per hosting Web Dashboard e API |
| PowerShell | 5.1+ | Per script deployment |

### Hardware Recommendations

| Componente | Small (< 500 devices) | Medium (500-2000) | Large (2000-10000) |
|------------|----------------------|-------------------|-------------------|
| **SQL Server** | 4 CPU, 16GB RAM, 100GB SSD | 8 CPU, 32GB RAM, 250GB SSD | 16 CPU, 64GB RAM, 500GB SSD |
| **API Server** | 2 CPU, 4GB RAM | 4 CPU, 8GB RAM | 8 CPU, 16GB RAM |
| **Web Server** | 2 CPU, 4GB RAM | 4 CPU, 8GB RAM | 4 CPU, 16GB RAM |

### Network Requirements

- **Inbound**:
  - TCP 443 (HTTPS) - API ingestion endpoint
  - TCP 443 (HTTPS) - Web Dashboard
  - TCP 1433 (SQL Server) - Solo per server interni
  
- **Outbound**:
  - TCP 443 (HTTPS) - Azure Queue Storage (opzionale)
  - TCP 443 (HTTPS) - Windows Update / Microsoft endpoints

---

## Preparazione Infrastruttura

### Step 1: Preparazione Server

#### SQL Server

```powershell
# Crea directory per database
New-Item -Path "D:\SQLData" -ItemType Directory -Force
New-Item -Path "D:\SQLLogs" -ItemType Directory -Force
New-Item -Path "D:\SQLBackups" -ItemType Directory -Force

# Verifica SQL Server service
Get-Service -Name "MSSQL*" | Format-Table Name, Status, StartType

# Avvia SQL Server se necessario
Start-Service -Name "MSSQLSERVER"
```

#### Web/API Server

```powershell
# Installa IIS e feature necessarie
Install-WindowsFeature -Name Web-Server, `
    Web-WebServer, `
    Web-Common-Http, `
    Web-Default-Doc, `
    Web-Dir-Browsing, `
    Web-Http-Errors, `
    Web-Static-Content, `
    Web-Http-Logging, `
    Web-Request-Monitor, `
    Web-Filtering, `
    Web-Stat-Compression, `
    Web-Dyn-Compression, `
    Web-Mgmt-Console, `
    Web-Asp-Net45 `
    -IncludeManagementTools

# Installa .NET 10 Hosting Bundle
# Download da: https://dotnet.microsoft.com/download/dotnet/10.0
# Esegui installer: dotnet-hosting-10.0.x-win.exe
```

---

## Database SQL Server

### Step 1: Creazione Database

#### Opzione A: Script SQL Diretto

Crea file `Create-Database.sql`:

```sql
-- =============================================================================
-- Secure Boot Dashboard - Database Creation Script
-- =============================================================================
USE master;
GO

DECLARE @Db sysname = N'SecureBootDashboard';
DECLARE @DataPath nvarchar(4000);
DECLARE @LogPath  nvarchar(4000);

-- Prefer SERVERPROPERTY; fallback to registry if needed
SELECT
  @DataPath = TRY_CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)),
  @LogPath  = TRY_CAST(SERVERPROPERTY('InstanceDefaultLogPath')  AS nvarchar(4000));

IF @DataPath IS NULL OR @LogPath IS NULL
BEGIN
  EXEC master.dbo.xp_instance_regread
    N'HKEY_LOCAL_MACHINE',
    N'SOFTWARE\Microsoft\MSSQLServer\MSSQLServer',
    N'DefaultData',
    @DataPath OUTPUT;

  EXEC master.dbo.xp_instance_regread
    N'HKEY_LOCAL_MACHINE',
    N'SOFTWARE\Microsoft\MSSQLServer\MSSQLServer',
    N'DefaultLog',
    @LogPath OUTPUT;
END

-- Normalize trailing slash
SET @DataPath = COALESCE(@DataPath, N'');
SET @LogPath  = COALESCE(@LogPath,  N'');
IF RIGHT(@DataPath,1) NOT IN ('\','/') SET @DataPath += N'\';
IF RIGHT(@LogPath,1)  NOT IN ('\','/') SET @LogPath  += N'\';

DECLARE @Mdf nvarchar(4000) = @DataPath + @Db + N'.mdf';
DECLARE @Ldf nvarchar(4000) = @LogPath  + @Db + N'_log.ldf';

IF DB_ID(@Db) IS NOT NULL
BEGIN
  PRINT 'Database ' + @Db + ' already exists';
END
ELSE
BEGIN
  DECLARE @sql nvarchar(max) =
    N'CREATE DATABASE ' + QUOTENAME(@Db) + N'
      ON PRIMARY (
        NAME = N' + QUOTENAME(@Db + N'_Data', '''') + N',
        FILENAME = N' + QUOTENAME(@Mdf, '''') + N',
        SIZE = 100MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 50MB
      )
      LOG ON (
        NAME = N' + QUOTENAME(@Db + N'_Log', '''') + N',
        FILENAME = N' + QUOTENAME(@Ldf, '''') + N',
        SIZE = 50MB,
        MAXSIZE = 10GB,
        FILEGROWTH = 10MB
      );';

  EXEC (@sql);
  PRINT 'Database ' + @Db + ' created successfully';
END
GO

-- Post-creation settings (unchanged)
ALTER DATABASE [SecureBootDashboard] SET RECOVERY SIMPLE;
GO
ALTER DATABASE [SecureBootDashboard] SET COMPATIBILITY_LEVEL = 150;
GO
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
```

Esegui lo script:

```powershell
# Esegui script SQL
sqlcmd -S localhost -i Create-Database.sql -o Create-Database.log

# Verifica creazione
sqlcmd -S localhost -Q "SELECT name, state_desc, recovery_model_desc FROM sys.databases WHERE name = 'SecureBootDashboard'"
```

#### Opzione B: PowerShell Script

```powershell
# Create-SecureBootDatabase.ps1
param(
    [string]$ServerInstance = "localhost",
    [string]$DatabaseName = "SecureBootDashboard",
    [string]$DataPath = "D:\SQLData",
    [string]$LogPath = "D:\SQLLogs"
)

# Import SQL Server module
Import-Module SqlServer -ErrorAction Stop

# Check if database exists
$dbExists = Invoke-Sqlcmd -ServerInstance $ServerInstance -Query "
    SELECT COUNT(*) as Count 
    FROM sys.databases 
    WHERE name = '$DatabaseName'
"

if ($dbExists.Count -gt 0) {
    Write-Host "Database $DatabaseName already exists" -ForegroundColor Yellow
    exit 0
}

# Create database
$createQuery = @"
CREATE DATABASE [$DatabaseName]
ON PRIMARY (
    NAME = N'${DatabaseName}_Data',
    FILENAME = N'$DataPath\$DatabaseName.mdf',
    SIZE = 100MB,
    MAXSIZE = UNLIMITED,
    FILEGROWTH = 50MB
)
LOG ON (
    NAME = N'${DatabaseName}_Log',
    FILENAME = N'$LogPath\${DatabaseName}_log.ldf',
    SIZE = 50MB,
    MAXSIZE = 10GB,
    FILEGROWTH = 10MB
);

ALTER DATABASE [$DatabaseName] SET RECOVERY SIMPLE;
ALTER DATABASE [$DatabaseName] SET COMPATIBILITY_LEVEL = 150;
"@

Invoke-Sqlcmd -ServerInstance $ServerInstance -Query $createQuery

Write-Host "Database $DatabaseName created successfully" -ForegroundColor Green
```

### Step 2: Applicare Migrations Entity Framework

#### Metodo 1: Automatico (Recommended)

Il database schema viene creato automaticamente al primo avvio dell'API se la connection string è configurata correttamente.

Configura `SecureBootDashboard.Api\appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Al primo avvio, l'API applicherà automaticamente tutte le migrations pendenti.

#### Metodo 2: Manuale con dotnet-ef

```powershell
# Installa EF Core tools
dotnet tool install --global dotnet-ef

# Naviga alla cartella API
cd SecureBootDashboard.Api

# Verifica migrations disponibili
dotnet ef migrations list

# Output atteso:
# 20251105093532_InitialCreate (Pending)
# 20251105101836_AddCertificateCollection (Pending)
# 20251107164106_AddFirmwareReleaseDateToDevice (Pending)
# ... (altre migrations)

# Applica tutte le migrations
dotnet ef database update

# Output atteso:
# Build started...
# Build succeeded.
# Applying migration '20251105093532_InitialCreate'.
# Applying migration '20251105101836_AddCertificateCollection'.
# ...
# Done.
```

#### Metodo 3: SQL Script per Production

Per ambienti production, genera uno script SQL:

```powershell
# Genera script SQL idempotent (può essere eseguito multiple volte)
cd SecureBootDashboard.Api
dotnet ef migrations script --idempotent --output ..\deploy\database\migrations.sql

# Rivedi lo script
notepad ..\deploy\database\migrations.sql

# Esegui lo script su SQL Server
sqlcmd -S SRVSQL -d SecureBootDashboard -i ..\deploy\database\migrations.sql -o migration-apply.log

# Verifica l'applicazione
sqlcmd -S SRVSQL -d SecureBootDashboard -Q "SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId"
```

### Step 3: Verifica Schema Database

```sql
-- Verifica tabelle create
USE SecureBootDashboard;
GO

SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    SUM(p.rows) AS RowCount
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE p.index_id IN (0, 1)
GROUP BY s.name, t.name
ORDER BY s.name, t.name;
GO

-- Verifica migrations applicate
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;
GO
```

**Output atteso:**

```
TableName                           RowCount
---------------------------------   ---------
Devices                             0
SecureBootReports                   0
SecureBootEvents                    0
PendingCommands                     0
WindowsVersions                     0
WindowsBuilds                       0
DeviceCleanupConfig                 1
ApplicationSettings                 12
MutualTlsConfig                     1
TrustedCertificateAuthorities       0
ClientSinkConfig                    1
ApiConfiguration                    1
```

### Step 4: Creazione Login e Permessi

#### Per Windows Authentication (Recommended)

```sql
USE master;
GO

-- Crea login per API service account
CREATE LOGIN [DOMAIN\SecureBootAPI_ServiceAccount] FROM WINDOWS;
GO

USE SecureBootDashboard;
GO

-- Crea user
CREATE USER [DOMAIN\SecureBootAPI_ServiceAccount] FOR LOGIN [DOMAIN\SecureBootAPI_ServiceAccount];
GO

-- Assegna permessi
ALTER ROLE db_datareader ADD MEMBER [DOMAIN\SecureBootAPI_ServiceAccount];
ALTER ROLE db_datawriter ADD MEMBER [DOMAIN\SecureBootAPI_ServiceAccount];
ALTER ROLE db_ddladmin ADD MEMBER [DOMAIN\SecureBootAPI_ServiceAccount];
GO

PRINT 'Permissions granted successfully';
GO
```

#### Per SQL Authentication

```sql
USE master;
GO

-- Crea login SQL
CREATE LOGIN [SecureBootAPI_User] WITH PASSWORD = 'YourStrongPassword!123';
GO

USE SecureBootDashboard;
GO

-- Crea user
CREATE USER [SecureBootAPI_User] FOR LOGIN [SecureBootAPI_User];
GO

-- Assegna permessi
ALTER ROLE db_datareader ADD MEMBER [SecureBootAPI_User];
ALTER ROLE db_datawriter ADD MEMBER [SecureBootAPI_User];
ALTER ROLE db_ddladmin ADD MEMBER [SecureBootAPI_User];
GO
```

Connection string per SQL Authentication:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=SRVSQL;Database=SecureBootDashboard;User Id=SecureBootAPI_User;Password=YourStrongPassword!123;TrustServerCertificate=True;Encrypt=True"
  }
}
```

---

## Certificati per Mutual TLS

### Perché Mutual TLS?

Mutual TLS (mTLS) fornisce:
- **Autenticazione client forte**: Ogni client deve presentare un certificato valido
- **Crittografia end-to-end**: Tutto il traffico è cifrato con TLS 1.2+
- **Non-repudiation**: Logging completo di chi ha inviato ogni richiesta
- **Zero-trust security**: Nessun client può connettersi senza certificato valido

### Architettura Certificati

```
Root CA (SecureBootWatcher Root CA)
  ??? Client Certificate (Clients SecureBootWatcher)
  ?   ??? Installato su ogni workstation client
  ??? Client Certificate (Web Dashboard)
  ?   ??? Installato su web server
  ??? Server Certificate (API Server)
      ??? Installato su API server
```

### Step 1: Generazione Root CA

#### Opzione A: PowerShell (Self-Signed per Testing)

```powershell
# Create-RootCA.ps1
# ATTENZIONE: Solo per ambienti TEST/DEV!

# Crea root CA certificate
$rootCert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootWatcher Root CA, O=Your Organization, C=IT" `
    -KeyExportPolicy Exportable `
    -KeyUsage CertSign,CRLSign,DigitalSignature `
    -KeyLength 4096 `
    -NotAfter (Get-Date).AddYears(10) `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256 `
    -KeyAlgorithm RSA

Write-Host "Root CA Created:" -ForegroundColor Green
Write-Host "  Thumbprint: $($rootCert.Thumbprint)"
Write-Host "  Subject: $($rootCert.Subject)"
Write-Host "  Valid Until: $($rootCert.NotAfter)"

# Export root CA certificate (senza private key)
$rootCertPath = "C:\Certificates\SecureBootWatcher-RootCA.cer"
Export-Certificate -Cert $rootCert -FilePath $rootCertPath -Force

Write-Host "`nRoot CA exported to: $rootCertPath" -ForegroundColor Green

# Export root CA con private key (PROTEGGERE QUESTO FILE!)
$rootPfxPath = "C:\Certificates\SecureBootWatcher-RootCA.pfx"
$rootPassword = ConvertTo-SecureString -String "YourRootCAPassword!123" -Force -AsPlainText
Export-PfxCertificate -Cert $rootCert -FilePath $rootPfxPath -Password $rootPassword -Force

Write-Host "Root CA (with private key) exported to: $rootPfxPath" -ForegroundColor Yellow
Write-Host "??  PROTEGGERE QUESTO FILE! Contiene la chiave privata della CA" -ForegroundColor Red

# Installa root CA nel Trusted Root store
Import-Certificate -FilePath $rootCertPath -CertStoreLocation "Cert:\LocalMachine\Root" -Verbose

Write-Host "`n? Root CA installed in Trusted Root Certification Authorities" -ForegroundColor Green
```

#### Opzione B: OpenSSL (Per Production)

```bash
#!/bin/bash
# create-root-ca.sh

# Crea directory struttura
mkdir -p /etc/pki/secureboot/{certs,private,csr}
cd /etc/pki/secureboot

# Genera root CA private key (4096-bit RSA)
openssl genrsa -aes256 -out private/root-ca.key 4096
chmod 400 private/root-ca.key

# Genera root CA certificate (10 anni)
openssl req -x509 -new -nodes -key private/root-ca.key \
    -sha256 -days 3650 -out certs/root-ca.crt \
    -subj "/C=IT/ST=Lazio/L=Rome/O=Your Organization/CN=SecureBootWatcher Root CA"

# Verifica certificato
openssl x509 -in certs/root-ca.crt -text -noout

echo "? Root CA created successfully"
echo "Certificate: /etc/pki/secureboot/certs/root-ca.crt"
echo "Private Key: /etc/pki/secureboot/private/root-ca.key"
```

#### Opzione C: Active Directory Certificate Services (Enterprise)

Se disponete di AD CS:

1. Apri Certification Authority console
2. Right-click su CA name ? Properties
3. Extensions tab ? Verifica CRL Distribution Points configurati
4. Policy Module ? Configura template per client certificates

### Step 2: Generazione Client Certificates

#### PowerShell Script

```powershell
# Create-ClientCertificates.ps1
param(
    [string]$RootCertThumbprint = "PASTE_ROOT_CA_THUMBPRINT_HERE",
    [int]$ValidityYears = 2,
    [string]$OutputPath = "C:\Certificates"
)

# Carica root CA certificate
$rootCert = Get-ChildItem -Path "Cert:\LocalMachine\My" | 
    Where-Object { $_.Thumbprint -eq $RootCertThumbprint }

if (-not $rootCert) {
    throw "Root CA certificate not found with thumbprint: $RootCertThumbprint"
}

Write-Host "Using Root CA: $($rootCert.Subject)" -ForegroundColor Cyan

# Crea directory output
New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null

# ==============================================================================
# Client Certificate per SecureBootWatcher Client
# ==============================================================================

Write-Host "`nGenerating Client Certificate for SecureBootWatcher Client..." -ForegroundColor Yellow

$clientCert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootWatcher-Client, O=Your Organization, C=IT" `
    -Signer $rootCert `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2") `
    -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears($ValidityYears) `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256

Write-Host "? Client Certificate Created:" -ForegroundColor Green
Write-Host "  Thumbprint: $($clientCert.Thumbprint)"
Write-Host "  Subject: $($clientCert.Subject)"
Write-Host "  Valid Until: $($clientCert.NotAfter)"

# Export client certificate con private key
$clientPfxPath = Join-Path $OutputPath "SecureBootWatcher-Client.pfx"
$clientPassword = ConvertTo-SecureString -String "ClientCertPassword!123" -Force -AsPlainText
Export-PfxCertificate -Cert $clientCert -FilePath $clientPfxPath -Password $clientPassword -Force | Out-Null

Write-Host "  Exported to: $clientPfxPath" -ForegroundColor Green

# Salva thumbprint in file per reference
$thumbprintFile = Join-Path $OutputPath "client-thumbprints.txt"
Add-Content -Path $thumbprintFile -Value "Client Certificate Thumbprint: $($clientCert.Thumbprint)"
Add-Content -Path $thumbprintFile -Value "Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Content -Path $thumbprintFile -Value "Valid Until: $($clientCert.NotAfter.ToString('yyyy-MM-dd'))"
Add-Content -Path $thumbprintFile -Value ""

# ==============================================================================
# Client Certificate per Web Dashboard
# ==============================================================================

Write-Host "`nGenerating Client Certificate for Web Dashboard..." -ForegroundColor Yellow

$webCert = New-SelfSignedCertificate `
    -Subject "CN=SecureBootDashboard-Web, O=Your Organization, C=IT" `
    -Signer $rootCert `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2") `
    -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears($ValidityYears) `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -HashAlgorithm SHA256

Write-Host "? Web Certificate Created:" -ForegroundColor Green
Write-Host "  Thumbprint: $($webCert.Thumbprint)"
Write-Host "  Subject: $($webCert.Subject)"
Write-Host "  Valid Until: $($webCert.NotAfter)"

# Export web certificate con private key
$webPfxPath = Join-Path $OutputPath "SecureBootDashboard-Web.pfx"
$webPassword = ConvertTo-SecureString -String "WebCertPassword!123" -Force -AsPlainText
Export-PfxCertificate -Cert $webCert -FilePath $webPfxPath -Password $webPassword -Force | Out-Null

Write-Host "  Exported to: $webPfxPath" -ForegroundColor Green

# Salva thumbprint
Add-Content -Path $thumbprintFile -Value "Web Dashboard Certificate Thumbprint: $($webCert.Thumbprint)"
Add-Content -Path $thumbprintFile -Value "Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Content -Path $thumbprintFile -Value "Valid Until: $($webCert.NotAfter.ToString('yyyy-MM-dd'))"

# ==============================================================================
# Summary
# ==============================================================================

Write-Host "`n======================================" -ForegroundColor Cyan
Write-Host "Certificate Generation Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "`nCertificates exported to: $OutputPath"
Write-Host "`nClient Certificate:"
Write-Host "  File: SecureBootWatcher-Client.pfx"
Write-Host "  Password: ClientCertPassword!123"
Write-Host "  Thumbprint: $($clientCert.Thumbprint)"
Write-Host "`nWeb Certificate:"
Write-Host "  File: SecureBootDashboard-Web.pfx"
Write-Host "  Password: WebCertPassword!123"
Write-Host "  Thumbprint: $($webCert.Thumbprint)"
Write-Host "`nThumbprints saved to: $thumbprintFile"
Write-Host "`n??  Store passwords securely!" -ForegroundColor Yellow
```

Esegui lo script:

```powershell
# Trova root CA thumbprint
$rootThumbprint = (Get-ChildItem -Path "Cert:\LocalMachine\My" | 
    Where-Object { $_.Subject -like "*SecureBootWatcher Root CA*" }).Thumbprint

Write-Host "Root CA Thumbprint: $rootThumbprint"

# Genera client certificates
.\Create-ClientCertificates.ps1 -RootCertThumbprint $rootThumbprint -OutputPath "C:\Certificates"
```

### Step 3: Distribuzione Certificati

#### Su API Server

```powershell
# Import root CA nel Trusted Root store
$rootCertPath = "C:\Certificates\SecureBootWatcher-RootCA.cer"
Import-Certificate -FilePath $rootCertPath -CertStoreLocation "Cert:\LocalMachine\Root" -Verbose

Write-Host "? Root CA installed on API server" -ForegroundColor Green

# Verifica installazione
Get-ChildItem -Path "Cert:\LocalMachine\Root" | 
    Where-Object { $_.Subject -like "*SecureBootWatcher Root CA*" } | 
    Format-List Subject, Thumbprint, NotAfter
```

#### Su Web Server

```powershell
# Import root CA
Import-Certificate -FilePath "C:\Certificates\SecureBootWatcher-RootCA.cer" `
    -CertStoreLocation "Cert:\LocalMachine\Root" -Verbose

# Import web client certificate
$webPfxPath = "C:\Certificates\SecureBootDashboard-Web.pfx"
$webPassword = ConvertTo-SecureString -String "WebCertPassword!123" -Force -AsPlainText
Import-PfxCertificate -FilePath $webPfxPath `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -Password $webPassword -Exportable -Verbose

# Grant IIS AppPool read access to private key
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | 
    Where-Object { $_.Subject -like "*SecureBootDashboard-Web*" }

$rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$fileName = $rsaCert.Key.UniqueName
$path = "$env:ALLUSERSPROFILE\Microsoft\Crypto\Keys\$fileName"

# Grant read permission to IIS AppPool identity
$appPoolIdentity = "IIS AppPool\SecureBootDashboard"
$permissions = Get-Acl -Path $path
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $appPoolIdentity, "Read", "Allow"
)
$permissions.AddAccessRule($rule)
Set-Acl -Path $path -AclObject $permissions

Write-Host "? Web certificate installed and permissions granted" -ForegroundColor Green
```

#### Su Client Workstations

```powershell
# Deploy-ClientCertificate.ps1
# Questo script può essere distribuito via GPO, SCCM, o Intune

param(
    [string]$CertPath = "\\fileserver\Certificates\SecureBootWatcher-Client.pfx",
    [string]$CertPassword = "ClientCertPassword!123"
)

try {
    # Import root CA
    $rootPath = "\\fileserver\Certificates\SecureBootWatcher-RootCA.cer"
    Import-Certificate -FilePath $rootPath `
        -CertStoreLocation "Cert:\LocalMachine\Root" -ErrorAction Stop

    # Import client certificate
    $password = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
    $cert = Import-PfxCertificate -FilePath $CertPath `
        -CertStoreLocation "Cert:\LocalMachine\My" `
        -Password $password -Exportable -ErrorAction Stop

    # Grant NETWORK SERVICE read access (se client runs as service)
    $rsaCert = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    $fileName = $rsaCert.Key.UniqueName
    $keyPath = "$env:ALLUSERSPROFILE\Microsoft\Crypto\Keys\$fileName"
    
    $acl = Get-Acl -Path $keyPath
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "NETWORK SERVICE", "Read", "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl -Path $keyPath -AclObject $acl

    Write-Host "? Client certificate installed successfully" -ForegroundColor Green
    Write-Host "  Thumbprint: $($cert.Thumbprint)"
    
    exit 0
} catch {
    Write-Error "Failed to install certificate: $_"
    exit 1
}
```

---

## API Server Deployment

### Step 1: Build API

```powershell
# Build-Api.ps1
param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "C:\Deploy\SecureBootDashboard.Api"
)

# Naviga alla cartella API
Set-Location -Path "SecureBootDashboard.Api"

# Clean
dotnet clean --configuration $Configuration

# Restore packages
dotnet restore

# Build
dotnet build --configuration $Configuration --no-restore

# Publish
dotnet publish --configuration $Configuration `
    --output $OutputPath `
    --no-build `
    --self-contained false `
    --runtime win-x64

Write-Host "? API published to: $OutputPath" -ForegroundColor Green
```

### Step 2: Configurazione appsettings.json

Crea `appsettings.Production.json` nella cartella di deployment:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=SRVSQL;Database=SecureBootDashboard;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True"
  },
  "Urls": "https://0.0.0.0:5001;http://0.0.0.0:5000",
  "WebAppUrl": "https://dashboard.yourdomain.com",
  "MutualTls": {
    "Enabled": true,
    "AllowSelfSignedCertificates": false,
    "AllowedIssuers": ["SecureBootWatcher Root CA"],
    "CheckCertificateRevocation": true,
    "ValidateCertificateChain": true
  },
  "QueueProcessor": {
    "Enabled": true,
    "QueueServiceUri": "https://yourstorageaccount.queue.core.windows.net",
    "QueueName": "secureboot-reports",
    "AuthenticationMethod": "Certificate",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "CertificateThumbprint": "your-cert-thumbprint",
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
          "path": "C:\\Logs\\SecureBootDashboard\\api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

### Step 3: Setup IIS

```powershell
# Deploy-ApiToIIS.ps1
param(
    [string]$SiteName = "SecureBootDashboard.Api",
    [string]$AppPoolName = "SecureBootDashboard.Api",
    [string]$PhysicalPath = "C:\inetpub\SecureBootDashboard.Api",
    [string]$HostHeader = "api.yourdomain.com",
    [int]$HttpsPort = 443,
    [string]$CertificateThumbprint = "YOUR_SSL_CERT_THUMBPRINT"
)

Import-Module WebAdministration

# Crea Application Pool
if (Test-Path "IIS:\AppPools\$AppPoolName") {
    Write-Host "AppPool $AppPoolName already exists" -ForegroundColor Yellow
} else {
    New-WebAppPool -Name $AppPoolName
    
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "managedRuntimeVersion" -Value ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "processModel.identityType" -Value "NetworkService"
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "startMode" -Value "AlwaysRunning"
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "autoStart" -Value $true
    
    Write-Host "? AppPool $AppPoolName created" -ForegroundColor Green
}

# Copia file deployment
if (Test-Path $PhysicalPath) {
    Write-Host "Removing existing deployment..." -ForegroundColor Yellow
    Remove-Item -Path $PhysicalPath -Recurse -Force
}

Copy-Item -Path "C:\Deploy\SecureBootDashboard.Api\*" -Destination $PhysicalPath -Recurse -Force
Write-Host "? Files copied to $PhysicalPath" -ForegroundColor Green

# Crea Website
if (Test-Path "IIS:\Sites\$SiteName") {
    Write-Host "Site $SiteName already exists - removing..." -ForegroundColor Yellow
    Remove-WebSite -Name $SiteName
}

New-WebSite -Name $SiteName `
    -PhysicalPath $PhysicalPath `
    -ApplicationPool $AppPoolName `
    -HostHeader $HostHeader `
    -Port $HttpsPort `
    -Ssl

# Bind SSL certificate
$binding = Get-WebBinding -Name $SiteName -Protocol "https"
$binding.AddSslCertificate($CertificateThumbprint, "my")

Write-Host "? Website $SiteName created and SSL certificate bound" -ForegroundColor Green

# Configure request limits
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" `
    -Filter "system.webServer/security/requestFiltering/requestLimits" `
    -Name "maxAllowedContentLength" -Value 104857600  # 100 MB

Write-Host "? Request limits configured" -ForegroundColor Green

# Start website
Start-WebSite -Name $SiteName
Write-Host "? Website started" -ForegroundColor Green

# Test
Start-Sleep -Seconds 5
$response = Invoke-WebRequest -Uri "https://$HostHeader/health" -UseBasicParsing
if ($response.StatusCode -eq 200) {
    Write-Host "? API is responding correctly" -ForegroundColor Green
} else {
    Write-Warning "API health check returned: $($response.StatusCode)"
}
```

---

## Web Dashboard Deployment

### Step 1: Build Web App

```powershell
# Build-Web.ps1
param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "C:\Deploy\SecureBootDashboard.Web"
)

Set-Location -Path "SecureBootDashboard.Web"

dotnet clean --configuration $Configuration
dotnet restore
dotnet build --configuration $Configuration --no-restore
dotnet publish --configuration $Configuration `
    --output $OutputPath `
    --no-build `
    --self-contained false `
    --runtime win-x64

Write-Host "? Web Dashboard published to: $OutputPath" -ForegroundColor Green
```

### Step 2: Configurazione

`appsettings.Production.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://api.yourdomain.com",
    "UseCertificateAuth": true,
    "CertificateThumbprint": "WEB_CLIENT_CERT_THUMBPRINT",
    "CertificateStoreLocation": "LocalMachine",
    "CertificateStoreName": "My"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
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
```

### Step 3: Deploy to IIS

```powershell
# Deploy-WebToIIS.ps1
# Similar to API deployment script
# Use different site name, app pool, and physical path
```

---

## Client Deployment

Vedi documentazione dettagliata: `CLIENT_DEPLOYMENT.md`

### Quick Start via SCCM

```powershell
# Crea Application in SCCM
$packageSource = "\\fileserver\Software\SecureBootWatcher\Client"

# Deploy tramite Application Deployment
# Detection method: File exists at C:\Program Files\SecureBootWatcher\SecureBootWatcher.Client.exe
# Installation command: powershell.exe -ExecutionPolicy Bypass -File .\Install-Client.ps1
```

---

## Verifica e Testing

### Database Health Check

```sql
-- Verifica connessioni attive
SELECT 
    DB_NAME(dbid) as DBName,
    COUNT(dbid) as NumberOfConnections,
    loginame as LoginName
FROM sys.sysprocesses
WHERE dbid > 0
GROUP BY dbid, loginame
HAVING DB_NAME(dbid) = 'SecureBootDashboard';

-- Verifica device count
SELECT COUNT(*) as TotalDevices FROM Devices;

-- Verifica ultimi report
SELECT TOP 10 * FROM SecureBootReports ORDER BY CreatedAtUtc DESC;
```

### API Health Check

```powershell
# Test health endpoint
Invoke-RestMethod -Uri "https://api.yourdomain.com/health" -Method Get

# Test with client certificate
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | 
    Where-Object { $_.Thumbprint -eq "CLIENT_CERT_THUMBPRINT" }
    
Invoke-RestMethod -Uri "https://api.yourdomain.com/api/Devices" `
    -Method Get `
    -Certificate $cert
```

### Web Dashboard Check

```powershell
# Test web dashboard
Start-Process "https://dashboard.yourdomain.com"
```

---

## Troubleshooting

### Database Connection Issues

```powershell
# Test SQL connectivity
Test-NetConnection -ComputerName SRVSQL -Port 1433

# Test authentication
sqlcmd -S SRVSQL -d SecureBootDashboard -Q "SELECT @@VERSION"
```

### Certificate Issues

```powershell
# Verifica certificato installato
Get-ChildItem -Path "Cert:\LocalMachine\My" | 
    Where-Object { $_.Subject -like "*SecureBootWatcher*" } | 
    Format-List Subject, Thumbprint, NotAfter, HasPrivateKey

# Test certificate private key access
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My\THUMBPRINT"
$cert.HasPrivateKey  # Should be True
```

### IIS Application Pool Crashes

```powershell
# Check Event Viewer
Get-EventLog -LogName Application -Source "ASP.NET Core*" -Newest 20

# Check stdout log (se configurato)
Get-Content "C:\inetpub\SecureBootDashboard.Api\logs\stdout.log" -Tail 50
```

---

## Backup e Disaster Recovery

### Database Backup

```sql
-- Full backup giornaliero
BACKUP DATABASE [SecureBootDashboard]
TO DISK = 'D:\SQLBackups\SecureBootDashboard_Full.bak'
WITH INIT, COMPRESSION, STATS = 10;

-- Differential backup ogni 6 ore
BACKUP DATABASE [SecureBootDashboard]
TO DISK = 'D:\SQLBackups\SecureBootDashboard_Diff.bak'
WITH DIFFERENTIAL, INIT, COMPRESSION;

-- Transaction log backup ogni ora (se FULL recovery model)
BACKUP LOG [SecureBootDashboard]
TO DISK = 'D:\SQLBackups\SecureBootDashboard_Log.trn'
WITH INIT, COMPRESSION;
```

### Automated Backup Script

```powershell
# Backup-SecureBootDatabase.ps1
param(
    [string]$ServerInstance = "SRVSQL",
    [string]$DatabaseName = "SecureBootDashboard",
    [string]$BackupPath = "D:\SQLBackups",
    [string]$RetentionDays = "30"
)

Import-Module SqlServer

# Full backup
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = Join-Path $BackupPath "$DatabaseName_Full_$timestamp.bak"

Backup-SqlDatabase -ServerInstance $ServerInstance `
    -Database $DatabaseName `
    -BackupFile $backupFile `
    -CompressionOption On

# Cleanup old backups
Get-ChildItem -Path $BackupPath -Filter "$DatabaseName_*.bak" | 
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) } | 
    Remove-Item -Force

Write-Host "? Backup completed: $backupFile" -ForegroundColor Green
```

### Application Files Backup

```powershell
# Backup-ApplicationFiles.ps1
$backupPath = "D:\Backups\SecureBootDashboard"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Backup API files
Compress-Archive -Path "C:\inetpub\SecureBootDashboard.Api\*" `
    -DestinationPath "$backupPath\API_$timestamp.zip" -Force

# Backup Web files
Compress-Archive -Path "C:\inetpub\SecureBootDashboard.Web\*" `
    -DestinationPath "$backupPath\Web_$timestamp.zip" -Force

# Backup certificates
Compress-Archive -Path "C:\Certificates\*" `
    -DestinationPath "$backupPath\Certificates_$timestamp.zip" -Force

Write-Host "? Application files backed up" -ForegroundColor Green
```

---

## Next Steps

1. ? **Configura Monitoring**: Azure Monitor, Application Insights
2. ? **Setup Alerting**: Email alerts per failures
3. ? **Performance Tuning**: SQL indexing, caching
4. ? **Security Hardening**: Firewall rules, WAF
5. ? **Documentation**: Internal runbooks

---

## Related Documentation

- [CLIENT_DEPLOYMENT.md](CLIENT_DEPLOYMENT.md) - Client deployment guide
- [MUTUAL_TLS_CONFIGURATION.md](MUTUAL_TLS_CONFIGURATION.md) - Detailed mTLS setup
- [COMMAND_MANAGEMENT_USER_GUIDE.md](COMMAND_MANAGEMENT_USER_GUIDE.md) - User guide
- [DATABASE_MIGRATION_FIX.md](DATABASE_MIGRATION_FIX.md) - Database troubleshooting

---

**Document Version**: 1.0  
**Last Updated**: 2025-01-14  
**Classification**: Internal - IT Infrastructure
