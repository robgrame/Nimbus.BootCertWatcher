# ??? Secure Boot Certificate Dashboard - Technical Overview

## Soluzione Enterprise per la Gestione Certificati UEFI

---

## ?? Il Problema di Business

### Contesto: La Transizione UEFI CA 2023

Microsoft ha annunciato che i certificati **Windows UEFI CA** attualmente in uso su tutti i PC Windows scadranno nei prossimi anni. Questo richiede un aggiornamento proattivo della **Signature Database (db)** nel firmware UEFI di ogni dispositivo.

**Impatto se non gestito**:
- ? Dispositivi che non si avviano dopo la scadenza
- ? Blocco delle migrazioni a Windows 11 24H2/25H2
- ? Violazione policy di sicurezza aziendali
- ? Downtime non pianificato e ticket helpdesk elevati

### La Sfida per l'IT

| Domanda Critica | Risposta Attuale |
|-----------------|------------------|
| *Quanti PC hanno Secure Boot abilitato?* | "Non lo sappiamo con certezza" |
| *Quali PC hanno già il certificato CA 2023?* | "Dovremmo controllarli uno per uno" |
| *Possiamo forzare l'update da remoto?* | "Non abbiamo uno strumento per farlo" |
| *Quali PC sono pronti per Windows 11?* | "Serve un assessment manuale" |

---

## ? La Soluzione: Secure Boot Certificate Dashboard

### Cosa Fa

**Secure Boot Certificate Dashboard** è una piattaforma centralizzata che:

1. **Raccoglie automaticamente** lo stato Secure Boot da ogni PC
2. **Visualizza in dashboard** la situazione dell'intera flotta
3. **Identifica** dispositivi non conformi o a rischio
4. **Esegue remotamente** gli aggiornamenti certificati
5. **Traccia** il progresso del rollout in tempo reale

### Come Funziona

```
???????????????????????????????????????????????????????????????????????????
?                                                                          ?
?   1?? AGENT DEPLOYMENT          2?? DATA COLLECTION         3?? DASHBOARD   ?
?   ?????????????????            ?????????????????          ???????????   ?
?                                                                          ?
?   ???????                      ???????????????           ????????????   ?
?   ? PC1 ???                 ??>?             ?           ? Browser  ?   ?
?   ??????? ?   Ogni 30 min   ?  ?    API      ?  Real-    ? ???????  ?   ?
?   ??????? ?   ??????????>   ?  ?   Server    ?  time     ? Grafico  ?   ?
?   ? PC2 ?????????????????????  ?             ?<?????????>? Tabelle  ?   ?
?   ??????? ?   HTTPS POST    ?  ? ??????????? ?  SignalR  ? Filtri   ?   ?
?   ??????? ?   Inventory     ?  ? ?   DB    ? ?           ? Reports  ?   ?
?   ? PC3 ???   + Certificati ?  ? ??????????? ?           ????????????   ?
?   ???????                   ?  ???????????????                          ?
?                             ?                                            ?
?   ... (1000+ PC)            ??? Report JSON con:                        ?
?                                 • Stato Secure Boot                      ?
?                                 • Lista certificati db/dbx               ?
?                                 • Versione OS/Firmware                   ?
?                                 • Capability flags                       ?
?                                                                          ?
???????????????????????????????????????????????????????????????????????????
```

---

## ?? Funzionalità Dettagliate

### 1. Inventory Automatico

L'agent installato su ogni PC raccoglie:

| Categoria | Dati Raccolti |
|-----------|---------------|
| **Device Identity** | MachineName, Domain, SMBIOS UUID |
| **Secure Boot** | Enabled/Disabled, Setup Mode |
| **UEFI Certificates** | Tutti i certificati nella db con scadenze |
| **Windows UEFI CA 2023** | Presente/Assente |
| **Capability Code** | Hardware supporta update? |
| **OS Info** | Build number, edition, architecture |
| **Firmware** | Vendor, Version, Release Date |
| **Telemetry Settings** | AllowTelemetry level |
| **MS Update Opt-In** | CFR eligibility status |

