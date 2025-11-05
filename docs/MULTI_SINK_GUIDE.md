# Multi-Sink con Priority & Failover - Guida Completa

## ?? Obiettivo

Configurare il **SecureBootWatcher Client** per inviare report a **multipli sink** con **ordine di priorità** e **failover automatico** in caso di errori.

## ? Caratteristiche

- ? **Priority Order**: Definisci ordine di esecuzione dei sink
- ? **Failover Automatico**: Se il primo fallisce, prova il secondo
- ? **Dual Strategy**: StopOnFirstSuccess (veloce) o TryAll (ridondante)
- ? **Success Tracking**: Log dettagliati di successi e fallimenti
- ? **Configurabile**: Nessuna modifica codice, solo appsettings.json

## ?? Configurazione

### Parametri Nuovi

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "AzureQueue,WebApi,FileShare"
  }
}
```

| Parametro | Tipo | Default | Descrizione |
|-----------|------|---------|-------------|
| `ExecutionStrategy` | string | `"StopOnFirstSuccess"` | `"StopOnFirstSuccess"` o `"TryAll"` |
| `SinkPriority` | string | `"AzureQueue,WebApi,FileShare"` | Ordine di esecuzione comma-separated |

### Execution Strategies

#### 1. StopOnFirstSuccess (DEFAULT)

**Comportamento**: Si ferma al primo sink che ha successo.

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "AzureQueue,WebApi,FileShare",
    "EnableAzureQueue": true,
    "EnableWebApi": true,
    "EnableFileShare": true
  }
}
```

**Flusso**:
```
1. Prova AzureQueue ? ? Successo ? STOP
   (WebApi e FileShare non vengono tentati)

Se fallisce:
1. Prova AzureQueue ? ? Fallito
2. Prova WebApi ? ? Successo ? STOP
   (FileShare non viene tentato)

Se tutti falliscono:
1. Prova AzureQueue ? ? Fallito
2. Prova WebApi ? ? Fallito
3. Prova FileShare ? ? Fallito
? Errore: AggregateException
```

**Logs Esempio**:
```
[Information] Sending report using strategy: StopOnFirstSuccess. Enabled sinks: AzureQueue, WebApi, FileShare
[Debug] Attempting to send report to AzureQueue...
[Information] ? Successfully sent report to AzureQueue
[Information] StopOnFirstSuccess strategy: stopping after first successful sink.
[Information] Report delivery summary: 1 succeeded, 0 failed.
```

**Quando Usarlo**:
- ? Vuoi massimizzare velocità
- ? Non serve ridondanza
- ? Risparmio risorse (network, CPU, disk)
- ? Produzione standard

#### 2. TryAll

**Comportamento**: Prova TUTTI i sink abilitati indipendentemente dai successi.

```json
{
  "Sinks": {
 "ExecutionStrategy": "TryAll",
  "SinkPriority": "AzureQueue,WebApi,FileShare",
    "EnableAzureQueue": true,
    "EnableWebApi": true,
    "EnableFileShare": true
  }
}
```

**Flusso**:
```
1. Prova AzureQueue ? ? Successo ? CONTINUA
2. Prova WebApi ? ? Successo ? CONTINUA
3. Prova FileShare ? ? Successo ? FINE
Risultato: Report in 3 posti
```

**Logs Esempio** (tutto successo):
```
[Information] Sending report using strategy: TryAll. Enabled sinks: AzureQueue, WebApi, FileShare
[Information] ? Successfully sent report to AzureQueue
[Information] ? Successfully sent report to WebApi
[Information] ? Successfully sent report to FileShare
[Information] Report delivery summary: 3 succeeded, 0 failed.
```

**Logs Esempio** (con errori):
```
[Information] Sending report using strategy: TryAll. Enabled sinks: AzureQueue, WebApi, FileShare
[Information] ? Successfully sent report to AzureQueue
[Warning] ? Failed to send report to WebApi: Connection timeout after 30 seconds
[Information] ? Successfully sent report to FileShare
[Information] Report delivery summary: 2 succeeded, 1 failed.
```

**Quando Usarlo**:
- ? Massima ridondanza
- ? Compliance richiede backup multipli
- ? Dati critici (zero perdite)
- ?? Performance non prioritaria

---

## ?? Scenari di Utilizzo

### Scenario 1: Cloud Primary + Local Fallback

**Obiettivo**: Azure Queue come primario, fallback su FileShare locale se cloud non raggiungibile.

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "AzureQueue,FileShare",
    "EnableAzureQueue": true,
    "EnableFileShare": true,
    "EnableWebApi": false,
    "AzureQueue": {
    "QueueServiceUri": "https://stsecurebootprod.queue.core.windows.net",
      "QueueName": "secureboot-reports",
  "AuthenticationMethod": "Certificate"
    },
    "FileShare": {
      "RootPath": "C:\\ProgramData\\SecureBootWatcher\\reports",
      "FileExtension": ".json",
      "AppendTimestampToFileName": true
    }
  }
}
```

**Vantaggi**:
- ? Cloud-first (scalabile, centralizzato)
- ? Fallback locale (zero perdita dati)
- ? Veloce (stop al primo successo)

**Quando Usarlo**:
- Dispositivi con connettività intermittente
- Vuoi garantire zero perdita dati
- Cloud preferito ma non garantito

**Flusso**:
```
Connettività OK:
AzureQueue ? ? Successo ? Report in cloud ? FileShare mai usato

