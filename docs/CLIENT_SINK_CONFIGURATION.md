# Client Sink Configuration - Database-Driven Setup

## Overview

La configurazione dei sinks dei client è ora gestita centralmente nel database e può essere modificata tramite l'interfaccia web, senza necessità di modificare i file `appsettings.json` dei client.

## Funzionalità Principali

### 1. Gestione Centralizzata
- Tutte le configurazioni dei sinks sono memorizzate nel database
- I client recuperano la configurazione attiva tramite API
- Modifiche immediate senza ridistribuire i client

### 2. Supporto Multi-Sink
- **Web API**: Invio diretto HTTP POST all'endpoint API (raccomandato)
- **Azure Queue**: Messaggi accodati in Azure Storage Queue per elaborazione asincrona
- **FileShare**: File JSON scritti su condivisione di rete (per ambienti air-gapped)

### 3. Strategie di Esecuzione
- **StopOnFirstSuccess**: Si ferma dopo il primo sink riuscito (più veloce, default)
- **TryAll**: Invia a tutti i sinks abilitati (ridondanza, più lento)

### 4. Retry e Failover
- Retry configurabili per sink
- Exponential backoff opzionale
- Priorità personalizzabile

## Componenti Creati

### Database
- **Tabella**: `ClientSinkConfig`
- **Migration**: `20251204151938_AddClientSinkConfiguration`
- **Seed Data**: Configurazione predefinita con WebApi abilitato

### API
- **Controller**: `ClientSinkConfigController`
- **Endpoint principale**: `GET /api/ClientSinkConfig/active` (usato dai client)
- **Endpoint gestione**:
  - `GET /api/ClientSinkConfig` - Lista tutte le configurazioni
  - `GET /api/ClientSinkConfig/{id}` - Ottiene una configurazione specifica
  - `POST /api/ClientSinkConfig` - Crea nuova configurazione
  - `PUT /api/ClientSinkConfig/{id}` - Aggiorna configurazione
  - `POST /api/ClientSinkConfig/{id}/activate` - Attiva configurazione
  - `DELETE /api/ClientSinkConfig/{id}` - Elimina configurazione

### Web UI
- **Pagine Razor**:
  - `/Settings/SinkConfig/Index` - Lista configurazioni
  - `/Settings/SinkConfig/Edit` - Crea/Modifica configurazione
- **Menu Admin**: Nuovo link "Client Sink Configuration"

## Applicazione della Migration

### Opzione 1: Via Command Line (Sviluppo)

```powershell
cd SecureBootDashboard.Api
dotnet ef database update
```

### Opzione 2: Via Script (Produzione)

```powershell
# Genera script SQL
cd SecureBootDashboard.Api
dotnet ef migrations script --idempotent --output migration-clientsink.sql

# Applica manualmente su SQL Server
sqlcmd -S SRVSQL -d SecureBootDashboard -i migration-clientsink.sql
```

## Utilizzo dell'Interfaccia Web

### Accesso
1. Naviga su Dashboard
2. Menu Admin ? Client Sink Configuration

### Creare una Nuova Configurazione
1. Click su "New Configuration"
2. Configura le impostazioni generali:
   - Execution Strategy
   - Sink Priority
   - Retry attempts e delay
3. Configura i sinks tramite le tabs:
   - **Web API**: URL, timeout, certificati mTLS
   - **Azure Queue**: URI, autenticazione (Managed Identity, App Registration, Certificate, ecc.)
   - **FileShare**: Percorso UNC o locale
4. Salva la configurazione

### Attivare una Configurazione
1. Nella lista, trova la configurazione desiderata
2. Click sul pulsante "Activate" (icona power)
3. Conferma l'attivazione
4. Solo una configurazione può essere attiva alla volta

### Modificare la Configurazione Attiva
1. Click su "Edit" (icona matita)
2. Modifica i parametri desiderati
3. Salva le modifiche
4. I client recupereranno la nuova configurazione al prossimo polling

## Integrazione Client

I client esistenti **non richiedono modifiche** se continuano a usare i file appsettings.json.

Per abilitare la configurazione da database:

### 1. Modificare il Client per Recuperare la Configurazione

```csharp
// In SecureBootWatcher.Client/Program.cs o Startup
public static async Task<SinkOptions> GetSinkConfigurationFromApi()
{
    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync("https://api.example.com/api/ClientSinkConfig/active");
    
    if (response.IsSuccessStatusCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SinkOptions>(json);
    }
    
    // Fallback a configurazione locale
    return new SinkOptions();
}
```

### 2. Aggiornare IOptionsMonitor

```csharp
// Sostituire la configurazione statica con quella dinamica
services.Configure<SecureBootWatcherOptions>(options =>
{
    var sinkConfig = GetSinkConfigurationFromApi().Result;
    options.Sinks = sinkConfig;
});
```

## Configurazioni di Esempio

### 1. Solo Web API (Semplice)