**Frequenza**: Configurabile (default: ogni 30 minuti)  
**Impatto**: < 1% CPU, < 50MB RAM durante esecuzione

### 2. Dashboard Real-Time

**Home Dashboard**:
```
???????????????????????????????????????????????????????????????????????
?  ?? SECURE BOOT DASHBOARD                                           ?
???????????????????????????????????????????????????????????????????????
?                                                                      ?
?  ?????????????  ?????????????  ?????????????  ?????????????        ?
?  ?  TOTAL    ?  ? DEPLOYED  ?  ?  PENDING  ?  ?  ERRORS   ?        ?
?  ?   4,523   ?  ?   3,891   ?  ?    482    ?  ?    150    ?        ?
?  ?  devices  ?  ?   86.0%   ?  ?   10.7%   ?  ?    3.3%   ?        ?
?  ?????????????  ?????????????  ?????????????  ?????????????        ?
?                                                                      ?
?  [??????????????????????????????????????????????] 86% Compliance    ?
?                                                                      ?
?  ???????????????????????????????????????????????????????????????    ?
?  ? Recent Activity                                              ?    ?
?  ? ?????????????????                                            ?    ?
?  ? ?? PC-IT-0542  Deployed     2 min ago                       ?    ?
?  ? ?? PC-HR-0128  Deployed     5 min ago                       ?    ?
?  ? ?? PC-FIN-0891 Error        8 min ago  "Registry access..."  ?    ?
?  ? ?? PC-MKT-0234 Pending     15 min ago                        ?    ?
?  ???????????????????????????????????????????????????????????????    ?
?                                                                      ?
???????????????????????????????????????????????????????????????????????
```

### 3. Device Management

**Device List con Filtri Avanzati**:

| Filtro | Opzioni |
|--------|---------|
| **Stato Deployment** | NotStarted, Pending, InProgress, Deployed, Error |
| **Secure Boot** | Enabled, Disabled |
| **Windows UEFI CA 2023** | Present, Absent |
| **Readiness** | Ready, Not Ready |
| **Manufacturer** | Dell, HP, Lenovo, Microsoft, ... |
| **OS Version** | Windows 10 22H2, Windows 11 23H2, ... |
| **Fleet ID** | Tag personalizzabile per raggruppamenti |

**Device Details**:
- Informazioni complete hardware/software
- Lista certificati UEFI con date scadenza
- Storico report ricevuti
- Comandi eseguiti con risultati

### 4. Command Management

**Tipi di Comandi Supportati**:

| Comando | Descrizione | Impatto |
|---------|-------------|---------|
| **Certificate Update** | Imposta flag per update certificati al prossimo reboot | Modifica registry |
| **MS Update Opt-In** | Abilita/Disabilita CFR eligibility | Modifica registry |
| **Telemetry Config** | Verifica/Imposta livello telemetria | Modifica registry |

**Modalità di Invio**:

1. **Single Device**: Seleziona un PC e invia comando
2. **Batch (By Filter)**:
   - Per Fleet ID
   - Per Manufacturer
   - Per Deployment State
   - Per OS Version
3. **Batch (All)**: Tutti i dispositivi (con conferma)

**Opzioni Avanzate**:
- **Priority**: 0-10 (urgenza esecuzione)
- **Schedule**: Data/ora esecuzione differita
- **Description**: Note per audit trail

### 5. Windows Version Tracking

**Integrazione con dati Microsoft**:

- Versioni Windows 10/11 con date EOL
- Build numbers e KB associati
- Verifica automatica compatibilità OS per UEFI update

---

## ?? Sicurezza

### Autenticazione & Autorizzazione

