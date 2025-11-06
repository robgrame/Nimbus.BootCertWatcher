# ?? Serilog + Nerdbank.GitVersioning Implementation Guide

## ? Implementazione Completata

### 1. **Serilog sul Client (.NET Framework 4.8)**
- ? Serilog 4.3.0 installato
- ? Serilog.Sinks.Console 6.1.1
- ? Serilog.Sinks.File 7.0.0
- ? Serilog.Extensions.Logging 9.0.2 per integrazione con Microsoft.Extensions.Logging
- ? Log su Console + File rotanti giornalieri
- ? Retention 30 giorni

### 2. **Nerdbank.GitVersioning su Tutti i Progetti**
- ? Tool globale `nbgv` installato
- ? Versione iniziale: **1.1.0**
- ? Auto-increment build number ad ogni commit
- ? Supporto branch `main` per release pubbliche

---

## ?? Versioning Schema

### Formato Version String

```
1.1.0.{height}+{commit-id}
? ? ?   ?         ?
? ? ?   ?         ?? Commit SHA abbreviato (es. b87bb0c6cc)
? ? ?   ???????????? Build number (numero di commit da version tag)
? ? ???????????????? Patch (incremento manuale per hotfix)
? ?????????????????? Minor (incremento manuale per nuove feature)
???????????????????? Major (incremento manuale per breaking changes)
```

### Esempio Output

```
Version:                      1.1.0.47227
AssemblyVersion:              1.1.0.0
AssemblyInformationalVersion: 1.1.0+b87bb0c6cc
NuGetPackageVersion:          1.1.0-gb87bb0c6cc
```

### Dove Appare la Versione

| Assembly | Versione Visualizzata | Esempio |
|----------|----------------------|---------|
| **Client** | `AssemblyInformationalVersion` | `1.1.0+b87bb0c6cc` |
| **API** | `AssemblyInformationalVersion` | `1.1.0+b87bb0c6cc` |
| **Web** | `AssemblyInformationalVersion` | `1.1.0+b87bb0c6cc` |
| **NuGet Packages** | `NuGetPackageVersion` | `1.1.0-gb87bb0c6cc` |

---

## ?? Configurazione Nerdbank.GitVersioning

### `version.json` (Root della Repository)

```json
{
  "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/main/src/NerdBank.GitVersioning/version.schema.json",
  "version": "1.1",
  "publicReleaseRefSpec": [
    "^refs/heads/master$",
    "^refs/heads/v\\d+(?:\\.\\d+)?$"
  ],
  "cloudBuild": {
    "buildNumber": {
      "enabled": true
    }
  }
}
```

### Comportamento

- **Branch `main`**: Version `1.1.0.{height}` (public release)
- **Altri branch**: Version `1.1.0-alpha.{height}` (prerelease)
- **Height**: Numero di commit dal tag version più recente
- **Commit ID**: Aggiunto automaticamente nei metadati (+b87bb0c6cc)

---

## ?? Serilog Configuration - Client

### Output Example

```
[12:45:30 INF] ========================================
[12:45:30 INF] SecureBootWatcher Client Starting
[12:45:30 INF] ========================================
[12:45:30 INF] Version: 1.1.0+b87bb0c6cc
[12:45:30 INF] Base Directory: C:\Program Files\SecureBootWatcher
[12:45:30 INF] Current Directory: C:\Program Files\SecureBootWatcher
[12:45:30 INF] Machine Name: W11DEV
[12:45:30 INF] Domain: MSINTUNE
[12:45:30 INF] User: Administrator
[12:45:30 INF] .NET Framework: 4.0.30319.42000
[12:45:30 INF] OS: Microsoft Windows NT 10.0.22631.0
[12:45:30 INF] Log Path: C:\Program Files\SecureBootWatcher\logs\client-20251106.log
[12:45:30 INF] Configuration Files:
[12:45:30 INF]   appsettings.json: Found
[12:45:30 INF]   appsettings.local.json: Not Found
[12:45:30 INF] ========================================
[12:45:30 INF] Configuration:
[12:45:30 INF] ========================================
[12:45:30 INF] Fleet ID: mslabs
[12:45:30 INF] Registry Poll Interval: 00:30:00
[12:45:30 INF] Event Query Interval: 00:30:00
[12:45:30 INF] Event Lookback Period: 1.00:00:00
[12:45:30 INF] Event Channels: 2
[12:45:30 INF]   - Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin
[12:45:30 INF]   - Microsoft-Windows-CodeIntegrity/Operational
[12:45:30 INF] ----------------------------------------
[12:45:30 INF] Sink Configuration:
[12:45:30 INF]   Execution Strategy: StopOnFirstSuccess
[12:45:30 INF]   Sink Priority: WebApi,AzureQueue,FileShare
[12:45:30 INF]   File Share Sink: Disabled
[12:45:30 INF]   Azure Queue Sink: Disabled
[12:45:30 INF]   Web API Sink: Enabled
[12:45:30 INF]     Base Address: https://srvcm00.msintune.lab:5001
[12:45:30 INF]     Ingestion Route: /api/SecureBootReports
[12:45:30 INF]     HTTP Timeout: 00:00:30
[12:45:30 INF] ========================================
[12:45:30 INF] Active Sinks: WebApi
[12:45:30 INF] ========================================
```