```
Enabled Sinks: Web API
Execution Strategy: StopOnFirstSuccess
Priority: WebApi
Max Retry: 3
Retry Delay: 5 minuti

Web API:
- Base Address: https://api.example.com
- Ingestion Route: /api/SecureBootReports
- Timeout: 30 secondi
- Certificate Auth: No
```

### 2. Azure Queue + Web API (Failover)

```
Enabled Sinks: Azure Queue, Web API
Execution Strategy: StopOnFirstSuccess
Priority: AzureQueue,WebApi
Max Retry: 3
Retry Delay: 5 minuti
Exponential Backoff: Yes

Azure Queue:
- Service URI: https://mystorageaccount.queue.core.windows.net
- Queue Name: secureboot-reports
- Auth Method: Managed Identity

Web API (fallback):
- Base Address: https://api-backup.example.com
- Ingestion Route: /api/SecureBootReports
```

### 3. Tutti i Sinks (Massima Ridondanza)

```
Enabled Sinks: Web API, Azure Queue, FileShare
Execution Strategy: TryAll
Priority: WebApi,AzureQueue,FileShare
Max Retry: 5
Retry Delay: 2 minuti

Web API:
- Base Address: https://api.example.com
- Ingestion Route: /api/SecureBootReports

Azure Queue:
- Service URI: https://mystorageaccount.queue.core.windows.net
- Queue Name: secureboot-reports
- Auth Method: App Registration
- Client ID: xxx-xxx-xxx
- Tenant ID: yyy-yyy-yyy

FileShare:
- Root Path: \\server\share\reports
- Extension: .json
- Append Timestamp: Yes
```

## Sicurezza

### Dati Sensibili
Le seguenti proprietà sono considerate sensibili e devono essere protette:
- `AzureQueueConnectionString`
- `AzureQueueClientSecret`
- `AzureQueueCertPassword`
- `WebApiCertPassword`

### Best Practices
1. **Non usare Connection String in produzione** - Preferire Managed Identity o App Registration
2. **Usare certificati per mTLS** quando possibile
3. **Limitare i permessi del database** - Solo API deve poter scrivere
4. **Backup regolari** della tabella ClientSinkConfig
5. **Versioning** - Mantenere configurazioni storiche per rollback

## Troubleshooting

### I client non ricevono la nuova configurazione
- Verificare che ci sia una configurazione con `IsActive = true`
- Controllare i log del controller API
- Verificare la connettività client ? API

### Errore "No active configuration found"
- Creare almeno una configurazione e attivarla
- Verificare che il record esista nel database con query:
  ```sql
  SELECT * FROM ClientSinkConfig WHERE IsActive = 1
  ```

### Errori di autenticazione Azure Queue
- Verificare che Client ID, Tenant ID siano corretti
- Per Managed Identity, verificare che sia assegnata al servizio
- Per App Registration, verificare che abbia i permessi sulla Storage Account

## Migrazione da appsettings.json

### Passo 1: Creare Configurazione nel Database
1. Accedere alla pagina Settings/SinkConfig
2. Creare nuova configurazione con i valori da appsettings.json
3. Attivarla

### Passo 2: Testare in Ambiente di Test
1. Deployare un client di test
2. Verificare che recuperi la configurazione
3. Confermare invio report

### Passo 3: Rollout Graduale
1. Aggiornare i client per gruppo
2. Monitorare i log
3. Verificare che i report arrivino

### Passo 4: Rimuovere Configurazione Locale (Opzionale)
1. Una volta confermato il funzionamento
2. Rimuovere le sezioni Sinks da appsettings.json
3. Mantenere un fallback per disaster recovery

## Monitoraggio

### Metriche da Monitorare
- Numero di configurazioni nel database
- Ultima modifica della configurazione attiva
- Numero di client che recuperano la configurazione (log API)
- Successo/fallimento dei sinks (log client)

### Log Importanti
API:
```
Returned active sink configuration (ID: 1)
Created new sink configuration 2 by admin@example.com
Activated sink configuration 2 by admin@example.com
```

Client:
```
Sending report using strategy: StopOnFirstSuccess. Enabled sinks: WebApi
Successfully sent report to WebApi
```

## Riferimenti

- **Migration**: `SecureBootDashboard.Api/Data/Migrations/20251204151938_AddClientSinkConfiguration.cs`
- **Entity**: `SecureBootDashboard.Api/Data/ClientSinkConfigEntity.cs`
- **Controller**: `SecureBootDashboard.Api/Controllers/ClientSinkConfigController.cs`
- **View Model**: `SecureBootDashboard.Web/Pages/Settings/SinkConfig/Index.cshtml.cs`
- **Razor Pages**: `SecureBootDashboard.Web/Pages/Settings/SinkConfig/*.cshtml`

## Supporto

Per problemi o domande:
1. Controllare i log dell'API e del client
2. Verificare la configurazione nel database
3. Consultare la documentazione Azure per autenticazione
4. Contattare il team di sviluppo

---

**Versione**: 1.0  
**Data**: 2025-01-14  
**Autore**: GitHub Copilot
