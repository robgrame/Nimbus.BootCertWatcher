<#
.SYNOPSIS
    Translates all Italian text to English in the SecureBootDashboard.Web project.

.DESCRIPTION
    This script performs comprehensive translation of Italian strings to English
    across all Razor Pages (.cshtml and .cshtml.cs files), including the new
    Welcome and About pages.

.EXAMPLE
    .\Translate-AllToEnglish.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Complete Translation: Italian to English" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Comprehensive translation mappings
$translations = @{
    # Page titles and headers
    "Benvenuto" = "Welcome"
    "Monitoraggio Certificati Secure Boot" = "Secure Boot Certificate Monitoring"
    "Sistema completo per il monitoraggio e la gestione dei certificati Secure Boot sui dispositivi Windows della tua infrastruttura aziendale\." = "Complete system for monitoring and managing Secure Boot certificates on Windows devices in your enterprise infrastructure."
    "Accedi al Portale" = "Access Portal"
    "Dashboard Analitica" = "Analytics Dashboard"
    "Gestione Dispositivi" = "Device Management"
    "Tracking Certificati" = "Certificate Tracking"
    "Sicurezza Enterprise" = "Enterprise Security"
    "Tracciamento dettagliato dei certificati UEFI" = "Detailed tracking of UEFI certificates"
    "Autenticazione aziendale sicura e protetta" = "Secure enterprise authentication"
    
    # About page
    "è una soluzione completa per il monitoraggio e la gestione dei certificati Secure Boot su dispositivi Windows enterprise\." = "is a complete solution for monitoring and managing Secure Boot certificates on enterprise Windows devices."
    "Il sistema cattura automaticamente lo stato dei certificati dai registri Windows e dagli event log, centralizzando le informazioni in una dashboard web per analisi, reporting e compliance tracking\." = "The system automatically captures certificate status from Windows registries and event logs, centralizing information in a web dashboard for analysis, reporting, and compliance tracking."
    "Scopo Principale" = "Main Purpose"
    "Tracciare il rollout del" = "Track the rollout of the"
    "e identificare dispositivi che richiedono intervento prima della scadenza dei certificati legacy\." = "and identify devices that require intervention before legacy certificate expiration."
    "Architettura" = "Architecture"
    "Polling registri Secure Boot" = "Polling Secure Boot registries"
    "Cattura Windows Event Log" = "Capture Windows Event Log"
    "Invio dati via HTTP/Queue/File" = "Send data via HTTP/Queue/File"
    "Esecuzione schedulata \(Task Scheduler\)" = "Scheduled execution (Task Scheduler)"
    "Ingestion endpoint REST" = "REST ingestion endpoint"
    "Azure Queue processor" = "Azure Queue processor"
    "EF Core / File storage" = "EF Core / File storage"
    "Health checks & monitoring" = "Health checks & monitoring"
    "Visualizzazione compliance" = "Compliance visualization"
    "Grafici interattivi \(Chart\.js\)" = "Interactive charts (Chart.js)"
    "Filtri e ricerca dispositivi" = "Device filters and search"
    "Report history & details" = "Report history & details"
    "SQL Server \(Azure SQL / on-prem\)" = "SQL Server (Azure SQL / on-prem)"
    "Azure Queue Storage \(optional\)" = "Azure Queue Storage (optional)"
    "File-based storage \(air-gapped\)" = "File-based storage (air-gapped)"
    "Deduplication automatica" = "Automatic deduplication"
    "Funzionalità Principali" = "Main Features"
    "Grafici compliance status \(pie/doughnut\)" = "Compliance status charts (pie/doughnut)"
    "Trend temporale \(ultimi 7 days\)" = "Temporal trend (last 7 days)"
    "Deployment state distribution" = "Deployment state distribution"
    "Card statistiche cliccabili" = "Clickable statistics cards"
    "Lista completa dispositivi" = "Complete device list"
    "Filtri per stato, fleet, manufacturer" = "Filters by state, fleet, manufacturer"
    "Ricerca testuale avanzata" = "Advanced text search"
    "Badge per stati e compliance" = "Badges for states and compliance"
    "Report history per dispositivo" = "Report history per device"
    "Dettagli registro Secure Boot" = "Secure Boot registry details"
    "Certificate chain visualization" = "Certificate chain visualization"
    "Event log capture" = "Event log capture"
    "Managed Identity support" = "Managed Identity support"
    "Certificate-based auth" = "Certificate-based auth"
    "TLS/HTTPS enforcement" = "TLS/HTTPS enforcement"
    "RBAC & Network isolation" = "RBAC & Network isolation"
    "Technology Stack" = "Technology Stack"
    "Sviluppato con" = "Developed with"
    "dalla IT Community\." = "by the IT Community."
    "Visualizzazione in tempo reale dello stato di compliance" = "Real-time compliance status visualization"
    "Monitoraggio completo di tutti i dispositivi registrati" = "Complete monitoring of all registered devices"
    
    # Login page
    "Accedi con Microsoft Entra ID" = "Sign in with Microsoft Entra ID"
    "Accedi con Windows Domain" = "Sign in with Windows Domain"
    "Torna alla home" = "Back to home"
    "Informazioni" = "Information"
    "Autenticazione sicura enterprise" = "Secure enterprise authentication"
    "Gestione centralizzata degli accessi" = "Centralized access management"
    "Integrazione con Active Directory o Entra ID" = "Integration with Active Directory or Entra ID"
    
    # Common UI elements (from previous script)
    "Dispositivi Monitorati" = "Monitored Devices"
    "Torna alla Dashboard" = "Back to Dashboard"
    "Azioni Rapide" = "Quick Actions"
    "Nessun Dispositivo Registrato" = "No Registered Devices"
    "Non ci sono dispositivi da visualizzare\. I client devono ancora inviare i dati all'API\." = "No devices to display. Clients have not yet sent data to the API."
    "Prossimi passi:" = "Next steps:"
    "Configura il client SecureBootWatcher sui dispositivi Windows" = "Configure the SecureBootWatcher client on Windows devices"
    "Verifica che i client possano raggiungere l'API" = "Verify that clients can reach the API"
    "Attendi che i client inviino il primo report" = "Wait for clients to send the first report"
    "Errore nel caricamento dei dati\." = "Error loading data."
    "API non disponibile\. Verificare la connessione\." = "API unavailable. Check connection."
    "Real-time Attivo" = "Real-time Active"
    "Riconnessione\.\.\." = "Reconnecting..."
    "Disconnesso" = "Disconnected"
    "Connessione\.\.\." = "Connecting..."
    "Caricamento in corso\.\.\." = "Loading..."
    "Totale Dispositivi" = "Total Devices"
    "Attivi \(24h\)" = "Active (24h)"
    "Inattivi \(>7d\)" = "Inactive (>7d)"
    "Dispositivi Compliant" = "Compliant Devices"
    "Crescita Dispositivi Compliant" = "Compliant Devices Growth"
    "Distribuzione Stati" = "State Distribution"
    "Gestisci Errori" = "Manage Errors"
    "Monitora Pending" = "Monitor Pending"
    "Applica" = "Apply"
    "Filtrato" = "Filtered"
    "Cerca" = "Search"
    "Nome macchina, dominio, produttore\.\.\." = "Machine name, domain, manufacturer..."
    "Stato" = "Status"
    "Tutti" = "All"
    "Tutte" = "All"
    "Monitoraggio certificati Secure Boot su dispositivi Windows" = "Secure Boot certificate monitoring on Windows devices"
    "Clicca per dettagli" = "Click for details"
    "Lista completa dei dispositivi Windows con certificati Secure Boot" = "Complete list of Windows devices with Secure Boot certificates"
    "Totale" = "Total"
    "giorni" = "days"
    "Errore" = "Error"
    "Secure Boot Abilitato" = "Secure Boot Enabled"
    "Vedi tutti" = "View All"
    "Vedi attivi" = "View Active"
    "Vedi inattivi" = "View Inactive"
    "Vedi deployed" = "View Deployed"
    "Vedi pending" = "View Pending"
    "Vedi errori" = "View Errors"
    "Vedi dettagli" = "View Details"
    "Versioni Client" = "Client Versions"
    "Informazioni Dispositivo" = "Device Information"
    "Informazioni Report" = "Report Information"
    
    # New language attribute
    'lang="it"' = 'lang="en"'
}