### Log File Location

- **Console**: Standard output (in real-time)
- **File**: `{AppBaseDirectory}\logs\client-{Date}.log`
  - Example: `C:\Program Files\SecureBootWatcher\logs\client-20251106.log`
  - Rolling: Daily
  - Retention: 30 days
  - Encoding: UTF-8

### Log Levels

| Level | Descrizione | Esempio |
|-------|-------------|---------|
| `Information` | Normal operations | Client startup, report sent |
| `Warning` | Non-critical issues | No sinks enabled, certificate expiring soon |
| `Error` | Recoverable errors | Failed to send report, retry scheduled |
| `Fatal` | Critical failures | Unhandled exception, application crash |

---

## ?? Comandi Nerdbank.GitVersioning

### Get Current Version

```powershell
nbgv get-version
```

Output:
```
Version:                      1.1.0.47227
AssemblyVersion:              1.1.0.0
AssemblyInformationalVersion: 1.1.0+b87bb0c6cc
NuGetPackageVersion:          1.1.0-gb87bb0c6cc
```

### Increment Version

```powershell
# Increment Minor (1.1 ? 1.2)
nbgv set-version 1.2

# Increment Major (1.1 ? 2.0)
nbgv set-version 2.0

# Set Patch (1.1.0 ? 1.1.1)
nbgv set-version 1.1.1
```

### Tag a Release

```powershell
# Create version tag
nbgv tag

# Push tag to remote
git push origin v1.1.0
```

### Get Version in CI/CD

```yaml
# Azure Pipelines
steps:
- task: UseDotNet@2
- script: |
    dotnet tool install --global nbgv
    nbgv cloud
  displayName: 'Set Version'
```

---

## ?? Project Files Modified

### Client Project (SecureBootWatcher.Client.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Serilog -->
    <PackageReference Include="Serilog" Version="4.3.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.1.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
    <PackageReference Include="Serilog.Extensions.Logging" Version="9.0.2" />
    
    <!-- Nerdbank.GitVersioning (via Directory.Build.props) -->
    <!-- Managed centrally for all projects -->
  </ItemGroup>
</Project>
```

### Directory.Build.props (Auto-created by nbgv)

```xml
<Project>
  <ItemGroup>
    <PackageReference Include="Nerdbank.GitVersioning" Version="3.9.50">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

---

## ?? Retrieving Version at Runtime

### In Code (All Projects)

```csharp
using System.Reflection;

var assembly = Assembly.GetExecutingAssembly();
var version = assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "Unknown";

Console.WriteLine($"Version: {version}");
// Output: Version: 1.1.0+b87bb0c6cc
```

### In API Response

```csharp
[HttpGet("version")]
public IActionResult GetVersion()
{
    var version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "Unknown";
    
    return Ok(new { version });
}
```

**Response:**
```json
{
  "version": "1.1.0+b87bb0c6cc"
}
```

---

## ?? Versioning Strategy

### Semantic Versioning (SemVer)

