# SecureBootWatcher - Guida al Deployment

Documentazione completa per il deployment di SecureBootWatcher v1.14 (architettura unificata Web+API).

## 📚 Documentazione Disponibile

### 🚀 Quick Start (15 minuti)
**[QUICKSTART_LOCAL.md](QUICKSTART_LOCAL.md)**
- Deployment rapido su server locale
- Configurazione minima
- Test immediato
- **Raccomandato per**: Test, POC, ambienti di sviluppo

### 📖 Guida Completa Deployment Locale
**[DEPLOYMENT_GUIDE_LOCAL.md](DEPLOYMENT_GUIDE_LOCAL.md)**
- Deployment dettagliato step-by-step (700+ righe)
- Prerequisiti e installazione componenti
- Configurazione SQL Server e IIS
- Setup client PowerShell
- Troubleshooting completo
- Manutenzione e backup
- Sicurezza e hardening
- **Raccomandato per**: Produzione, deployment enterprise

### 🤖 Script di Automazione
**[../scripts/Deploy-LocalServer.ps1](../scripts/Deploy-LocalServer.ps1)**
- Deployment automatizzato completo
- Validazione prerequisiti
- Configurazione database automatica
- Setup IIS e certificati
- Configurazione client
- **Raccomandato per**: Deployment ripetibili, standardizzazione

---

## 🎯 Scenari di Deployment

### Scenario 1: Test Rapido (Sviluppatore)
```powershell
# Prerequisiti: .NET 10, IIS, SQL Express già installati

cd scripts
.\Deploy-LocalServer.ps1 -DeployApplication -ConfigureClient

# Accedere a: http://localhost
```
**Tempo**: ~5 minuti  
**Guida**: QUICKSTART_LOCAL.md  
**Progetto deployato**: SecureBootDashboard.Web (applicazione unificata Web+API)

---

### Scenario 2: Ambiente di Produzione (IT Pro)
```powershell
# Deployment completo con tutti i prerequisiti

cd scripts
.\Deploy-LocalServer.ps1 -All

# Oppure step by step seguendo DEPLOYMENT_GUIDE_LOCAL.md
```
**Tempo**: ~30 minuti  
**Guida**: DEPLOYMENT_GUIDE_LOCAL.md  
**Progetto deployato**: SecureBootDashboard.Web (applicazione unificata Web+API)

---

### Scenario 3: Enterprise Multi-Server

**Server 1: Database**
- Installare SQL Server Standard/Enterprise
- Eseguire script di creazione database
- Configurare backup e maintenance

**Server 2: Web/API**
- Deployment applicazione unificata
- Configurare IIS con load balancing
- Configurare HTTPS con certificato valido

**Client Fleet**
- Distribuire client PowerShell via GPO/Intune
- Configurare scheduled task
- Monitorare collezione dati

**Guida**: DEPLOYMENT_GUIDE_LOCAL.md + custom scripts

---

## 📋 Checklist Pre-Deployment

### Requisiti Sistema
- [ ] Windows Server 2019+ o Windows 10/11 Pro
- [ ] 8 GB RAM minimo (16 GB raccomandato)
- [ ] 20 GB spazio disco libero
- [ ] Connettività di rete per client

### Software Richiesto
- [ ] .NET 10 Runtime (ASP.NET Core Hosting Bundle)
- [ ] IIS 10+
- [ ] SQL Server 2019+ (Express, Standard, o Enterprise)
- [ ] PowerShell 5.1+

### Permessi
- [ ] Account amministratore locale
- [ ] Permessi creazione database SQL
- [ ] Permessi configurazione IIS
- [ ] Permessi apertura porte firewall (80, 443, 1433)

---

## 🏗️ Architettura Post-Deployment

**⚠️ Importante:** Sul branch `copilot/merge-web-app-and-api`, il progetto **SecureBootDashboard.Web** è l'applicazione unificata che contiene:
- ✅ Interfaccia Web (Razor Pages)
- ✅ API Controllers (tutti gli endpoint REST)
- ✅ SignalR Hubs (real-time updates)
- ✅ Database Layer (EF Core Context e Migrations)
- ✅ Servizi Backend (business logic)