Connettività Down:
AzureQueue ? ? Timeout ? FileShare ? ? Successo ? Report locale
(Quando cloud torna online, API background service può processare file locali)
```

---

### Scenario 2: API Direct + Queue Backup

**Obiettivo**: WebAPI per feedback immediato, Azure Queue come buffer se API non disponibile.

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
  "SinkPriority": "WebApi,AzureQueue",
    "EnableWebApi": true,
    "EnableAzureQueue": true,
"EnableFileShare": false,
  "WebApi": {
      "BaseAddress": "https://api-secureboot-prod.azurewebsites.net",
      "IngestionRoute": "/api/secureboot/reports",
   "HttpTimeout": "00:00:30"
    },
    "AzureQueue": {
      "QueueServiceUri": "https://stsecurebootprod.queue.core.windows.net",
      "QueueName": "secureboot-reports",
"AuthenticationMethod": "Certificate"
    }
  }
}
```

**Vantaggi**:
- ? API diretta = risposta immediata
- ? Queue backup = resilienza
- ? Nessun file locale da gestire

**Quando Usarlo**:
- Vuoi risposta immediata dall'API
- Connettività generalmente buona
- Azure Queue come safety net

**Flusso**:
```
API disponibile:
WebApi ? ? 200 OK ? Report salvato in database ? Queue mai usata

API down (maintenance/deployment):
WebApi ? ? 503 Service Unavailable ? AzureQueue ? ? Successo
? Report in queue, API background service lo processerà appena torna online
```

---

### Scenario 3: Triple Redundancy (Paranoid Mode)

**Obiettivo**: Salva report in TUTTI i sink per massima ridondanza.

```json
{
  "Sinks": {
    "ExecutionStrategy": "TryAll",
    "SinkPriority": "AzureQueue,WebApi,FileShare",
    "EnableAzureQueue": true,
    "EnableWebApi": true,
    "EnableFileShare": true,
  "AzureQueue": {
      "QueueServiceUri": "https://stsecurebootprod.queue.core.windows.net",
    "QueueName": "secureboot-reports",
      "AuthenticationMethod": "Certificate"
    },
    "WebApi": {
"BaseAddress": "https://api-secureboot-prod.azurewebsites.net",
      "IngestionRoute": "/api/secureboot/reports"
    },
    "FileShare": {
      "RootPath": "C:\\ProgramData\\SecureBootWatcher\\reports",
      "FileExtension": ".json",
      "AppendTimestampToFileName": true
    }
  }
}
```

**Vantaggi**:
- ? Report in 3 posti contemporaneamente
- ? Anche se 2/3 falliscono, hai ancora una copia
- ? Audit trail completo

**Svantaggi**:
- ?? Più lento (3x write operations)
- ?? Più risorse (network, disk, CPU)
- ?? Possibili duplicati in database (gestire via deduplication)

**Quando Usarlo**:
- Compliance richiede backup multipli
- Dati critici assolutamente NON perdibili
- Performance non è una priorità
- Audit/forensics richiede ridondanza

**Flusso**:
```
Tutti e 3 i sink vengono sempre tentati:
1. AzureQueue ? ? Successo
2. WebApi ? ? Successo
3. FileShare ? ? Successo
Risultato: Report salvato in 3 posti

Anche con errori parziali:
1. AzureQueue ? ? Timeout
2. WebApi ? ? Successo
3. FileShare ? ? Successo
Risultato: Report salvato in 2/3 posti (accettabile)
```

---

### Scenario 4: Local-Only (Air-Gapped)

