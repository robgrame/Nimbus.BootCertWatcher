# WMI Secure Boot Certificate Enumeration - Limitations

**Data**: 2025-01-21  
**Componente**: SecureBootWatcher.Client  
**Status**: ?? Limitato - Usare PowerShell invece

---

## ?? Sommario

Analisi delle limitazioni di WMI (Windows Management Instrumentation) per l'enumerazione dei certificati Secure Boot UEFI e raccomandazioni per approcci alternativi.

---

## ?? Problema

### Obiettivo
Enumerare i certificati presenti nei database UEFI Secure Boot:
- **db** (Signature Database) - Certificati autorizzati
- **dbx** (Forbidden Database) - Certificati revocati
- **KEK** (Key Exchange Keys) - Chiavi di scambio
- **PK** (Platform Key) - Chiave piattaforma

### Limitazione WMI
**WMI NON espone direttamente le variabili UEFI Secure Boot.**

---

## ?? Analisi Tecnica

### WMI Classes Disponibili

#### 1. Win32_BIOS
```csharp
// Fornisce informazioni BIOS/UEFI
SELECT * FROM Win32_BIOS

// Proprietà disponibili:
- BiosCharacteristics[] - Array di caratteristiche
- SMBIOSBIOSVersion - Versione firmware
- Manufacturer - Produttore
- ReleaseDate - Data rilascio

// ? NON fornisce:
- Certificati Secure Boot
- Database db/dbx/KEK/PK
- Variabili UEFI runtime
```

**Codice Esempio**:
```csharp
using var biosSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
using var biosCollection = biosSearcher.Get();

foreach (ManagementObject bios in biosCollection)
{
    var characteristics = bios["BiosCharacteristics"] as UInt16[];
    
    // Bit 27 (0x08000000) = UEFI support
    // Ma non dà accesso ai certificati!
    
    bios.Dispose();
}
```

#### 2. Win32_ComputerSystemProduct
```csharp
// Fornisce informazioni sistema
SELECT * FROM Win32_ComputerSystemProduct

// Proprietà disponibili:
- IdentifyingNumber - Serial number
- Name - Nome prodotto
- Vendor - Produttore
- UUID - Identificativo unico

// ? NON fornisce:
- Dati UEFI Secure Boot
```

#### 3. Win32_Environment
```csharp
// Fornisce variabili d'ambiente
SELECT * FROM Win32_Environment

// ? NON include:
- Variabili UEFI (sono in firmware, non in OS)
```

### Perché WMI Non Funziona?

**UEFI Secure Boot databases sono:**
1. **Firmware Variables** - Memorizzate in NVRAM (non-volatile RAM)
2. **Non accessibili via WMI** - WMI accede solo a dati OS-level
3. **Richiedono UEFI Runtime Services** - API kernel-mode

**Architettura**:
```
???????????????????????????????????????
?  User-Mode Application (C#)        ?
?  ? WMI (Win32_BIOS)                ?
?  ? UEFI Variables                  ?
???????????????????????????????????????
              ?
              ?
???????????????????????????????????????
?  Windows Kernel                     ?
?  ? WMI Provider                     ?
?  ? UEFI Runtime Services API       ?
???????????????????????????????????????
              ?
              ?
???????????????????????????????????????
?  UEFI Firmware                      ?
?  ? Secure Boot Variables (db/dbx)  ?
?  ? NVRAM Storage                   ?
???????????????????????????????????????

WMI non ha bridge verso UEFI Runtime Services!
```

---

## ? Soluzioni Alternative

### 1. **PowerShell Get-SecureBootUEFI** (? Raccomandato)

**Classe**: `PowerShellSecureBootCertificateEnumerator`

**Codice**:
```csharp
var script = $@"
    try {{
        $bytes = (Get-SecureBootUEFI -Name {databaseName}).Bytes
        if ($bytes) {{
            [Convert]::ToBase64String($bytes)
        }}
    }} catch {{
        Write-Error $_.Exception.Message
    }}";

var base64Data = await ExecutePowerShellAsync(script, cancellationToken);
```

**Pro**:
- ? Accesso completo a db, dbx, KEK, PK
- ? Supporto nativo Windows
- ? Parsing completo EFI_SIGNATURE_LIST
- ? Estrazione certificati X.509

**Contro**:
- ?? Richiede PowerShell 5.0+
- ?? Overhead processo esterno

### 2. **Registry Keys** (?? Fallback)

**Classe**: `SecureBootCertificateEnumerator` (corrente)

**Codice**:
```csharp
var possiblePaths = new[]
{
    "SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\db",
    "SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\Platform\\db"
};

foreach (var path in possiblePaths)
{
    using var key = Registry.LocalMachine.OpenSubKey(path, false);
    if (key != null)
    {
        // Leggi valori registro
    }
}
```

**Pro**:
- ? Nessuna dipendenza PowerShell
- ? Accesso diretto .NET

**Contro**:
- ? Inaffidabile (chiavi non sempre popolate)
- ? Dipende da quando Windows aggiorna registro
- ? Può essere vuoto anche con Secure Boot abilitato

### 3. **UEFI Runtime Services** (? Kernel-mode)

**Approccio**: Driver kernel Windows

**Codice** (ipotetico):
```c
// Kernel mode driver required
EFI_STATUS status = gRT->GetVariable(
    L"db",                          // Variable name
    &gEfiImageSecurityDatabaseGuid, // GUID
    NULL,                           // Attributes
    &dataSize,                      // Size
    data                            // Buffer
);
```