Il progetto `SecureBootDashboard.Api` esiste ancora nella solution ma non viene utilizzato per il deployment.

```
┌─────────────────────────────────────────────────────────┐
│                   Client Devices                        │
│  ┌──────────────────────────────────────────────┐      │
│  │  SecureBootWatcher-Client.ps1                │      │
│  │  - Raccolta dati Secure Boot                 │      │
│  │  - Registry snapshots                        │      │
│  │  - Event log collection                      │      │
│  │  - Esecuzione via Scheduled Task            │      │
│  └──────────────┬───────────────────────────────┘      │
└─────────────────┼───────────────────────────────────────┘
                  │ HTTP POST
                  │ /api/SecureBootReports
                  ▼
┌─────────────────────────────────────────────────────────┐
│              Server IIS (localhost)                     │
│  ┌────────────────────────────────────────────────┐    │
│  │  SecureBootDashboard.Web                       │    │
│  │  (Applicazione Unificata Web + API)            │    │
│  │  ┌──────────────────────────────────────────┐ │    │
│  │  │  Web UI (Razor Pages)                    │ │    │
│  │  │  - Dashboard                             │ │    │
│  │  │  - Device List                           │ │    │
│  │  │  - Reports                               │ │    │
│  │  │  - Admin Panel                           │ │    │
│  │  └──────────────────────────────────────────┘ │    │
│  │  ┌──────────────────────────────────────────┐ │    │
│  │  │  API Controllers                         │ │    │
│  │  │  - SecureBootReports (ingestion)         │ │    │
│  │  │  - Devices (query)                       │ │    │
│  │  │  - Commands (remote config)              │ │    │
│  │  └──────────────────────────────────────────┘ │    │
│  │  ┌──────────────────────────────────────────┐ │    │
│  │  │  SignalR Hub (Real-time)                 │ │    │
│  │  │  - Live dashboard updates                │ │    │
│  │  └──────────────────────────────────────────┘ │    │
│  └────────────────┬───────────────────────────────┘    │
└───────────────────┼─────────────────────────────────────┘
                    │ Entity Framework Core
                    │ SQL Connection
                    ▼
┌─────────────────────────────────────────────────────────┐
│         SQL Server (localhost\SQLEXPRESS)               │
│  ┌────────────────────────────────────────────────┐    │
│  │  SecureBootWatcher Database                    │    │
│  │  - Devices                                     │    │
│  │  - SecureBootReports                           │    │
│  │  - DeviceAttributes                            │    │
│  │  - Commands                                    │    │
│  │  - ApplicationSettings                         │    │
│  └────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

---

## 🔧 Comandi Rapidi Post-Deployment

### Verifica Servizi
```powershell
# IIS
Get-WebSite -Name "SecureBootWatcher"
Get-WebAppPoolState -Name "SecureBootWatcher"

# SQL Server
Get-Service -Name "MSSQL$SQLEXPRESS"

# Test API
Invoke-RestMethod -Uri "http://localhost/health"
```

### Accesso Dashboard
```powershell
# Aprire dashboard
Start-Process "http://localhost"

# URL principali:
# http://localhost/                   (Home)
# http://localhost/Devices/List       (Dispositivi)
# http://localhost/Reports            (Report)
# http://localhost/swagger            (API docs)
```

### Test Client
```powershell
# Eseguire client manualmente
cd C:\SecureBootWatcher\Client
.\SecureBootWatcher-Client.ps1 -RunMode Once -Verbose

# Verificare log
Get-Content "logs\client.log" -Tail 50
```

### Verifica Database
```sql
USE SecureBootWatcher
GO

-- Conta dispositivi
SELECT COUNT(*) FROM Devices

-- Ultimi 10 report
SELECT TOP 10 * FROM SecureBootReports ORDER BY CreatedAtUtc DESC
```

---

## 📊 Monitoraggio

### Health Check Endpoint
```powershell
# Status applicazione
Invoke-RestMethod -Uri "http://localhost/health" | ConvertTo-Json

