# ?? Command Management - User Guide

## ?? Panoramica

La funzionalità **Command Management** permette di inviare comandi di configurazione ai device client per gestire:

- ? **Certificate Update** - Aggiornamento certificati UEFI CA 2023
- ? **Microsoft Update Opt-In** - Eligibilità per Controlled Feature Rollout (CFR)
- ? **Telemetry Configuration** - Configurazione livelli di telemetria Windows

## ?? Dove Trovare il Menu Commands

### Nel Menu di Navigazione

Il menu **Commands** si trova nella barra di navigazione principale, tra **Devices** e **Windows Versions**:

```
Dashboard | Devices | [Commands ?] | Windows Versions | Admin
```

### Opzioni del Menu Commands

Cliccando su **Commands** si apre un dropdown con 3 opzioni:

1. **?? Send to Single Device** - Invia comando a un singolo device
2. **?? Send to Multiple Devices** - Invia comando batch a gruppi di device
3. **?? Command History** - Visualizza storico comandi inviati

### URL Diretti

Puoi anche navigare direttamente:

| Pagina | URL | Descrizione |
|--------|-----|-------------|
| Invio Singolo | `/Commands/Send` | Seleziona 1 device e invia comando |
| Invio Batch | `/Commands/Batch` | Seleziona multipli device e invia comando |
| Storico | `/Commands/History` | Visualizza tutti i comandi inviati |

### Accesso dalla Dashboard

Dalla **Dashboard Home** (`/Index`):

1. Widget **Commands** (se ci sono comandi attivi)
   - Click sul numero per vedere i comandi pending
   - Link "View all" per andare alla History

2. Sezione **Quick Actions**
   - Bottone **Command History** 

## ?? Invio Comando Singolo

### Percorso

`Commands ?` ? **Send to Single Device**

### Funzionalità

1. **Selezione Device**
   - Dropdown con tutti i device registrati
   - Mostra: Nome, Dominio, Manufacturer, Model, Deployment State

2. **Tipi di Comando**
   - Certificate Update
   - Microsoft Update Opt-In/Out
   - Telemetry Configuration

3. **Opzioni Avanzate**
   - **Priority**: 0-10 (0=normal, 10=highest)
   - **Schedule**: Esecuzione differita (data/ora)
   - **Description**: Note/motivo del comando

### Esempio: Certificate Update

```
1. Seleziona device: "SRVCM00 (MSINTUNE.LAB)"
2. Seleziona comando: "Certificate Update"
3. Configura:
   - Update Type: DB Update (1)
   - Force Update: ? (se necessario)
4. Aggiungi descrizione: "UEFI CA 2023 rollout"
5. Click "Send Command"
```

## ?? Invio Comando Batch (Gruppi)

### Percorso

`Commands ?` ? **Send to Multiple Devices**

### Modalità di Selezione Device

#### 1. Manual Select (Selezione Manuale)

- Lista checkbox di tutti i device
- **Select All** checkbox per selezionare tutti
- Dettagli visibili: Nome, Dominio, Manufacturer, Model, FleetID

**Quando usarlo**: 
- Quando hai una lista specifica di device
- Per test su pochi device selezionati

#### 2. By Fleet (Per FleetID)

- Dropdown con tutti i FleetID disponibili
- Mostra il numero di device per fleet
- Esempio: "mslabs (4 devices)"

**Quando usarlo**:
- Quando hai organizzato i device per fleet
- Per rollout per reparto/ufficio

#### 3. By Manufacturer (Per Produttore)

- Dropdown con tutti i manufacturer rilevati
- Mostra il numero di device per manufacturer
- Esempio: "LENOVO (12 devices)", "Dell Inc. (8 devices)"

**Quando usarlo**:
- Problemi specifici di un vendor
- Test di compatibilità firmware

#### 4. By Deployment State (Per Stato)

- Dropdown con stati deployment:
  - **Deployed** - Già deployati
  - **Pending** - In attesa
  - **InProgress** - In corso
  - **Error** - Errori
  - **NotStarted** - Mai iniziato

**Quando usarlo**:
- Retry su device in errore
- Completamento device pending

#### 5. All Devices ??

- **WARNING**: Invia a TUTTI i device registrati!
- Mostra alert rosso con conferma
- Da usare con estrema cautela

**Quando usarlo**:
- Rollout globale pianificato
- Update critici di sicurezza

### Esempio: Batch Certificate Update per Fleet

```
1. Seleziona: "By Fleet"
2. Scegli fleet: "mslabs (4 devices)"
3. Comando: "Certificate Update"
4. Configura:
   - Update Type: DB Update (1)
   - Force Update: ?
5. Priority: 5 (medium-high)
6. Description: "UEFI CA 2023 pilot rollout - mslabs fleet"
7. Click "Send Batch Command"

Risultato:
? Batch command queued! 4 succeeded, 0 failed out of 4 devices
```

