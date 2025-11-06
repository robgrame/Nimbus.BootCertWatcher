# Start Development Environment
# Avvia API e Web in parallelo

Write-Host "?? Starting Secure Boot Dashboard Development Environment" -ForegroundColor Cyan
Write-Host ""

# Start API in background
Write-Host "Starting API on https://localhost:7120..." -ForegroundColor Green
$apiJob = Start-Job -ScriptBlock {
    Set-Location $using:PSScriptRoot
    Set-Location SecureBootDashboard.Api
    dotnet run
}

# Wait a bit for API to start
Start-Sleep -Seconds 3

# Start Web in background
Write-Host "Starting Web on https://localhost:7001..." -ForegroundColor Green
$webJob = Start-Job -ScriptBlock {
    Set-Location $using:PSScriptRoot
    Set-Location SecureBootDashboard.Web
    dotnet run
}

# Wait a bit for Web to start
Start-Sleep -Seconds 3

Write-Host ""
Write-Host "? Services started!" -ForegroundColor Green
Write-Host ""
Write-Host "?? Available URLs:" -ForegroundColor Yellow
Write-Host "   API:     https://localhost:7120" -ForegroundColor White
Write-Host "   Swagger: https://localhost:7120/swagger" -ForegroundColor White
Write-Host "   Web:     https://localhost:7001" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop all services" -ForegroundColor Yellow
Write-Host ""

# Monitor jobs
try {
    while ($true) {
        # Show API output
        $apiOutput = Receive-Job $apiJob
        if ($apiOutput) {
            Write-Host "[API] " -ForegroundColor Blue -NoNewline
            Write-Host $apiOutput
        }

        # Show Web output
        $webOutput = Receive-Job $webJob
        if ($webOutput) {
            Write-Host "[WEB] " -ForegroundColor Magenta -NoNewline
            Write-Host $webOutput
        }

        Start-Sleep -Milliseconds 500
    }
}
finally {
    Write-Host ""
    Write-Host "?? Stopping services..." -ForegroundColor Red
    Stop-Job $apiJob, $webJob
    Remove-Job $apiJob, $webJob
    Write-Host "? All services stopped" -ForegroundColor Green
}
