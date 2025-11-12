# PowerShell Execution Issues - Diagnostic Guide

**Data**: 2025-01-21  
**Componente**: PowerShellSecureBootCertificateEnumerator  
**Problema**: Script PowerShell restituisce `null` quando eseguito da C#

---

## ?? Problema

### Sintomo
Lo script PowerShell funziona quando eseguito manualmente in una shell PowerShell, ma restituisce `null` o stringa vuota quando eseguito dal metodo `ExecutePowerShellAsync` nel codice C#.

### Script Interessato
```powershell
try {
    $bytes = (Get-SecureBootUEFI -Name {databaseName}).Bytes
    if ($bytes) {
        [Convert]::ToBase64String($bytes)
    }
} catch {
    Write-Error $_.Exception.Message
}
```

---

## ?? Cause Comuni

### 1. **Execution Policy**
PowerShell potrebbe bloccare l'esecuzione dello script.

**Verifica**:
```powershell
Get-ExecutionPolicy
```

**Soluzione nel codice**:
```csharp
Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ..."
```
? Giא implementato

### 2. **Privilegi Insufficienti**
`Get-SecureBootUEFI` richiede privilegi elevati (Administrator o SYSTEM).

**Verifica**:
```powershell
# Esegui come Administrator
[Security.Principal.WindowsIdentity]::GetCurrent().Name

# Test cmdlet
Get-SecureBootUEFI -Name db
```

**Soluzione**:
- Eseguire l'applicazione client come Administrator o SYSTEM
- Scheduled task dovrebbe essere configurato con "Run with highest privileges"

### 3. **PowerShell 32-bit vs 64-bit**
Il processo C# potrebbe avviare PowerShell 32-bit che non ha accesso ai cmdlet UEFI.

**Problema**:
```
C:\Windows\SysWOW64\WindowsSystem32\powershell.exe  ? 32-bit
C:\Windows\System32\powershell.exe                  ? 64-bit
```

**Verifica quale PowerShell viene utilizzato**:
```csharp
// Nel codice C#
_logger.LogDebug("PowerShell path: {Path}", startInfo.FileName);
_logger.LogDebug("Process architecture: {Architecture}", 
    Environment.Is64BitProcess ? "x64" : "x86");
```

**Soluzione**:
```csharp
var startInfo = new ProcessStartInfo
{
    // Forza PowerShell 64-bit
    FileName = Environment.Is64BitOperatingSystem
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")
        : "powershell.exe",
    // ... rest of config
};
```

### 4. **Working Directory**
PowerShell potrebbe avviarsi in una directory senza permessi.

**Soluzione implementata**:
```csharp
WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
```

### 5. **Output Encoding**
PowerShell potrebbe scrivere su stdout in un encoding non supportato.

**Possibile soluzione**:
```csharp
var startInfo = new ProcessStartInfo
{
    // ... existing config
    StandardOutputEncoding = System.Text.Encoding.UTF8,
    StandardErrorEncoding = System.Text.Encoding.UTF8
};
```

### 6. **Timeout Processo**
Il processo PowerShell potrebbe impiegare piש tempo del previsto.

**Soluzione implementata**:
```csharp
if (!process.WaitForExit(30000)) // 30 second timeout
{
    _logger.LogWarning("PowerShell process timeout - killing process");
    process.Kill();
}
```

### 7. **Async I/O Non Completato**
Gli stream async potrebbero non essere completamente letti prima che il processo termini.

**Soluzione implementata**:
```csharp
// Wait for process
process.WaitForExit(30000);

// Give async operations time to complete
await Task.Delay(100, cancellationToken);
```

---

## ?? Miglioramenti Implementati

### Versione Migliorata del Metodo

