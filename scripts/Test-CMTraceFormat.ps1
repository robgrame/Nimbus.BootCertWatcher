# Test CMTrace Log Format
# Questo script genera log di test per verificare la compatibilità con CMTrace

param(
    [Parameter(Mandatory = $false)]
    [string]$ClientPath = ".\bin\Debug\net48\SecureBootWatcher.Client.exe",
    
    [Parameter(Mandatory = $false)]
    [string]$CMTracePath = "C:\Program Files (x86)\Microsoft Endpoint Manager\AdminConsole\bin\CMTrace.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CMTrace Log Format Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verifica che il client esista
if (-not (Test-Path $ClientPath)) {
    Write-Error "Client non trovato: $ClientPath"
    Write-Host "Eseguire prima: dotnet build" -ForegroundColor Yellow
    exit 1
}

# Determina il percorso del log
$clientDir = Split-Path -Parent $ClientPath
$logDir = Join-Path $clientDir "logs"
$logPattern = Join-Path $logDir "client-*.log"

Write-Host "[1/4] Verifica percorsi..." -ForegroundColor Yellow
Write-Host "  Client: $ClientPath" -ForegroundColor Gray
Write-Host "  Log dir: $logDir" -ForegroundColor Gray
Write-Host ""

# Pulisci vecchi log per test pulito
Write-Host "[2/4] Pulizia vecchi log..." -ForegroundColor Yellow
if (Test-Path $logDir) {
    Get-ChildItem -Path $logDir -Filter "client-*.log" | Remove-Item -Force
    Write-Host "  Vecchi log rimossi" -ForegroundColor Green
} else {
    Write-Host "  Nessun log precedente" -ForegroundColor Gray
}
Write-Host ""

# Esegui il client brevemente
Write-Host "[3/4] Esecuzione client (5 secondi)..." -ForegroundColor Yellow
$process = Start-Process -FilePath $ClientPath -PassThru -WindowStyle Hidden

Start-Sleep -Seconds 5

if (!$process.HasExited) {
    $process.Kill()
    $process.WaitForExit()
}

Write-Host "  Client eseguito con successo" -ForegroundColor Green
Write-Host ""

# Verifica che i log siano stati creati
Write-Host "[4/4] Verifica formato log..." -ForegroundColor Yellow

$logFiles = Get-ChildItem -Path $logDir -Filter "client-*.log" -ErrorAction SilentlyContinue

if ($logFiles.Count -eq 0) {
    Write-Error "Nessun file di log generato!"
    exit 1
}

$logFile = $logFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host "  File di log: $($logFile.Name)" -ForegroundColor Gray
Write-Host "  Dimensione: $([math]::Round($logFile.Length / 1KB, 2)) KB" -ForegroundColor Gray
Write-Host ""

# Leggi e analizza alcune righe
$logLines = Get-Content $logFile.FullName | Select-Object -Last 10

$cmtraceCount = 0
$standardCount = 0

foreach ($line in $logLines) {
    if ($line -match "^<!\[LOG\[.*\]LOG\]!>") {
        $cmtraceCount++
    } elseif ($line -match "^\d{4}-\d{2}-\d{2}") {
        $standardCount++
    }
}

Write-Host "Analisi formato log:" -ForegroundColor Cyan
Write-Host "  Righe CMTrace format: $cmtraceCount" -ForegroundColor $(if ($cmtraceCount -gt 0) { "Green" } else { "Red" })
Write-Host "  Righe Standard format: $standardCount" -ForegroundColor $(if ($standardCount -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

if ($cmtraceCount -eq 0) {
    Write-Host "? ERRORE: Nessuna riga in formato CMTrace trovata!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Verifica in appsettings.json:" -ForegroundColor Yellow
    Write-Host '  "Logging": { "File": { "Format": "CMTrace" } }' -ForegroundColor Gray
    exit 1
}

# Mostra esempio di log
Write-Host "Esempio di riga CMTrace:" -ForegroundColor Cyan
$exampleLine = $logLines | Where-Object { $_ -match "^<!\[LOG\[" } | Select-Object -First 1

if ($exampleLine) {
    # Estrai solo la parte del messaggio per leggibilità
    if ($exampleLine -match "<!\[LOG\[(.*?)\]LOG\]!>") {
        $message = $matches[1]
        Write-Host "  Messaggio: $message" -ForegroundColor Gray
    }
    
    if ($exampleLine -match 'type="(\d)"') {
        $type = $matches[1]
        $typeText = switch ($type) {
            "1" { "Information (bianco)" }
            "2" { "Warning (giallo)" }
            "3" { "Error (rosso)" }
            default { "Sconosciuto" }
        }
        Write-Host "  Tipo: $typeText" -ForegroundColor Gray
    }
    
    if ($exampleLine -match 'time="([^"]+)"') {
        Write-Host "  Timestamp: $($matches[1])" -ForegroundColor Gray
    }
}

Write-Host ""

# Verifica componenti del formato
Write-Host "Verifica componenti formato CMTrace:" -ForegroundColor Cyan

$checks = @{
    "Message tag" = $logLines -match "<!\[LOG\[.*\]LOG\]!>"
    "Time attribute" = $logLines -match 'time="[^"]+"'
    "Date attribute" = $logLines -match 'date="[^"]+"'
    "Component attribute" = $logLines -match 'component="SecureBootWatcher.Client"'
    "Type attribute (1/2/3)" = $logLines -match 'type="[123]"'
    "Thread attribute" = $logLines -match 'thread="\d+"'
}

$allPassed = $true
foreach ($check in $checks.GetEnumerator()) {
    $passed = $check.Value.Count -gt 0
    $icon = if ($passed) { "?" } else { "?"; $allPassed = $false }
    Write-Host "  $icon $($check.Key)" -ForegroundColor $(if ($passed) { "Green" } else { "Red" })
}

Write-Host ""

if (-not $allPassed) {
    Write-Host "? Alcuni controlli falliti. Verificare il CMTraceFormatter." -ForegroundColor Red
    exit 1
}

Write-Host "? Formato CMTrace verificato con successo!" -ForegroundColor Green
Write-Host ""

# Opzionalmente apri con CMTrace
if (Test-Path $CMTracePath) {
    Write-Host "Vuoi aprire il log con CMTrace? (S/N)" -ForegroundColor Yellow
    $response = Read-Host
    
    if ($response -eq "S" -or $response -eq "s" -or $response -eq "Y" -or $response -eq "y") {
        Write-Host "Apertura CMTrace..." -ForegroundColor Cyan
        Start-Process -FilePath $CMTracePath -ArgumentList "`"$($logFile.FullName)`""
        Write-Host ""
        Write-Host "CMTrace aperto. Verifica che:" -ForegroundColor Cyan
        Write-Host "  ? I log siano colorati (giallo per warning, rosso per error)" -ForegroundColor Gray
        Write-Host "  ? Le colonne siano popolate correttamente" -ForegroundColor Gray
        Write-Host "  ? Real-time monitoring funzioni (Tools ? Start Monitoring)" -ForegroundColor Gray
    }
} else {
    Write-Host "CMTrace non trovato in: $CMTracePath" -ForegroundColor Yellow
    Write-Host "Per aprire il log manualmente:" -ForegroundColor Cyan
    Write-Host "  CMTrace.exe `"$($logFile.FullName)`"" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test completato con successo!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

exit 0