```
MAJOR.MINOR.PATCH
  ?     ?      ?
  ?     ?      ?? Bug fixes, security patches
  ?     ????????? New features, backward compatible
  ??????????????? Breaking changes
```

### When to Increment

| Change Type | Version Change | Example |
|-------------|---------------|---------|
| Bug fix | Patch (`1.1.0` ? `1.1.1`) | Fix certificate validation |
| New feature (compatible) | Minor (`1.1.0` ? `1.2.0`) | Add new sink type |
| Breaking change | Major (`1.1.0` ? `2.0.0`) | Change API contract |
| Daily commit | Build (`1.1.0.100` ? `1.1.0.101`) | Automatic |

### Example Timeline

```
Commit 1:  1.1.0.1    (Initial commit after version tag)
Commit 2:  1.1.0.2    (Bug fix)
Commit 3:  1.1.0.3    (Refactoring)
Tag:       1.2.0      (New feature released)
Commit 4:  1.2.0.1    (First commit after 1.2.0 tag)
Commit 5:  1.2.0.2    (Another commit)
Tag:       2.0.0      (Breaking change released)
Commit 6:  2.0.0.1    (First commit after 2.0.0 tag)
```

---

## ?? Benefits

### Serilog

? **Structured Logging**: Structured data invece di string interpolation  
? **Performance**: Async writes, buffering  
? **Flexibility**: Multiple sinks (Console + File + future: Azure App Insights)  
? **Configuration**: Minimal code, maximum flexibility  
? **Compatibility**: .NET Framework 4.8 + .NET 8

### Nerdbank.GitVersioning

? **Automatic Build Numbers**: No manual version management  
? **Git Integration**: Version derivata dal repository  
? **Deterministic**: Same commit = same version  
? **CI/CD Ready**: Azure Pipelines, GitHub Actions support  
? **Zero Configuration**: Works out of the box

---

## ?? Quick Reference

### Check Current Version

```powershell
nbgv get-version
```

### Bump Version

```powershell
# Minor version (1.1 ? 1.2)
nbgv set-version 1.2

# Major version (1.1 ? 2.0)
nbgv set-version 2.0

# Patch version (1.1.0 ? 1.1.1)
nbgv set-version 1.1.1
```

### View Client Logs

```powershell
# View latest log
Get-Content "C:\Program Files\SecureBootWatcher\logs\client-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50 -Wait

# Search for errors
Select-String -Path "C:\Program Files\SecureBootWatcher\logs\client-*.log" -Pattern "\[ERR\]|\[FTL\]"
```

### Test Client with Serilog

```powershell
cd SecureBootWatcher.Client\bin\Debug\net48
.\SecureBootWatcher.Client.exe

# Output should show:
# [12:45:30 INF] ========================================
# [12:45:30 INF] SecureBootWatcher Client Starting
# [12:45:30 INF] ========================================
# [12:45:30 INF] Version: 1.1.0+b87bb0c6cc
# ...
```

---

## ?? Troubleshooting

### Version Shows "Unknown"

**Problem**: `AssemblyInformationalVersion` not found

**Solution**:
```powershell
# Rebuild project
dotnet clean
dotnet build

# Verify version generated
nbgv get-version
```

### Logs Not Created

**Problem**: Log files not appearing

**Diagnostics**:
```csharp
// Check log path
Log.Information("Log Path: {LogPath}", Path.GetFullPath(logPath));

// Verify directory exists
if (!Directory.Exists(Path.GetDirectoryName(logPath)))
{
    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
}
```

### Version Not Auto-Incrementing

**Problem**: Build number stays the same

**Cause**: No new commits

**Solution**:
```powershell
# Make a commit
git commit -m "Update feature"

# Verify version changed
nbgv get-version
# Height should increase
```

---

## ?? Implementation Complete!

? **Serilog** implementato sul Client .NET Framework 4.8  
? **Nerdbank.GitVersioning** configurato per tutta la solution  
? **Versioning automatico** ad ogni commit  
? **Logging strutturato** con retention 30 giorni  
? **Version 1.1.0** come baseline

**Prossimo commit incrementerà automaticamente la build version!**

**Example**: `1.1.0.47227` ? `1.1.0.47228` al prossimo commit