```csharp
private async Task<string> ExecutePowerShellAsync(string script, CancellationToken cancellationToken)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System) // NEW
    };

    using var process = new Process { StartInfo = startInfo };

    var output = new StringBuilder();
    var error = new StringBuilder();

    process.OutputDataReceived += (sender, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            output.AppendLine(e.Data);
        }
    };

    process.ErrorDataReceived += (sender, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            error.AppendLine(e.Data);
        }
    };

    try
    {
        // NEW: Log script execution
        _logger.LogDebug("Executing PowerShell script: {Script}", 
            script.Substring(0, Math.Min(100, script.Length)));
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // NEW: Timeout protection
        await Task.Run(() =>
        {
            if (!process.WaitForExit(30000)) // 30 second timeout
            {
                _logger.LogWarning("PowerShell process timeout - killing process");
                try
                {
                    process.Kill();
                }
                catch (Exception killEx)
                {
                    _logger.LogDebug(killEx, "Failed to kill PowerShell process");
                }
            }
        }, cancellationToken);

        // NEW: Wait for async I/O
        await Task.Delay(100, cancellationToken);

        var exitCode = process.ExitCode;
        var outputText = output.ToString().Trim();
        var errorText = error.ToString().Trim();

        // NEW: Detailed logging
        _logger.LogDebug(
            "PowerShell execution completed - ExitCode: {ExitCode}, Output length: {OutputLength}, Error length: {ErrorLength}",
            exitCode,
            outputText.Length,
            errorText.Length);

        if (!string.IsNullOrEmpty(errorText))
        {
            _logger.LogDebug("PowerShell stderr: {Error}", errorText);
        }

        // NEW: Exit code check
        if (exitCode != 0)
        {
            _logger.LogWarning(
                "PowerShell exited with non-zero code {ExitCode}. Output: {Output}, Error: {Error}",
                exitCode,
                outputText.Length > 0 ? outputText.Substring(0, Math.Min(200, outputText.Length)) : "(empty)",
                errorText.Length > 0 ? errorText.Substring(0, Math.Min(200, errorText.Length)) : "(empty)");
        }

        return outputText;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to execute PowerShell script");
        return string.Empty;
    }
}
```

### Benefici

1. ? **Logging Dettagliato**: Capire cosa sta succedendo
2. ? **Timeout Protection**: Evitare processi bloccati
3. ? **Working Directory**: Esecuzione in directory sicura
4. ? **Exit Code Check**: Rilevare fallimenti PowerShell
5. ? **Async I/O Wait**: Garantire lettura completa dell'output
6. ? **Error Handling**: Gestione robusta delle eccezioni

---

## ?? Test Diagnostici

### Test 1: Verifica PowerShell Path

```csharp
// Aggiungi log temporaneo
_logger.LogInformation("PowerShell executable: {Path}", 
    Path.GetFullPath("powershell.exe"));
    
_logger.LogInformation("Process is 64-bit: {Is64Bit}", 
    Environment.Is64BitProcess);
```

**Output atteso**:
```
PowerShell executable: C:\Windows\System32\powershell.exe
Process is 64-bit: True
```

### Test 2: Verifica Privilegi

```csharp
// Test semplice - Confirm-SecureBootUEFI
var script = "Confirm-SecureBootUEFI";
var result = await ExecutePowerShellAsync(script, cancellationToken);

_logger.LogInformation("SecureBoot check result: '{Result}' (length: {Length})", 
    result, result?.Length ?? 0);
```

**Output atteso**:
```
SecureBoot check result: 'True' (length: 4)
```

**Se ottieni stringa vuota**:
- Privilegi insufficienti
- PowerShell 32-bit utilizzato
- Cmdlet non disponibile

### Test 3: Verifica Encoding

```csharp
// Test output con caratteri speciali
var script = "Write-Output 'Test: אטילעש 123'";
var result = await ExecutePowerShellAsync(script, cancellationToken);

_logger.LogInformation("Encoding test: '{Result}'", result);
```

**Output atteso**:
```
Encoding test: 'Test: אטילעש 123'
```

### Test 4: Verifica Get-SecureBootUEFI

```powershell
# Esegui manualmente come Administrator
Get-SecureBootUEFI -Name db

# Output atteso:
# Name       : db
# Bytes      : {39, 0, 0, 0, ...}
# Attributes : NonVolatile, BootserviceAccess, RuntimeAccess
```

Se funziona manualmente ma non dal codice:
- ? Problema di privilegi
- ? PowerShell 32-bit vs 64-bit
- ? Execution policy (unlikely con -Bypass)

---

## ?? Soluzioni Raccomandate

### Soluzione 1: Forza PowerShell 64-bit

```csharp
private async Task<string> ExecutePowerShellAsync(string script, CancellationToken cancellationToken)
{
    // Determine correct PowerShell path
    var powerShellPath = "powershell.exe";
    
    if (Environment.Is64BitOperatingSystem)
    {
        // Force 64-bit PowerShell
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        powerShellPath = Path.Combine(system32, "WindowsPowerShell", "v1.0", "powershell.exe");
        
        _logger.LogDebug("Using 64-bit PowerShell: {Path}", powerShellPath);
    }
    
    var startInfo = new ProcessStartInfo
    {
        FileName = powerShellPath,
        // ... rest of config
    };
    
    // ... rest of method
}
```