| Metodo | Supportato | Note |
|--------|------------|------|
| **Windows Authentication** | ? | Integrazione AD nativa |
| **Entra ID (Azure AD)** | ? | SSO enterprise |
| **Mutual TLS** | ? | Autenticazione client certificato |
| **API Key** | ? | Per integrazioni machine-to-machine |

### Protezione Dati

- **Transport**: TLS 1.2+ obbligatorio
- **At-Rest**: Encryption database (TDE raccomandato)
- **Secrets**: Azure Key Vault / Windows DPAPI

### Audit Trail

Tutte le operazioni sono tracciate:
- Chi ha inviato il comando
- Quando è stato inviato
- Risultato esecuzione
- Stato attuale

---

## ?? Architettura Componenti

### Client Agent

**Tecnologia**: .NET 10, Single-file deployment

**Componenti**:
```
SecureBootWatcher.Client.exe
??? Services/
?   ??? SecureBootWatcherService.cs  ? Main loop
?   ??? CommandProcessor.cs          ? Esegue comandi
?   ??? ReportBuilder.cs             ? Costruisce inventory
??? Sinks/
?   ??? WebApiSink.cs                ? Invio diretto a API
?   ??? AzureQueueSink.cs            ? Buffer su Azure Queue
?   ??? FileShareSink.cs             ? Fallback su file share
??? appsettings.json                  ? Configurazione
```

**Deployment Options**:

| Metodo | Script Fornito | Note |
|--------|----------------|------|
| SCCM/MECM | ? | Application + Task Sequence |
| Intune | ? | Win32 App con detection rule |
| GPO | ? | Startup script + Scheduled Task |
| Manual | ? | MSI / ZIP |

### API Server

**Tecnologia**: ASP.NET Core 10, Entity Framework Core

**Endpoints Principali**:

| Route | Metodo | Descrizione |
|-------|--------|-------------|
| `/api/SecureBootReports` | POST | Ricezione inventory |
| `/api/Devices` | GET | Lista dispositivi |
| `/api/Devices/{id}` | GET | Dettagli dispositivo |
| `/api/CommandManagement/queue` | POST | Coda comando singolo |
| `/api/CommandManagement/queue-batch` | POST | Coda comando batch |
| `/api/ClientCommands/pending` | GET | Comandi per device |
| `/api/ClientCommands/result` | POST | Risultato esecuzione |

**Background Services**:
- Queue Processor (Azure Queue ? Database)
- Device Cleanup (rimozione stale devices)
- SignalR Hub (real-time updates)

### Dashboard Web

**Tecnologia**: ASP.NET Core Razor Pages, Bootstrap 5

**Sezioni**:

```
?? Dashboard          ? Home con metriche e grafici
?? Fleet
   ??? Devices        ? Lista e gestione dispositivi
   ??? Cleanup        ? Rimozione device inattivi
   ??? Versions       ? Windows/Client versions
?? Operations
   ??? Send Single    ? Comando a singolo device
   ??? Send Batch     ? Comando a gruppo device
   ??? History        ? Storico comandi
?? Admin
   ??? App Settings   ? Configurazione applicazione
   ??? Sink Config    ? Configurazione sink client
   ??? API Config     ? Configurazione API
   ??? Mutual TLS     ? Gestione certificati
? Help
   ??? About          ? Versione e info
   ??? Privacy        ? Privacy policy
   ??? Documentation  ? Link a docs
```

### Database

**Engine**: SQL Server 2019+ / Azure SQL

**Schema Principale**:

```sql
Devices              ? Anagrafica dispositivi
SecureBootReports    ? Report inventory storici
SecureBootEvents     ? Eventi Windows correlati
PendingCommands      ? Coda comandi
WindowsVersions      ? Versioni Windows supportate
WindowsBuilds        ? Build numbers con KB
ApplicationSettings  ? Configurazione dinamica
DeviceCleanupConfig  ? Policy cleanup
MutualTlsConfig      ? Configurazione mTLS
ClientSinkConfig     ? Template configurazione client
ApiConfiguration     ? Configurazione API
```