# Output atteso:
# {
#   "status": "Healthy",
#   "totalDuration": "00:00:00.1234567"
# }
```

### Log Files
```powershell
# Log applicazione
Get-Content "C:\SecureBootWatcher\Logs\app-*.log" -Tail 100

# Log IIS stdout
Get-Content "C:\SecureBootWatcher\App\logs\stdout*.log" -Tail 100

# Log client
Get-Content "C:\SecureBootWatcher\Client\logs\client.log" -Tail 50
```

### Performance Counters
```powershell
# CPU Application Pool
Get-Counter "\Process(w3wp)\% Processor Time"

# Memoria
Get-Counter "\Process(w3wp)\Working Set"

# Richieste HTTP
Get-Counter "\Web Service(_Total)\Current Connections"
```

---

## 🔒 Sicurezza

### Checklist Sicurezza Base
- [ ] HTTPS configurato (certificato valido in produzione)
- [ ] SQL Server con strong password
- [ ] Firewall configurato (solo porte necessarie aperte)
- [ ] IIS hardening applicato
- [ ] Log audit abilitati
- [ ] Backup database configurato
- [ ] Account service con least privilege

### Comandi Sicurezza
```powershell
# Verificare binding HTTPS
Get-WebBinding -Name "SecureBootWatcher" | Where-Object {$_.protocol -eq "https"}

# Verificare firewall
Get-NetFirewallRule | Where-Object {$_.DisplayName -like "*SecureBoot*"}

# Verificare permessi SQL
sqlcmd -S localhost\SQLEXPRESS -Q "USE SecureBootWatcher; SELECT * FROM sys.database_principals WHERE name = 'SecureBootWatcherApp'"
```

---

## 🆘 Supporto e Troubleshooting

### Problemi Comuni

| Problema | Soluzione Rapida | Guida Dettaglio |
|----------|------------------|-----------------|
| HTTP 500.30 | Verificare .NET Runtime installato | DEPLOYMENT_GUIDE_LOCAL.md §8.1 |
| SQL Connection Failed | Verificare servizio SQL e firewall | DEPLOYMENT_GUIDE_LOCAL.md §8.1 |
| Client non invia dati | Verificare URL in appsettings.json | QUICKSTART_LOCAL.md |
| SignalR non funziona | Verificare WebSocket abilitato | DEPLOYMENT_GUIDE_LOCAL.md §8.1 |

### Risorse Aggiuntive
- **Troubleshooting Porte**: `docs/TROUBLESHOOTING_PORTS.md`
- **Release Notes**: `docs/RELEASE_NOTES_*.md`
- **GitHub Issues**: https://github.com/robgrame/Nimbus.BootCertWatcher/issues

---

## 📈 Roadmap e Updates

### Versione Corrente: 1.14
- ✅ Unificazione Web + API
- ✅ Rimozione Azure Queue dependency
- ✅ Semplificazione architettura
- ✅ SignalR real-time updates
- ✅ Deployment automation

### Future Enhancements
- 🔄 Azure deployment guide
- 🔄 Docker containerization
- 🔄 Kubernetes deployment
- 🔄 Intune packaging guide
- 🔄 SCCM deployment guide

---

## 👥 Contributi

Per contribuire al progetto:
1. Fork del repository
2. Branch feature: `git checkout -b feature/nome-feature`
3. Commit: `git commit -m 'feat: descrizione'`
4. Push: `git push origin feature/nome-feature`
5. Pull Request su GitHub

---

## 📄 License

Vedere file `LICENSE` nella root del repository.

---

## 🙏 Crediti

**Progetto**: SecureBootWatcher  
**Versione**: 1.14  
**Branch**: copilot/merge-web-app-and-api  
**Team**: Nimbus SecureBootWatcher  
**Repository**: https://github.com/robgrame/Nimbus.BootCertWatcher

---

**Data ultimo aggiornamento**: Dicembre 2024  
**Documentazione per versione**: 1.14 (Unified Architecture)
