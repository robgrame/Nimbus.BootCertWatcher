# Quick Fix Script for Deploy-WebDashboard.ps1
# This script reads Deploy-ApiServer.ps1 and adapts it for Deploy-WebDashboard.ps1

Write-Host "Creating fixed Deploy-WebDashboard.ps1 based on Deploy-ApiServer.ps1..." -ForegroundColor Cyan

# Read the fixed Deploy-ApiServer.ps1
$apiScript = Get-Content "scripts\Deploy-ApiServer.ps1" -Raw

# Replace all API-specific references with Web-specific ones
$webScript = $apiScript `
    -replace 'DeployApiServerRunning', 'DeployWebDashboardRunning' `
    -replace 'SecureBootDashboard\.Api', 'SecureBootDashboard.Web' `
    -replace 'SecureBootDashboard - API Server Deployment', 'SecureBootDashboard - Web Dashboard Deployment' `
    -replace 'API Server', 'Web Dashboard' `
    -replace 'api\.yourdomain\.com', 'dashboard.yourdomain.com' `
    -replace 'API binaries published', 'Web Dashboard binaries published' `
    -replace 'SecureBootDashboard\.Api\\bin', 'SecureBootDashboard.Web\bin' `
    -replace 'Deploys SecureBootDashboard API Server to IIS', 'Deploys SecureBootDashboard Web Dashboard to IIS' `
    -replace 'Deploy-ApiServer\.ps1', 'Deploy-WebDashboard.ps1' `
    -replace 'api-\*\.log', 'web-*.log' `
    -replace 'C:\\inetpub\\SecureBootDashboard\.Api', 'C:\inetpub\SecureBootDashboard.Web' `
    -replace 'Publish the API first using:', 'Publish the Web Dashboard first using:' `
    -replace 'dotnet publish SecureBootDashboard\.Api', 'dotnet publish SecureBootDashboard.Web' `
    -replace 'Testing API Server', 'Testing Web Dashboard' `
    -replace 'API health check', 'Web Dashboard health check' `
    -replace 'Copying API Server files', 'Copying Web Dashboard files' `
    -replace 'Health: \$url/health', 'Health: $url' `
    -replace 'Swagger: \$url/swagger', 'Dashboard: $url' `
    -replace 'API Base: \$url/api', '(Main page)' `
    -replace 'Test API endpoint: \$url/health', 'Test Dashboard endpoint: $url'

# Save the adapted script
$webScript | Set-Content "scripts\Deploy-WebDashboard.ps1.NEW" -Encoding UTF8

Write-Host "? Created scripts\Deploy-WebDashboard.ps1.NEW" -ForegroundColor Green
Write-Host "  Review the file and then rename it to Deploy-WebDashboard.ps1" -ForegroundColor Yellow