## ?? Storico Comandi

### Percorso

`Commands ?` ? **Command History**

### Funzionalità

#### 1. Statistiche Card

Dashboard con conteggi:

| Card | Descrizione | Colore |
|------|-------------|--------|
| **Total** | Tutti i comandi | Grigio |
| **Pending** | In attesa di esecuzione | Giallo |
| **Completed** | Eseguiti con successo | Verde |
| **Failed** | Falliti | Rosso |
| **Cancelled** | Cancellati manualmente | Grigio scuro |

Click su una card per filtrare per stato!

#### 2. Tabella Comandi

Colonne visualizzate:

| Colonna | Contenuto |
|---------|-----------|
| **Status** | Badge colorato con icona |
| **Command Type** | Tipo di comando (`CertificateUpdate`, ecc.) |
| **Device** | Link al device (primi 8 char GUID) |
| **Description** | Note/descrizione |
| **Created** | Data/ora creazione |
| **Created By** | Utente che ha creato il comando |
| **Priority** | Livello priorità (badge se >0) |
| **Fetches** | Numero volte scaricato dal client |
| **Processed** | Data/ora esecuzione |
| **Actions** | Pulsanti azione |

#### 3. Azioni Disponibili

- **Cancel** (?) - Annulla comando Pending o Fetched
- **View** (???) - Dettagli completi comando

#### 4. Filtri

- **By Status**: Click sulle card statistiche
- **By Device**: Parametro `?deviceId=...` nell'URL
- **Clear Filter**: Bottone per rimuovere filtri

### Stati dei Comandi

| Status | Icona | Descrizione |
|--------|-------|-------------|
| **Pending** | ? | In coda, non ancora scaricato |
| **Fetched** | ?? | Scaricato dal client, in esecuzione |
| **Processing** | ?? | In elaborazione |
| **Completed** | ? | Eseguito con successo |
| **Failed** | ? | Esecuzione fallita |
| **Cancelled** | ? | Cancellato manualmente |
| **Expired** | ? | Scaduto (>7 giorni) |

## ?? Tipi di Comandi

### 1. Certificate Update

**Scopo**: Aggiorna i certificati UEFI nella Signature Database (db)

**Opzioni**:

| Opzione | Valore | Descrizione |
|---------|--------|-------------|
| **Update Type** | 0 | None - Nessun update |
| | 1 | **DB Update** - Aggiorna Signature Database (consigliato) |
| | 2 | Boot Manager - Aggiorna Boot Manager |
| **Force Update** | ? | Ignora check di compatibilità |
| | ? | Forza update anche se non supportato |

**Registry modificato**:
```
HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\Policy\Default
?? SecureBootCertificateUpdateType (DWORD) = 1
```

**Requisiti**:
- Device con UEFI Secure Boot enabled
- Windows 10 22H2+ o Windows 11
- Permessi Administrator sul client

**Quando usarlo**:
- Preparazione per Windows 11 24H2/25H2
- Compliance UEFI CA 2023

### 2. Microsoft Update Opt-In

**Scopo**: Abilita/disabilita eligibilità per Controlled Feature Rollout (CFR)

**Opzioni**:

| Opzione | Valore | Descrizione |
|---------|--------|-------------|
| **Opt-In** | ? | Disabilita CFR (valore 0) |
| | ? | **Abilita CFR** (valore 1) - Consigliato |

**Registry modificato**:
```
HKLM\SOFTWARE\Microsoft\WindowsUpdate\UpdatePolicy\GPO
?? MicrosoftUpdateManagedOptIn (DWORD) = 1
```

**Requisiti**:
- Telemetry level ? 1 (Basic)
- Windows Update attivo
- Permessi Administrator sul client

**Quando usarlo**:
- Rollout graduale Windows 11 24H2/25H2
- Test pre-production

### 3. Telemetry Configuration

**Scopo**: Configura/valida il livello di telemetria Windows

**Opzioni**:

| Opzione | Valore | Descrizione | CFR Eligible |
|---------|--------|-------------|--------------|
| **Telemetry Level** | 0 | Security - Solo Enterprise | ? |
| | 1 | **Basic** - Minimo per CFR | ? |
| | 2 | Enhanced | ? |
| | 3 | Full | ? |
| **Validate Only** | ? | Solo verifica, non modifica | - |
| | ? | Verifica E modifica se necessario | - |

**Registry verificato**:
```
HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection
?? AllowTelemetry (DWORD) ? 1
```

**Requisiti**:
- Windows Enterprise/Pro
- Permessi Administrator sul client