# Get project root
$scriptPath = $PSScriptRoot
$projectRoot = Split-Path $scriptPath
$webProjectPath = Join-Path $projectRoot "SecureBootDashboard.Web"

if (-not (Test-Path $webProjectPath)) {
    Write-Host "? Web project not found at: $webProjectPath" -ForegroundColor Red
    exit 1
}

Write-Host "Web Project: $webProjectPath" -ForegroundColor Yellow
Write-Host ""

# Find all Razor files
$razorFiles = Get-ChildItem -Path $webProjectPath -Filter "*.cshtml" -Recurse | 
    Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

$razorCodeFiles = Get-ChildItem -Path $webProjectPath -Filter "*.cshtml.cs" -Recurse |
    Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

$allFiles = $razorFiles + $razorCodeFiles

Write-Host "Found $($allFiles.Count) files to process" -ForegroundColor Cyan
Write-Host ""

$filesChanged = 0
$totalReplacements = 0

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($projectRoot.Length + 1)
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    $fileReplacements = 0
    
    foreach ($italian in $translations.Keys) {
        $english = $translations[$italian]
        
        # Skip if same (already in English)
        if ($italian -eq $english) {
            continue
        }
        
        # Count occurrences before replacement
        $pattern = $italian
        $matches = [regex]::Matches($content, $pattern)
        
        if ($matches.Count -gt 0) {
            $content = $content -replace $pattern, $english
            $fileReplacements += $matches.Count
        }
    }
    
    if ($content -ne $originalContent) {
        Write-Host "  Updating: $relativePath" -ForegroundColor Green
        Write-Host "    Replacements: $fileReplacements" -ForegroundColor Gray
        
        # Backup original
        $backupPath = "$($file.FullName).bak"
        Copy-Item $file.FullName $backupPath -Force
        
        # Save updated content
        Set-Content $file.FullName -Value $content -Encoding UTF8 -NoNewline
        
        $filesChanged++
        $totalReplacements += $fileReplacements
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Translation Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Files Changed: $filesChanged" -ForegroundColor Green
Write-Host "Total Replacements: $totalReplacements" -ForegroundColor Green
Write-Host ""

if ($filesChanged -gt 0) {
    Write-Host "Backup files created with .bak extension" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Review changes: git diff" -ForegroundColor White
    Write-Host "  2. Test application: dotnet run --project SecureBootDashboard.Web" -ForegroundColor White
    Write-Host "  3. If satisfied, commit: git add .; git commit -m 'i18n: complete Italian to English translation'" -ForegroundColor White
    Write-Host "  4. If issues, restore: .\scripts\Restore-Backups.ps1" -ForegroundColor White
} else {
    Write-Host "No changes needed - all text already in English!" -ForegroundColor Green
}

Write-Host ""