**Obiettivo**: Solo FileShare locale per dispositivi senza internet.

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "FileShare",
    "EnableFileShare": true,
    "EnableAzureQueue": false,
    "EnableWebApi": false,
    "FileShare": {
      "RootPath": "\\\\domain-fileserver\\SecureBootReports",
      "FileExtension": ".json",
    "AppendTimestampToFileName": true
    }
  }
}
```

**Vantaggi**:
- ? Funziona offline
- ? Nessuna dipendenza cloud
- ? Veloce (local I/O)
- ? Sicuro (air-gapped)

**Quando Usarlo**:
- Air-gapped environments
- Dispositivi senza connettività internet
- Test/sviluppo locale
- Security requirements (no cloud)

---

## ?? Matrice Decisione

```
??????????????????????????????????????????????????????????????????????????????
? Scenario     ? Strategy        ? Priority     ? Abilitati  ?
??????????????????????????????????????????????????????????????????????????????
? Velocità massima        ? StopOnFirst ? Queue        ? Solo Queue ?
? Cloud + fallback locale     ? StopOnFirst ? Queue,File   ? Queue,File ?
? API + queue backup       ? StopOnFirst ? API,Queue    ? API,Queue  ?
? Massima ridondanza  ? TryAll      ? Queue,API,F  ? Tutti e 3  ?
? Offline/Air-gapped    ? StopOnFirst ? File         ? Solo File  ?
? Alta disponibilità  ? StopOnFirst ? Queue,API,F  ? Tutti e 3  ?
??????????????????????????????????????????????????????????????????????????????
```

## ?? Raccomandazioni per Priority Order

### Performance-First (DEFAULT)
```
"SinkPriority": "AzureQueue,WebApi,FileShare"
```
- **AzureQueue** = Asincrono (più veloce)
- **WebApi** = Sincrono ma diretto
- **FileShare** = Local I/O (fallback)

### Feedback-First
```
"SinkPriority": "WebApi,AzureQueue,FileShare"
```
- **WebApi** = Risposta immediata
- **AzureQueue** = Buffer se API down
- **FileShare** = Ultimo resort

### Offline-First
```
"SinkPriority": "FileShare,AzureQueue,WebApi"
```
- **FileShare** = Sempre disponibile
- **AzureQueue** = Secondary
- **WebApi** = Tertiary

### Ridondanza Completa (con TryAll)
```
"SinkPriority": "AzureQueue,FileShare,WebApi"
```
- **AzureQueue** = Processing asincrono
- **FileShare** = Audit locale
- **WebApi** = Visibilità immediata

---

## ?? Troubleshooting

### Problema: Tutti i Sink Falliscono

**Sintomi**: Log mostra "Report delivery failed: All N enabled sink(s) failed"

**Diagnosi**:
```powershell
# Test manuale ogni sink
cd "C:\Program Files\SecureBootWatcher"

# Abilita debug logging
# In appsettings.json: "Default": "Debug"

.\SecureBootWatcher.Client.exe
```

**Soluzione**:
1. Verifica configurazione ogni sink (URLs, paths, credentials)
2. Test connettività manualmente
3. Controlla logs per errori specifici

---

### Problema: Solo Alcuni Sink Funzionano

**Sintomi**: "Report delivery summary: 1 succeeded, 2 failed"

**Diagnosi**:
```
[Information] ? Successfully sent report to AzureQueue
[Warning] ? Failed to send report to WebApi: 503 Service Unavailable
[Warning] ? Failed to send report to FileShare: Access denied to \\server\share
```

**Soluzione**:
- Se almeno 1 sink ha successo ? Report non perso, ma verifica i fallimenti
- Controlla permissions, network, configurazione per i sink falliti

---

### Problema: TryAll Strategy Troppo Lenta

**Sintomi**: Client impiega 60+ secondi per completare

**Soluzione**:
```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",  // Cambia da TryAll
    "SinkPriority": "AzureQueue,FileShare"  // Riduci numero sink
  }
}
```

Oppure riduci timeout:
```json
{
  "WebApi": {
 "HttpTimeout": "00:00:10"  // Da 30s a 10s
  },
  "AzureQueue": {
    "MaxSendRetryCount": 3  // Da 5 a 3
  }
}
```

---

## ? Best Practices

### Production (Enterprise Deployment)

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "AzureQueue,FileShare",
    "EnableAzureQueue": true,
    "EnableFileShare": true,
    "EnableWebApi": false
  }
}
```

**Rationale**:
- ? Azure Queue primario (scalabile, resiliente)
- ? FileShare fallback (zero perdita dati)
- ? Veloce (stop al primo successo)
- ? Bilanciato (performance + affidabilità)

### High-Availability / Critical Systems

```json
{
  "Sinks": {
    "ExecutionStrategy": "TryAll",
    "SinkPriority": "AzureQueue,FileShare,WebApi",
    "EnableAzureQueue": true,
    "EnableFileShare": true,
    "EnableWebApi": true
  }
}
```

**Rationale**:
- ? Massima ridondanza (3 copie)
- ? Tolleranza a 2/3 fallimenti
- ? Audit completo

### Development / Testing

```json
{
  "Sinks": {
    "ExecutionStrategy": "StopOnFirstSuccess",
    "SinkPriority": "FileShare",
    "EnableFileShare": true,
    "EnableAzureQueue": false,
    "EnableWebApi": false,
    "FileShare": {
      "RootPath": "C:\\Dev\\SecureBootReports"
    }
  }
}
```

**Rationale**:
- ? Solo locale (no cloud dependencies)
- ? Veloce
- ? Facile debug (files su disco)

---

## ?? Riferimenti

- **Configuration**: `appsettings.multi-sink.json` (esempi completi)
- **Architecture**: `docs/ARCHITECTURE_DIAGRAM.md`
- **Deployment**: `docs/CLIENT_DEPLOYMENT_GUIDE.md`

---

**Multi-Sink con priorità e failover implementato! ???**