**Quando usarlo**:
- Pre-flight check per CFR
- Audit compliance privacy

## ?? Opzioni Comuni

### Priority (Priorità)

| Valore | Livello | Quando usare |
|--------|---------|--------------|
| **0** | Normal | Comandi standard, non urgenti |
| **1-4** | Low-Medium | Update pianificati |
| **5-7** | Medium-High | Rollout importanti |
| **8-10** | Critical | Hotfix, security critical |

I comandi vengono eseguiti in ordine:
1. Priority più alta
2. A parità, FIFO (First In First Out)

### Schedule Execution

**Opzioni**:
- ? **Immediate** - Esegui appena possibile (default)
- ? **Scheduled** - Esegui a data/ora specifica

**Quando usare lo scheduling**:
- Finestre di manutenzione notturne
- Coordinamento con altri sistemi
- Rollout scaglionati (batch ogni ora)

**Esempio**:
```
Schedule: 2025-01-15 02:00 AM
? Il client eseguirà il comando solo dopo questa data/ora
```

## ?? Monitoraggio Esecuzione

### 1. Fetch Count

Indica quante volte il client ha scaricato il comando:

| Count | Significato |
|-------|-------------|
| **0** | Mai scaricato (Pending) |
| **1** | Scaricato una volta (normale) |
| **2+** | Ri-scaricato (retry o client restart) |

### 2. Processed Date

- **NULL**: Non ancora eseguito
- **Data/ora**: Momento di completamento (success o failure)

### 3. Result

Visualizza il risultato dettagliato:
- Success message
- Error message
- Validation warnings

## ?? Troubleshooting

### Comando Resta "Pending"

**Possibili cause**:
1. Client non in esecuzione
2. Command Processing disabilitato sul client
3. Network/firewall blocca API access

**Soluzione**:
```json
// Client appsettings.json
"Commands": {
  "EnableCommandProcessing": true,  // ? Verifica questo!
  "ProcessBeforeInventory": true,
  "MaxCommandsPerCycle": 10
}
```

### Comando "Failed"

**Verifica**:
1. Check logs client: `C:\...\logs\client-*.log`
2. Permission error? ? Client deve girare come Administrator
3. Registry access denied? ? UAC/Group Policy restrictions

### Batch Partial Failure

Alcuni device falliti?

**Analisi**:
1. Check "Failed Devices" alert box
2. Copia GUID dei failed devices
3. Filtra History per device ID
4. Analizza error message

**Common issues**:
- Device offline/spento
- Client version incompatibile
- Firmware non supporta update

## ?? Security & Permissions

### Requisiti Client

| Funzione | Requisito |
|----------|-----------|
| **Fetch commands** | HTTP access to API |
| **Execute commands** | Administrator privileges |
| **Registry write** | SYSTEM or Admin |

### Best Practices

? **DO**:
- Test su pochi device prima di batch
- Usa description chiare e tracciabili
- Monitora command history regolarmente
- Cancella comandi obsoleti

? **DON'T**:
- Non usare "All Devices" senza pianificazione
- Non forzare update su hardware incompatibile
- Non sovraccaricare con priority alta inutile

## ?? Client Configuration

Per abilitare command processing sul client:

```json
// SecureBootWatcher.Client\appsettings.json
{
  "SecureBootWatcher": {
    "Commands": {
      "EnableCommandProcessing": true,        // ? MUST BE TRUE
      "ProcessBeforeInventory": true,         // Execute before inventory
      "MaxCommandsPerCycle": 10,              // Max commands per run
      "CommandExecutionDelay": "00:00:02",    // Delay between commands
      "ContinueOnCommandFailure": true        // Don't stop on error
    }
  }
}
```

## ?? Related Documentation

- **Client Version Tracking**: `docs/CLIENT_VERSION_TRACKING.md`
- **Client Sink Configuration**: `docs/CLIENT_SINK_CONFIGURATION.md`
- **SSL Certificate Bypass**: `docs/SSL_CERTIFICATE_BYPASS.md`
- **API Configuration**: Dashboard ? Admin ? API Configuration

## ?? Support

**Logs Location**:
- **Web Dashboard**: `R:\Nimbus.SecureBootCert\logs\web-*.log`
- **API**: `R:\Nimbus.SecureBootCert\logs\api-*.log`
- **Client**: `%ProgramFiles%\SecureBootWatcher\logs\client-*.log`

**Common Log Searches**:
```
# Command queued
"Queued command {CommandId}"

# Command fetched by client
"Fetching pending commands for device"

# Command executed
"Command {CommandId} marked as {Status}"
```

---

**Version**: 1.14.0  
**Last Updated**: 2025-01-23  
**Status**: ? Fully Operational