---

## ?? Deployment Checklist

### Prerequisiti

- [ ] SQL Server 2019+ o Azure SQL Database
- [ ] Windows Server 2019+ o Azure App Service
- [ ] .NET 10 Runtime
- [ ] Certificato SSL (self-signed per test, CA per prod)
- [ ] Outbound HTTPS dai client verso API

### Step Deployment

1. **Database**
   ```powershell
   # Crea database
   sqlcmd -S server -Q "CREATE DATABASE SecureBootDashboard"
   
   # Applica migrations (automatico al primo avvio)
   ```

2. **API Server**
   ```powershell
   # Deploy
   dotnet publish -c Release -o C:\SecureBootApi
   
   # Configura IIS/Kestrel
   # Imposta connection string
   ```

3. **Web Dashboard**
   ```powershell
   # Deploy
   dotnet publish -c Release -o C:\SecureBootWeb
   
   # Configura puntamento a API
   ```

4. **Client Agent**
   ```powershell
   # Package
   .\scripts\Package-Client.ps1 -Configuration Release
   
   # Deploy via SCCM/Intune/GPO
   # Usando scripts forniti
   ```

---

## ?? Sizing & Performance

### Sizing Guidelines

| Fleet Size | API Server | Database | Client Impact |
|------------|------------|----------|---------------|
| 1-100 | 2 CPU, 4GB | 10 GB | Negligible |
| 100-500 | 4 CPU, 8GB | 25 GB | Negligible |
| 500-2000 | 4 CPU, 16GB | 50 GB | Negligible |
| 2000-10000 | 8 CPU, 32GB | 100 GB | Negligible |

### Performance Characteristics

| Metrica | Valore |
|---------|--------|
| **Client check-in latency** | < 5 sec |
| **Command execution latency** | 0-30 min (polling) |
| **Dashboard page load** | < 2 sec |
| **Concurrent users supported** | 50+ |
| **Reports/minute (burst)** | 500+ |

---

## ?? Integrazioni Supportate

### Oggi

| Sistema | Integrazione |
|---------|--------------|
| **SCCM/MECM** | Script deployment |
| **Intune** | Win32 App package |
| **Azure Queue** | Native support |
| **SQL Server** | Native support |
| **Windows Auth** | Native support |
| **Entra ID** | OIDC integration |

### Roadmap

| Sistema | Stato |
|---------|-------|
| **ServiceNow** | Q1 2025 |
| **Microsoft Graph** | Q2 2025 |
| **Defender for Endpoint** | Q2 2025 |
| **Splunk/SIEM** | Q3 2025 |

---

## ?? Documentazione Disponibile

| Documento | Contenuto |
|-----------|-----------|
| `COMMAND_EXECUTION_ARCHITECTURE.md` | Architettura tecnica dettagliata |
| `COMMAND_MANAGEMENT_USER_GUIDE.md` | Guida utente funzionalità comandi |
| `CLIENT_SINK_CONFIGURATION.md` | Configurazione sink client |
| `SSL_CERTIFICATE_BYPASS.md` | Gestione certificati SSL |
| `CLIENT_DEPLOYMENT.md` | Guide deployment client |

---

## ? Conclusioni

**Secure Boot Certificate Dashboard** fornisce:

1. ? **Visibilità completa** su stato Secure Boot di tutti i device
2. ? **Automazione** della raccolta dati e del deployment
3. ? **Controllo centralizzato** tramite dashboard web
4. ? **Scalabilità** per flotte enterprise (10.000+ device)
5. ? **Sicurezza enterprise-grade** con mTLS e audit trail
6. ? **Integrazione** con strumenti Microsoft esistenti

**Risultato**: Preparazione fluida per Windows 11 e protezione contro la scadenza certificati UEFI.

---

**Documento Versione**: 1.0  
**Data**: Dicembre 2024  
**Classificazione**: Tecnico - Pubblico