**Pro**:
- ? Accesso diretto UEFI

**Contro**:
- ? Richiede driver kernel firmato
- ? Complessità elevata
- ? Non pratico per applicazione user-mode

---

## ?? Raccomandazione

### Strategia Implementata

```csharp
// Program.cs - DI Registration
services.AddSingleton<ISecureBootCertificateEnumerator, 
    PowerShellSecureBootCertificateEnumerator>();  // ? PRIMARY

// Fallback automatico nella classe se PowerShell fallisce
```

### Ordine di Fallback

1. **PowerShell Get-SecureBootUEFI** (PRIMARY)
   - Affidabile, completo, supportato Windows

2. **Registry Keys** (FALLBACK)
   - Usato solo se PowerShell fallisce
   - Può restituire dati vuoti

3. **WMI** (NON IMPLEMENTATO)
   - Non fornisce dati utili
   - Placeholder documentato

---

## ?? Comparazione Approcci

| Approccio | Affidabilità | Performance | Complessità | Raccomandato |
|-----------|--------------|-------------|-------------|--------------|
| **PowerShell** | ? Alta | ?? Media | ? Bassa | ? **SÌ** |
| **Registry** | ?? Bassa | ? Alta | ? Bassa | ?? Fallback |
| **WMI** | ? Nulla | ? Alta | ? Bassa | ? NO |
| **Kernel Driver** | ? Alta | ? Alta | ? Altissima | ? NO |

---

## ?? Implementazione Attuale

### File Modificato
`SecureBootWatcher.Client/Services/SecureBootCertificateEnumerator.cs`

**Metodo WMI Implementato**:
```csharp
private void EnumerateCertificatesViaWmi(string databaseName, IList<SecureBootCertificate> targetList)
{
    try
    {
        _logger.LogDebug("Attempting WMI enumeration for {Database}", databaseName);

        using var biosSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
        using var biosCollection = biosSearcher.Get();
        
        foreach (ManagementObject bios in biosCollection)
        {
            try
            {
                var biosCharacteristics = bios["BiosCharacteristics"] as UInt16[];
                
                // BiosCharacteristics disponibili, ma non i certificati
                _logger.LogDebug("BIOS characteristics available, but certificate data not exposed via WMI");
            }
            finally
            {
                bios?.Dispose();
            }
            break;
        }

        _logger.LogDebug("WMI enumeration completed for {Database} - no certificate data available", databaseName);
    }
    catch (ManagementException mex)
    {
        _logger.LogDebug(mex, "WMI query failed for {Database} - {ErrorCode}", databaseName, mex.ErrorCode);
    }
}
```

**Risultato**: 
- ? Implementato (per completezza)
- ?? Non restituisce certificati (per limitazioni tecniche)
- ?? Documentato con commenti esplicativi

---

## ?? Riferimenti Tecnici

### UEFI Specification
- **UEFI Spec 2.10** - Section 32.4: Signature Database Update
- **EFI_SIGNATURE_LIST** structure definition
- **UEFI Runtime Services**: GetVariable() / SetVariable()

### Windows Implementation
- **PowerShell Module**: SecureBoot (built-in Windows 8+)
- **Cmdlets**: 
  - `Get-SecureBootUEFI`
  - `Confirm-SecureBootUEFI`
  - `Get-SecureBootPolicy`

### WMI Classes
- [Win32_BIOS Class](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-bios)
- [Win32_ComputerSystemProduct](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystemproduct)

---

## ?? Verifica Implementazione

### Test WMI
```powershell
# Test WMI access to BIOS info
Get-WmiObject -Class Win32_BIOS | Select-Object *

# Result: ? Firmware info available
#         ? No certificate databases

# Test PowerShell access to Secure Boot
Get-SecureBootUEFI -Name db

# Result: ? Certificate database accessible
```

### Test Codice
```csharp
// Test nell'applicazione
var enumerator = new SecureBootCertificateEnumerator(logger);
var collection = await enumerator.EnumerateAsync(CancellationToken.None);

// Se PowerShell disponibile:
//   collection.SignatureDatabase.Count > 0 ?
//
// Se solo WMI:
//   collection.SignatureDatabase.Count == 0 ??
//   (WMI fallback non restituisce dati)
```

---

## ? Conclusioni

### Decisione Tecnica

**NON usare WMI** per enumerazione certificati Secure Boot perché:
1. ? Non espone variabili UEFI
2. ? Nessun accesso a db/dbx/KEK/PK
3. ? Limitazione architetturale (WMI è OS-level, non firmware-level)

**Usare invece**:
1. ? **PowerShell Get-SecureBootUEFI** (PRIMARY)
   - `PowerShellSecureBootCertificateEnumerator` class
2. ?? **Registry Fallback** (SECONDARY)
   - `SecureBootCertificateEnumerator` class
3. ? **WMI** (NON IMPLEMENTATO per certificati)
   - Implementato solo per documentazione limitazioni

### Implementazione Finale

```csharp
// DI Registration (Program.cs)
services.AddSingleton<ISecureBootCertificateEnumerator, 
    PowerShellSecureBootCertificateEnumerator>();

// ? PowerShell approach = 100% affidabile
// ?? Registry fallback = disponibile se necessario
// ? WMI = documentato ma non funzionale per certificati
```

---

**Documento Creato**: 2025-01-21  
**Autore**: Analisi tecnica WMI vs PowerShell  
**Status**: ? Implementazione completata con documentazione

---

*WMI è ottimo per molte cose, ma non per UEFI Secure Boot certificates. PowerShell vince!* ??