### Soluzione 2: Aggiungi Encoding Esplicito

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = powerShellPath,
    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true,
    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
    StandardOutputEncoding = System.Text.Encoding.UTF8, // NEW
    StandardErrorEncoding = System.Text.Encoding.UTF8   // NEW
};
```

### Soluzione 3: Fallback a WaitForExit Sincrono

Se il problema persiste con async, usa la versione sincrona:

```csharp
process.Start();
process.BeginOutputReadLine();
process.BeginErrorReadLine();

// Synchronous wait
process.WaitForExit(30000);

// Ensure async handlers complete
Thread.Sleep(100);

var outputText = output.ToString().Trim();
```

---

## ?? Checklist Diagnostica

Prima di investigare ulteriormente, verifica:

- [ ] ? L'applicazione ט eseguita come Administrator o SYSTEM
- [ ] ? PowerShell 5.0+ ט installato
- [ ] ? `Get-SecureBootUEFI` funziona manualmente come Administrator
- [ ] ? Il processo C# ט 64-bit (o il client ט compilato come AnyCPU)
- [ ] ? Secure Boot ט abilitato sul sistema (opzionale per enumerazione)
- [ ] ? I log mostrano l'esecuzione dello script
- [ ] ? Exit code PowerShell ט 0 (successo)
- [ ] ? Nessun errore in stderr

---

## ?? Log Analysis

### Log Normale (Funzionante)

```
[DBG] Executing PowerShell script: try { $bytes = (Get-SecureBootUEFI -Name db).Bytes if ($bytes) { [Convert]::ToBase64String($bytes) } } catch { Write-Error $_.Exception.Message }
[DBG] PowerShell execution completed - ExitCode: 0, Output length: 15234, Error length: 0
[DBG] Found 7 certificates in db
```

### Log Problematico

```
[DBG] Executing PowerShell script: try { $bytes = (Get-SecureBootUEFI -Name db).Bytes if ($bytes) { [Convert]::ToBase64String($bytes) } } catch { Write-Error $_.Exception.Message }
[DBG] PowerShell execution completed - ExitCode: 1, Output length: 0, Error length: 112
[DBG] PowerShell stderr: Get-SecureBootUEFI : Access is denied
[DBG] No data returned for db
```

**Diagnosi**: Privilegi insufficienti

---

## ?? Debugging Steps

1. **Abilita Logging Dettagliato**:
   ```json
   // appsettings.json
   {
     "Serilog": {
       "MinimumLevel": {
         "Default": "Debug"
       }
     }
   }
   ```

2. **Esegui Client Manualmente**:
   ```powershell
   # Apri PowerShell come Administrator
   cd "C:\path\to\client"
   .\SecureBootWatcher.Client.exe
   ```

3. **Controlla Log Output**:
   ```powershell
   Get-Content "logs\client-*.log" -Tail 50
   ```

4. **Test PowerShell Diretto**:
   ```powershell
   # Come Administrator
   $script = @"
   try {
       `$bytes = (Get-SecureBootUEFI -Name db).Bytes
       if (`$bytes) {
           [Convert]::ToBase64String(`$bytes)
       }
   } catch {
       Write-Error `$_.Exception.Message
   }
   "@
   
   powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $script
   ```

---

## ? Risultati Attesi

Dopo le modifiche implementate, dovresti vedere nei log:

```
[INF] SecureBootWatcher Client Starting
[DBG] Executing PowerShell script: Confirm-SecureBootUEFI
[DBG] PowerShell execution completed - ExitCode: 0, Output length: 4, Error length: 0
[INF] Secure Boot is enabled on this device
[DBG] Executing PowerShell script: try { $bytes = (Get-SecureBootUEFI -Name db).Bytes...
[DBG] PowerShell execution completed - ExitCode: 0, Output length: 15234, Error length: 0
[DBG] Found 7 certificates in db
[DBG] Executing PowerShell script: try { $bytes = (Get-SecureBootUEFI -Name dbx).Bytes...
[DBG] PowerShell execution completed - ExitCode: 0, Output length: 8912, Error length: 0
[DBG] Found 4 certificates in dbx
[INF] Enumerated 15 certificates: db=7, dbx=4, KEK=3, PK=1, Expired=0
```

---

**Documento Creato**: 2025-01-21  
**Status**: Miglioramenti implementati  
**Next Steps**: Test in ambiente reale con logging dettagliato abilitato

---

*Se il problema persiste, condividi i log completi dell'esecuzione per analisi approfondita.*
