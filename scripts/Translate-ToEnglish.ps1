<#
.SYNOPSIS
    Translates Italian text to English across the SecureBootDashboard.Web project.

.DESCRIPTION
    This script performs automated text replacement to translate Italian strings
    to English in Razor Pages (.cshtml and .cshtml.cs files).

.EXAMPLE
    .\Translate-ToEnglish.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Text Translation: Italian to English" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Define translation mappings
$translations = @{
    # Page titles
    "Secure Boot Dashboard" = "Secure Boot Dashboard"  # Keep as is
    "Dispositivi Monitorati" = "Monitored Devices"
    "Versioni Client" = "Client Versions"
    "Informazioni Dispositivo" = "Device Information"
    "Informazioni Report" = "Report Information"
    "Report Details" = "Report Details"  # Keep as is
    
    # Navigation
    "Torna alla Dashboard" = "Back to Dashboard"
    "Vedi tutti" = "View All"
    "Vedi attivi" = "View Active"
    "Vedi inattivi" = "View Inactive"
    "Vedi deployed" = "View Deployed"
    "Vedi pending" = "View Pending"
    "Vedi errori" = "View Errors"
    "Vedi dettagli" = "View Details"
    
    # Statistics
    "Totale Dispositivi" = "Total Devices"
    "Attivi \(24h\)" = "Active (24h)"
    "Inattivi \(>7d\)" = "Inactive (>7d)"
    "Dispositivi Compliant" = "Compliant Devices"
    "Crescita Dispositivi Compliant" = "Compliant Devices Growth"
    
    # Charts
    "Compliance Status" = "Compliance Status"  # Keep as is
    "Deployment States" = "Deployment States"  # Keep as is
    "Compliance Trend \(7 giorni\)" = "Compliance Trend (7 days)"
    "Distribuzione Stati" = "State Distribution"
    
    # Actions
    "Azioni Rapide" = "Quick Actions"
    "Gestisci Errori" = "Manage Errors"
    "Monitora Pending" = "Monitor Pending"
    "Applica" = "Apply"
    "Reset" = "Reset"  # Keep as is
    "Filtrato" = "Filtered"
    
    # Device states
    "Deployed" = "Deployed"  # Keep as is
    "Pending" = "Pending"  # Keep as is
    "Error" = "Error"  # Keep as is
    "Unknown" = "Unknown"  # Keep as is
    "Inactive" = "Inactive"  # Keep as is
    "Compliant" = "Compliant"  # Keep as is
    "Non-Compliant" = "Non-Compliant"  # Keep as is
    
    # Table headers
    "Machine Name" = "Machine Name"  # Keep as is
    "Domain" = "Domain"  # Keep as is
    "Fleet" = "Fleet"  # Keep as is
    "Manufacturer / Model" = "Manufacturer / Model"  # Keep as is
    "Reports" = "Reports"  # Keep as is
    "State" = "State"  # Keep as is
    "Last Seen" = "Last Seen"  # Keep as is
    "Actions" = "Actions"  # Keep as is
    
    # Time references
    "min ago" = "min ago"  # Keep as is
    "hours ago" = "hours ago"  # Keep as is
    "days ago" = "days ago"  # Keep as is
    "giorni" = "days"
    
    # Messages
    "Nessun Dispositivo Registrato" = "No Registered Devices"
    "Non ci sono dispositivi da visualizzare\. I client devono ancora inviare i dati all'API\." = "No devices to display. Clients have not yet sent data to the API."
    "Prossimi passi:" = "Next steps:"
    "Configura il client SecureBootWatcher sui dispositivi Windows" = "Configure the SecureBootWatcher client on Windows devices"
    "Verifica che i client possano raggiungere l'API" = "Verify that clients can reach the API"
    "Attendi che i client inviino il primo report" = "Wait for clients to send the first report"
    "Errore nel caricamento dei dati\." = "Error loading data."
    "API non disponibile\. Verificare la connessione\." = "API unavailable. Check connection."
    
    # SignalR
    "Real-time Attivo" = "Real-time Active"
    "Riconnessione\.\.\." = "Reconnecting..."
    "Disconnesso" = "Disconnected"
    "Errore" = "Error"
    "Connessione\.\.\." = "Connecting..."
    
    # Client Versions page
    "Totale" = "Total"
    "Up-to-Date" = "Up-to-Date"  # Keep as is
    "Outdated" = "Outdated"  # Keep as is
    "Unsupported" = "Unsupported"  # Keep as is
    "Lista completa dei dispositivi Windows con certificati Secure Boot" = "Complete list of Windows devices with Secure Boot certificates"
    
    # Search and filters
    "Cerca" = "Search"
    "Nome macchina, dominio, produttore\.\.\." = "Machine name, domain, manufacturer..."
    "Stato" = "Status"
    "Tutti" = "All"
    "Tutte" = "All"
    
    # Dashboard
    "Monitoraggio certificati Secure Boot su dispositivi Windows" = "Secure Boot certificate monitoring on Windows devices"
    "Clicca per dettagli" = "Click for details"
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
        $pattern = [regex]::Escape($italian)
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
    Write-Host "  2. Test application: dotnet run" -ForegroundColor White
    Write-Host "  3. If satisfied, commit: git add .; git commit -m 'i18n: translate UI to English'" -ForegroundColor White
    Write-Host "  4. If issues, restore: .\scripts\Restore-Backups.ps1" -ForegroundColor White
} else {
    Write-Host "No changes needed - all text already in English!" -ForegroundColor Green
}

Write-Host ""

